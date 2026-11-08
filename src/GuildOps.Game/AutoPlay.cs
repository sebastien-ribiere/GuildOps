using Godot;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>
/// Scénario de preuve (option <c>-- --autoplay [--shots=dossier]</c>) : joue 7 jours via les MÊMES commandes que les boutons
/// (sélection puis GameShell.Assign/Resolve/…). Politique déterministe, identique à celle du test de parité Core.
/// Il vérifie aussi, sur l'interface réellement affichée : mise en page (cartes dans l'écran, textes non tronqués), glyphes accentués,
/// absence de mutation lors d'un refus / d'un clic répété / d'un rafraîchissement, et un parcours entièrement au clavier.
/// Les lignes <c>*_CHECK</c> et <c>AUTOPLAY_RESULT</c> sont lues par tools/run-autoplay.sh ; tout échec fait quitter avec un code non nul.
/// </summary>
public sealed class AutoPlay(GameShell shell)
{
    private static readonly Rect2 Screen = new(0, 0, 640, 360);
    private string? _shotsDir;
    private string _policy = "first-fit";
    private int _failures;
    private readonly HashSet<string> _done = [];

    public async Task Run()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--shots=")) _shotsDir = arg["--shots=".Length..];
            if (arg.StartsWith("--policy=")) _policy = arg["--policy=".Length..];
        }
        if (_shotsDir is not null) DirAccess.MakeDirRecursiveAbsolute(_shotsDir);

        var s = shell.Session;
        await Shot("01-hall", View.Hall);
        if (_policy == "first-fit")
        {
            await KeyboardChecks();
            shell.Reset(); // le parcours clavier a joué une mission : on repart de la graine pour que le scénario reste celui de référence
            await Frame(2);
            await EmptyStates();
            await FirstFit(s);
        }
        else await Reference(s);

        await Shot("16-report-final", View.Report);
        var g = s.Guild;
        GD.Print($"AUTOPLAY_RESULT policy={_policy} day={g.Day} treasury={g.TreasuryCopper} reputation={g.Reputation} succeeded={g.MissionsSucceeded} events={s.Events.Count} status={s.Status} success={s.Report?.Success}");
        WriteJournal(s);
        GD.Print($"AUTOPLAY_FAILED_CHECKS {_failures}");
        shell.GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    /// <summary>États vides (début de partie) : missions et réclamations sans contenu, avec un message utile.</summary>
    private async Task EmptyStates()
    {
        await Shot("17-missions-empty", View.Missions);
        await Scan("Missions");
        await Shot("18-complaints-empty", View.Complaints);
        await Scan("Complaints");
        shell.Show(View.Hall);
    }

    private async Task FirstFit(GameSession s)
    {
        while (s.Status == GameStatus.InProgress)
        {
            var day = s.Guild.Day;
            for (var n = 0; n < s.Data.Rules.MaxMissionsPerDay; n++)
            {
                var pick = Pick(s);
                if (pick is null) break;
                await Engage(s, pick.Value.offer, pick.Value.hero);
                if (day == 1 && n == 1 && _done.Add("states")) await StatesShots(s);   // deux missions engagées, une place restante
            }
            await ResolveAll(s);
            foreach (var complaint in s.OpenComplaints.ToList())
            {
                shell.SelectComplaint(complaint.Id);
                shell.Show(View.Complaints);
                if (_done.Add("complaints")) { await Shot("12-complaints", View.Complaints); await Scan("Complaints"); }
                shell.ResolveComplaint(Choose(s, complaint));
                if (_done.Add("complaints-done")) await Shot("13-complaint-resolved", View.Complaints);
            }
            shell.Show(View.Report);
            if (day == 1) { await Shot("14-report-day1", View.Report); await Scan("Report"); }
            if (day == s.Data.Rules.Days) await Shot("15-report-day7-before-end", View.Report);
            shell.EndDay();
        }
    }

    // ---- Parties de référence (docs/playtests) rejouées par l'interface ---------------------------------------------------

    /// <summary>
    /// <c>greedy-ignore|greedy-apologize|greedy-repair</c> : le robot glouton des tests (chance × prime − 100 × engagement), mais ses commandes
    /// passent par l'interface (sélections + GameShell). <c>idle</c> : ne jamais rien faire, 7 fois « Terminer la journée ».
    /// </summary>
    private async Task Reference(GameSession s)
    {
        while (s.Status == GameStatus.InProgress)
        {
            if (_policy != "idle")
            {
                for (var n = 0; n < s.Data.Rules.MaxMissionsPerDay; n++)
                {
                    var best = s.CurrentOffers.Where(o => o.State == OfferState.Available)
                        .SelectMany(o => s.Heroes.Select(h => (Offer: o, Hero: h, Check: s.Preview(o.Id, h.Id))))
                        .Where(x => x.Check.CanAssign)
                        .OrderByDescending(x => x.Check.Chance!.Final * x.Offer.Template.RewardCopper - 100 * x.Check.EngagementCostCopper)
                        .ThenBy(x => x.Offer.Id, StringComparer.Ordinal).ThenBy(x => x.Hero.Id, StringComparer.Ordinal)
                        .Cast<(QuestOffer Offer, Hero Hero, AssignmentCheck Check)?>().FirstOrDefault();
                    if (best is null) break;
                    shell.Show(View.Board); shell.SelectOffer(best.Value.Offer.Id);
                    shell.Show(View.Roster); shell.SelectHero(best.Value.Hero.Id);
                    shell.Assign();
                    var quest = s.Quests.Last();
                    shell.SelectQuest(quest.Id); shell.Show(View.Missions); shell.Resolve();
                    foreach (var complaint in s.OpenComplaints.ToList())
                    {
                        if (_policy == "greedy-ignore") continue;
                        var choice = _policy == "greedy-repair" && s.ComplaintCheck(complaint.Id, ComplaintChoice.Repair).Count == 0
                            ? ComplaintChoice.Repair : ComplaintChoice.Apologize;
                        shell.SelectComplaint(complaint.Id); shell.Show(View.Complaints); shell.ResolveComplaint(choice);
                    }
                }
            }
            shell.Show(View.Report);
            await Frame();
            shell.EndDay();
        }
    }

    /// <summary>Journal complet au format de docs/playtests (« - #n (Jd) message »), pour comparaison ligne à ligne avec les rapports de référence.</summary>
    private void WriteJournal(GameSession s)
    {
        if (_shotsDir is null) return;
        using var file = Godot.FileAccess.Open($"{_shotsDir}/journal.txt", Godot.FileAccess.ModeFlags.Write);
        foreach (var e in s.Events) file.StoreLine($"- #{e.Sequence} (J{e.Day}) {e.Message}");
    }

    // ---- Un engagement complet, avec les contrôles d'interface au passage ------------------------------------------------

    private async Task Engage(GameSession s, string offerId, string heroId)
    {
        shell.Show(View.Board);
        if (_done.Add("board-guide"))
        {
            await Shot("02-board-guide", View.Board);   // aucune sélection : guide en deux étapes
            await Scan("Board");
        }
        shell.SelectOffer(offerId);
        if (_done.Add("board-selected")) await Shot("03-board-selected", View.Board);

        shell.Show(View.Roster);
        if (_done.Add("roster")) { await Shot("04-roster-12-heroes", View.Roster); await Scan("Roster"); }

        // Un héros que Core refuse pour cette offre : aperçu motivé, puis tentative d'affectation refusée sans aucune mutation.
        if (s.Heroes.FirstOrDefault(h => h.Id != heroId && !s.Preview(offerId, h.Id).CanAssign) is { } refused && _done.Add("refused"))
        {
            shell.SelectHero(refused.Id);
            await Shot("06-assignment-impossible", View.Roster);
            var before = Snapshot(s);
            shell.Assign();
            Check("REFUSED_ASSIGN_CHECK", before == Snapshot(s) && !shell.LastToastOk, $"refus « {shell.LastToast} » sans mutation");
        }

        shell.SelectHero(heroId);
        if (_done.Add("assignment")) await Shot("05-assignment-possible", View.Roster);
        shell.Assign();
    }

    /// <summary>Après deux engagements : offres acceptées, héros engagés (⚠), incompatibles (✖), éligible choisi (✔) — les états sont distincts.</summary>
    private async Task StatesShots(GameSession s)
    {
        var next = s.CurrentOffers.FirstOrDefault(o => o.State == OfferState.Available);
        shell.SelectOffer(next?.Id);
        await Shot("03b-board-accepted", View.Board);
        var hero = next is null ? null : s.Heroes.FirstOrDefault(h => s.Preview(next.Id, h.Id).CanAssign);
        shell.SelectHero(hero?.Id);
        await Shot("04b-roster-states", View.Roster);
        await Scan("Roster");
        shell.SelectHero(null);
        shell.SelectOffer(null);
    }

    private async Task ResolveAll(GameSession s)
    {
        foreach (var quest in s.Quests.Where(q => q.State == QuestState.Assigned).ToList())
        {
            shell.SelectQuest(quest.Id);
            shell.Show(View.Missions);
            if (_done.Add("pending")) { await Shot("07-mission-pending", View.Missions); await Scan("Missions"); }
            shell.Resolve();
            var o = shell.Outcomes[quest.Id];

            if (_done.Add("repeat-resolve"))
            {
                // Clic répété sur « Résoudre » + rafraîchissements : ni nouveau tirage ni mutation.
                var before = Snapshot(s);
                shell.Resolve(); shell.Resolve(); shell.RefreshAll(); shell.Show(View.Report); shell.Show(View.Missions);
                Check("REPEAT_RESOLVE_CHECK", before == Snapshot(s) && shell.Outcomes[quest.Id] == o, "résolution répétée et rafraîchissements : état identique");
            }
            if (o.Success && _done.Add("success")) await DetailsShots(quest.Id, "08-mission-success", "09-mission-success-details", s);
            if (!o.Success && _done.Add("failure")) await DetailsShots(quest.Id, "10-mission-failure", "11-mission-failure-details", s);
        }
    }

    /// <summary>Résultat replié, puis détails du calcul ouverts (même contrôle que le joueur au clavier), puis refermés.</summary>
    private async Task DetailsShots(string questId, string closed, string opened, GameSession s)
    {
        shell.SelectQuest(questId);
        await Shot(closed, View.Missions);
        var before = Snapshot(s);
        var toggle = FindButton(shell, "Détails du calcul");
        if (toggle is null) { Check("DETAILS_CHECK", false, "bouton « Détails du calcul » introuvable"); return; }
        toggle.EmitSignal(BaseButton.SignalName.Pressed);
        await Frame(2);
        var shown = FindButton(shell, "Détails du calcul : masquer") is not null;
        await Shot(opened, View.Missions);
        await Scan("Missions");
        FindButton(shell, "Détails du calcul : masquer")?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frame(2);
        Check("DETAILS_CHECK", shown && before == Snapshot(s), "détails ouverts/fermés sans modification de l'état");
    }

    // ---- Politique commune ------------------------------------------------------------------------------------------------

    /// <summary>Politique commune (voir le test de parité) : première offre disponible × premier héros dont Preview.CanAssign est vrai.</summary>
    public static (string offer, string hero)? Pick(GameSession s)
    {
        foreach (var offer in s.CurrentOffers.Where(o => o.State == OfferState.Available))
            foreach (var hero in s.Heroes)
                if (s.Preview(offer.Id, hero.Id).CanAssign) return (offer.Id, hero.Id);
        return null;
    }

    public static ComplaintChoice Choose(GameSession s, Complaint c)
    {
        foreach (var choice in new[] { ComplaintChoice.Repair, ComplaintChoice.Apologize, ComplaintChoice.Deny })
            if (s.ComplaintCheck(c.Id, choice).Count == 0) return choice;
        return ComplaintChoice.Deny;
    }

    /// <summary>Empreinte de tout ce que Core peut modifier : si elle ne bouge pas, aucune mutation n'a eu lieu.</summary>
    private static string Snapshot(GameSession s)
        => $"{s.Guild.Day}|{s.Guild.TreasuryCopper}|{s.Guild.Reputation}|{s.Guild.MissionsAcceptedToday}|{s.Guild.MissionsSucceeded}|{s.Events.Count}|{s.Quests.Count}|{s.Complaints.Count}|" +
           string.Join(",", s.Quests.Select(q => $"{q.Id}:{q.State}")) + "|" + string.Join(",", s.Heroes.Select(h => $"{h.Id}:{h.Energy}"));

    // ---- Contrôles d'interface -------------------------------------------------------------------------------------------

    private void Check(string name, bool ok, string detail)
    {
        if (!ok) _failures++;
        GD.Print($"{name} {(ok ? "OK" : "FAIL")} — {detail}");
    }

    /// <summary>Mise en page et glyphes de la vue affichée : cartes dans l'écran, textes non tronqués, glyphes présents dans la police.</summary>
    private async Task Scan(string viewName)
    {
        await Frame(2);
        var root = shell.GetNode<Control>("Content/" + viewName);
        var outside = new List<string>();
        var truncated = new List<string>();
        var missing = new HashSet<char>();
        var cards = 0;
        void Walk(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is Control { Visible: false }) continue;
                if (child is CardButton card)
                {
                    cards++;
                    var rect = card.GetGlobalRect();
                    // Une carte dans une zone défilante peut dépasser volontairement ; ici aucune ne doit sortir de l'écran 640×360.
                    if (!Screen.Encloses(rect)) outside.Add($"{card.TooltipText.Split('\n')[0]} {rect}");
                }
                if (child is Label label && label.IsVisibleInTree() && label.Text.Length > 0)
                {
                    if (IsTruncated(label)) truncated.Add(label.Text);
                    foreach (var ch in label.Text) if (!char.IsWhiteSpace(ch) && !label.GetThemeFont("font").HasChar(ch)) missing.Add(ch);
                }
                if (child is Button button && button.IsVisibleInTree() && button.Text.Length > 0)
                {
                    // Libellé de bouton coupé : largeur du texte (hors marges de 6 px) supérieure à celle du bouton.
                    var wide = button.GetThemeFont("font").GetStringSize(button.Text, HorizontalAlignment.Left, -1, button.GetThemeFontSize("font_size")).X;
                    if (wide > button.Size.X - 12) truncated.Add(button.Text);
                }
                if (child is Button button2 && button2.IsVisibleInTree() && button2.Text.Length > 0)
                    foreach (var ch in button2.Text) if (!char.IsWhiteSpace(ch) && !button2.GetThemeFont("font").HasChar(ch)) missing.Add(ch);
                if (child is RichTextLabel rich && rich.IsVisibleInTree())
                    foreach (var ch in rich.GetParsedText()) if (!char.IsWhiteSpace(ch) && !rich.GetThemeFont("normal_font").HasChar(ch)) missing.Add(ch);
                Walk(child);
            }
        }
        Walk(root);
        var ok = outside.Count == 0 && truncated.Count == 0 && missing.Count == 0;
        Check("LAYOUT_CHECK", ok, $"vue={viewName} cartes={cards} hors-écran={outside.Count} tronqués={truncated.Count} glyphes-manquants={missing.Count}" +
            (ok ? "" : $" {string.Join(" ; ", outside.Concat(truncated.Select(t => "« " + t + " »")))} {string.Join("", missing)}"));
    }

    /// <summary>Texte coupé : lignes masquées (MaxLinesVisible), ellipse ou débordement horizontal d'une ligne non repliée.</summary>
    private static bool IsTruncated(Label label)
    {
        if (label.AutowrapMode != TextServer.AutowrapMode.Off)
            return label.GetVisibleLineCount() < label.GetLineCount();
        var font = label.GetThemeFont("font");
        var size = label.GetThemeFontSize("font_size");
        return font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X > label.Size.X + 0.5f && label.Size.X > 0;
    }

    private static Button? FindButton(Node root, string textPrefix)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button { Visible: true } b && b.IsVisibleInTree() && b.Text.StartsWith(textPrefix, StringComparison.Ordinal)) return b;
            if (FindButton(child, textPrefix) is { } found) return found;
        }
        return null;
    }

    // ---- Clavier seul -----------------------------------------------------------------------------------------------------

    private async Task Frame(int count = 1)
    {
        for (var i = 0; i < count; i++) await shell.ToSignal(shell.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Press(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        await Frame(2);
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
        await Frame(1);
    }

    private Control? Focused() => shell.GetViewport().GuiGetFocusOwner();

    /// <summary>Tabule (ui_focus_next) jusqu'à ce que le contrôle voulu ait le focus ; faux si le tour complet n'y mène pas.</summary>
    private async Task<bool> TabTo(Func<Control, bool> wanted, int max = 60)
    {
        for (var i = 0; i < max; i++)
        {
            if (Focused() is { } c && wanted(c)) return true;
            await Press("ui_focus_next");
        }
        return Focused() is { } last && wanted(last);
    }

    /// <summary>
    /// Navigation clavier : focus à l'ouverture de chaque vue, Échap vers le hall, puis un parcours complet sans souris :
    /// hall → contrats (Entrée) → choisir une quête → « Choisir un héros » → choisir un héros → « Affecter » → missions → « Résoudre ».
    /// </summary>
    private async Task KeyboardChecks()
    {
        var focus = new List<string>();
        foreach (var view in new[] { View.Hall, View.Board, View.Roster, View.Missions, View.Complaints, View.Report })
        {
            shell.Show(view);
            await Frame();
            focus.Add($"{view}:{(Focused() is null ? "none" : "ok")}");
        }
        shell.Show(View.Board);
        await Press("ui_cancel");
        Check("KEYBOARD_CHECK", focus.All(f => f.EndsWith("ok")) && shell.Current == View.Hall, $"focus[{string.Join(" ", focus)}] escape_to_hall={shell.Current == View.Hall}");

        var s = shell.Session;
        shell.Show(View.Hall);
        await Frame(2);
        var steps = new List<string>();
        bool Ok(string step, bool cond) { steps.Add($"{step}:{(cond ? "ok" : "KO")}"); return cond; }

        // Hall : le premier focus est la zone « Contrats ».
        var reachBoard = await TabTo(c => c is Button b && b.TooltipText.StartsWith("Contrats"));
        await Press("ui_accept");
        var flow = Ok("hall→contrats", reachBoard && shell.Current == View.Board);

        // Contrats : Entrée sur la première offre la sélectionne.
        if (flow) { await Frame(2); flow = Ok("offre", Focused() is CardButton) ; }
        if (flow) { await Press("ui_accept"); flow = Ok("offre-choisie", shell.SelectedOffer is not null); }
        // « Choisir un héros → » (étape 2 du panneau d'affectation) puis un héros.
        if (flow) { flow = Ok("vers-héros", await TabTo(c => c is Button b && b.Text.StartsWith("Choisir un héros"))); }
        if (flow) { await Press("ui_accept"); await Frame(2); flow = Ok("grille-héros", shell.Current == View.Roster && Focused() is CardButton); }
        // Le premier héros éligible : on avance avec les flèches/tab sur la grille.
        if (flow)
        {
            var offerId = shell.SelectedOfferId!;
            var eligible = s.Heroes.Select((h, i) => (h, i)).First(t => s.Preview(offerId, t.h.Id).CanAssign);
            for (var i = 0; i < eligible.i; i++) await Press("ui_focus_next");
            await Press("ui_accept");
            flow = Ok("héros-choisi", shell.SelectedHeroId == eligible.h.Id);
        }
        // « Affecter le héros » : le bouton est atteint au clavier et engage la mission.
        if (flow)
        {
            var before = s.Quests.Count;
            var found = await TabTo(c => c is Button b && b.Text.StartsWith("Affecter") && !b.Disabled);
            await Press("ui_accept");
            flow = Ok("affecter", found && s.Quests.Count == before + 1);
        }
        // Missions : la mission en cours se résout au clavier.
        if (flow)
        {
            shell.Show(View.Missions);
            await Frame(2);
            var quest = s.Quests.Last();
            shell.SelectQuest(quest.Id);
            await Frame(2);
            var found = await TabTo(c => c is Button b && b.Text.StartsWith("Résoudre") && !b.Disabled);
            await Press("ui_accept");
            flow = Ok("résoudre", found && s.Quests.Last().State != QuestState.Assigned && shell.Outcomes.ContainsKey(quest.Id));
        }
        Check("KEYBOARD_FLOW_CHECK", flow, string.Join(" ", steps));
        shell.Show(View.Hall);
    }

    // ---- Captures ---------------------------------------------------------------------------------------------------------

    private async Task Shot(string name, View view)
    {
        shell.Show(view);
        if (_shotsDir is null) return;
        await shell.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await shell.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = shell.GetViewport().GetTexture().GetImage();
        image.Resize(image.GetWidth() * 2, image.GetHeight() * 2, Image.Interpolation.Nearest);
        image.SavePng($"{_shotsDir}/{name}.png");
    }
}
