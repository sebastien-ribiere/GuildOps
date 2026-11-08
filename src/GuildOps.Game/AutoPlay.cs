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
    private Probe _probe = null!;
    private readonly HashSet<string> _done = [];

    public async Task Run()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--shots=")) _shotsDir = arg["--shots=".Length..];
            if (arg.StartsWith("--policy=")) _policy = arg["--policy=".Length..];
        }
        if (_shotsDir is not null) DirAccess.MakeDirRecursiveAbsolute(_shotsDir);
        _probe = new Probe(shell, _shotsDir);

        var s = shell.Session;
        await Shot("01-hall", View.Hall);
        await AmbientChecks();
        await ArtChecks(s);
        if (_policy == "first-fit")
        {
            await KeyboardChecks();
            shell.Reset(); // le parcours clavier a joué une mission : on repart de la graine pour que le scénario reste celui de référence
            await Frame(2);
            await EmptyStates();
            await ViewerCheck(s);
            await FirstFit(s);
        }
        else await Reference(s);

        await Shot("16-report-final", View.Report);
        var g = s.Guild;
        GD.Print($"AUTOPLAY_RESULT policy={_policy} day={g.Day} treasury={g.TreasuryCopper} reputation={g.Reputation} succeeded={g.MissionsSucceeded} events={s.Events.Count} status={s.Status} success={s.Report?.Success}");
        WriteJournal(s);
        GD.Print($"AUTOPLAY_FAILED_CHECKS {_probe.Failures}");
        shell.GetTree().Quit(_probe.Failures == 0 ? 0 : 1);
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
        if (_done.Add("roster")) { await Shot("04-roster-12-heroes", View.Roster); await Scan("Roster"); await ShotRosterScrolled(); }

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
            var fxBefore = CosmeticFx.Played.Count;
            shell.Resolve();
            var o = shell.Outcomes[quest.Id];
            await Frame(8); // l'effet est en cours : on le capture réellement, avant de naviguer
            var expected = ExpectedFx(o);
            if (o.Success && o.Incident is null && _done.Add("fx-success")) await ShotNow("08a-fx-succes");
            if (!o.Success && o.Incident is null && _done.Add("fx-failure")) await ShotNow("10a-fx-echec");
            // Laisse l'enchaînement (verdict, prime, incident) se dérouler, en vérifiant qu'il se termine tout seul.
            var last = expected.Count > 1 && o.Incident is not null && _done.Add("fx-incident");
            for (var i = 0; i < 400 && CosmeticFx.Active > 0; i++)
            {
                if (last && CosmeticFx.Played.Count - fxBefore == expected.Count && CosmeticFx.Played[^1] == FxKind.Incident) { await Frame(4); await ShotNow("12a-fx-incident"); last = false; }
                await Frame();
            }
            var played = CosmeticFx.Played.Skip(fxBefore).ToList();
            Check("FX_CHECK", played.SequenceEqual(expected), $"résultat {(o.Success ? "succès" : "échec")} prime={o.RewardPaidCopper} incident={o.Incident is not null} : effets attendus [{string.Join(",", expected)}] joués [{string.Join(",", played)}]");

            if (_done.Add("repeat-resolve"))
            {
                // Clic répété sur « Résoudre » + rafraîchissements : ni nouveau tirage, ni mutation, ni nouvel effet.
                var before = Snapshot(s);
                var fxCount = CosmeticFx.Played.Count;
                shell.Resolve(); shell.Resolve(); shell.RefreshAll(); shell.Show(View.Report); shell.Show(View.Missions);
                Check("REPEAT_RESOLVE_CHECK", before == Snapshot(s) && shell.Outcomes[quest.Id] == o && fxCount == CosmeticFx.Played.Count, "résolution répétée, rafraîchissements et changements d'écran : état identique, aucun nouvel effet");
            }
            if (o.Success && _done.Add("success")) await DetailsShots(quest.Id, "08-mission-success", "09-mission-success-details", s);
            if (!o.Success && _done.Add("failure")) await DetailsShots(quest.Id, "10-mission-failure", "11-mission-failure-details", s);
        }
    }

    /// <summary>Effets que Core autorise pour ce résultat : verdict, prime effectivement versée, incident effectivement généré.</summary>
    private static List<FxKind> ExpectedFx(MissionOutcome o)
    {
        var list = new List<FxKind> { o.Success ? FxKind.Succes : FxKind.Echec };
        if (o.RewardPaidCopper > 0) list.Add(FxKind.Recompense);
        if (o.Incident is not null) list.Add(FxKind.Incident);
        return list;
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

    private void Check(string name, bool ok, string detail) => _probe.Check(name, ok, detail);

    /// <summary>Capture de la grille des héros défilée en bas (3ᵉ rangée), puis retour en haut pour ne pas fausser les contrôles suivants.</summary>
    private async Task ShotRosterScrolled()
    {
        var scroll = shell.GetNode<Control>("Content/Roster").FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().FirstOrDefault();
        if (scroll is null) return;
        scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
        await Shot("04c-roster-scrolled", View.Roster);
        scroll.ScrollVertical = 0;
        await Frame(2);
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
                    // Dans un ScrollContainer, seule la sortie latérale ou par le haut est une erreur ; le bas est atteint en défilant.
                    var scrolled = node.GetParent() is ScrollContainer;
                    var inside = scrolled ? rect.Position.X >= 0 && rect.End.X <= Screen.End.X && rect.Position.Y >= 0 : Screen.Encloses(rect);
                    if (!inside) outside.Add($"{card.TooltipText.Split('\n')[0]} {rect}");
                }
                if (child is Label label && label.IsVisibleInTree() && label.Text.Length > 0)
                {
                    if (IsTruncated(label)) truncated.Add(label.Text);
                    foreach (var ch in label.Text) if (ch is not (' ' or '\n' or '\t' or '\r') && !label.GetThemeFont("font").HasChar(ch)) missing.Add(ch);
                }
                if (child is Button button && button.IsVisibleInTree() && button.Text.Length > 0)
                {
                    // Libellé de bouton coupé : largeur du texte (hors marges de 6 px) supérieure à celle du bouton.
                    var wide = button.GetThemeFont("font").GetStringSize(button.Text, HorizontalAlignment.Left, -1, button.GetThemeFontSize("font_size")).X;
                    if (wide > button.Size.X - 12) truncated.Add(button.Text);
                }
                if (child is Button button2 && button2.IsVisibleInTree() && button2.Text.Length > 0)
                    foreach (var ch in button2.Text) if (ch is not (' ' or '\n' or '\t' or '\r') && !button2.GetThemeFont("font").HasChar(ch)) missing.Add(ch);
                if (child is RichTextLabel rich && rich.IsVisibleInTree())
                    foreach (var ch in rich.GetParsedText()) if (ch is not (' ' or '\n' or '\t' or '\r') && !rich.GetThemeFont("normal_font").HasChar(ch)) missing.Add(ch);
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

    private static Button? FindButton(Node root, string textPrefix) => Probe.FindButton(root, textPrefix);

    // ---- Clavier seul -----------------------------------------------------------------------------------------------------

    private Task Frame(int count = 1) => _probe.Frame(count);
    private Task Press(string action) => _probe.Press(action);
    private Control? Focused() => _probe.Focused();
    private Task<bool> TabTo(Func<Control, bool> wanted, int max = 60) => _probe.TabTo(wanted, max);

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

    // ---- Art B5.2 -----------------------------------------------------------------------------------------------------------

    /// <summary>Les 59 PNG du kit se chargent, aux bonnes tailles ; les 20 contrats du catalogue ont leurs deux illustrations, par identifiant.</summary>
    private async Task ArtChecks(GameSession s)
    {
        var png = new List<(string path, Vector2I size)>();
        foreach (var dir in new[] { "ambient", "categories", "levels", "quests", "quests_hd", "skills", "vfx" })
            foreach (var f in DirAccess.GetFilesAt($"res://Assets/b5_2/{dir}").Where(f => f.EndsWith(".png"))) png.Add(($"b5_2/{dir}/{f}", Vector2I.Zero));
        var bad = new List<string>();
        foreach (var (path, _) in png)
        {
            var t = UiKit.Tex(path);
            if (t is null || t.GetWidth() == 0) bad.Add(path);
        }
        Check("ASSETS_CHECK", png.Count == 59 && bad.Count == 0, $"{png.Count} PNG B5.2 chargés (59 attendus), échecs={bad.Count} {string.Join(",", bad)}");

        var missing = new List<string>();
        foreach (var t in s.Data.Templates)
        {
            if (Art.Thumb(t.Id) is not { } a || a.GetSize() != new Vector2(96, 64)) missing.Add(t.Id + "/96");
            if (Art.Hd(t.Id) is not { } b || b.GetSize() != new Vector2(384, 256)) missing.Add(t.Id + "/384");
            if (Art.HdHalf(t.Id).GetSize() != new Vector2(192, 128)) missing.Add(t.Id + "/192");
        }
        Check("QUEST_ART_CHECK", s.Data.Templates.Count == 20 && missing.Count == 0, $"{s.Data.Templates.Count} modèles, illustrations 96×64 / 384×256 / 192×128 trouvées par identifiant, manquantes={missing.Count} {string.Join(",", missing)}");
        await Task.CompletedTask;
    }

    /// <summary>Hall : deux ambiances en boucle, décoratives (aucun filtre souris), qui avancent réellement et ne touchent pas aux zones cliquables.</summary>
    private async Task AmbientChecks()
    {
        shell.Show(View.Hall);
        var hall = shell.GetNode<Control>("Content/Hall");
        var fx = hall.GetChildren().OfType<AmbientFx>().ToList();
        var zones = hall.GetChildren().OfType<Button>().Select(b => b.GetGlobalRect()).ToList();
        var first = fx.Select(f => ((AtlasTexture)f.Texture).Region.Position.X).ToList();
        await Frame(40);
        await ShotNow("01b-hall-ambient-a");
        await Frame(25);
        await ShotNow("01c-hall-ambient-b");
        var later = fx.Select(f => ((AtlasTexture)f.Texture).Region.Position.X).ToList();
        var zonesAfter = hall.GetChildren().OfType<Button>().Select(b => b.GetGlobalRect()).ToList();
        var ok = fx.Count == 2 && fx.All(f => f.MouseFilter == Control.MouseFilterEnum.Ignore && f.FocusMode == Control.FocusModeEnum.None)
                 && !first.SequenceEqual(later) && zones.SequenceEqual(zonesAfter) && zones.Count == 5
                 && fx.All(f => hall.GetChildren().IndexOf(f) < hall.GetChildren().ToList().FindIndex(c => c is Button));
        Check("AMBIENT_CHECK", ok, $"ambiances={fx.Count} frames {string.Join(",", first)} -> {string.Join(",", later)} ; 5 zones cliquables inchangées ; sous les zones et sans filtre souris");
    }

    /// <summary>Visionneuse HD : ouverture au clavier depuis le tableau, focus piégé, Échap la ferme sans quitter la vue.</summary>
    private async Task ViewerCheck(GameSession s)
    {
        shell.Show(View.Board);
        var offer = s.CurrentOffers.First(o => o.State == OfferState.Available);
        shell.SelectOffer(offer.Id);
        await Frame(2);
        var snap = Snapshot(s);
        var thumb = shell.GetNode<Control>("Content/Board").FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.TooltipText.Contains("illustration"));
        var found = thumb is not null;
        thumb?.GrabFocus();
        await Press("ui_accept");
        await Frame(2);
        var open = shell.IllustrationOpen;
        await Press("ui_focus_next");
        var trapped = shell.GetViewport().GuiGetFocusOwner() is Button { Text: "Fermer (Échap)" };
        await ShotNow("03c-board-illustration-hd");
        await Press("ui_cancel");
        await Frame(2);
        var closed = !shell.IllustrationOpen && shell.Current == View.Board;
        Check("VIEWER_CHECK", found && open && trapped && closed && snap == Snapshot(s), $"bouton={found} ouvert={open} focus-piégé={trapped} fermé-par-Échap-sans-quitter-la-vue={closed}, état de session inchangé");
        shell.SelectOffer(null);
    }

    // ---- Captures ---------------------------------------------------------------------------------------------------------

    /// <summary>Capture de l'écran tel quel (sans navigation ni rafraîchissement : un effet en cours reste visible).</summary>
    private Task ShotNow(string name) => _probe.ShotNow(name);

    private async Task Shot(string name, View view)
    {
        shell.Show(view);
        await _probe.ShotNow(name);
    }
}
