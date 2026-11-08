using Godot;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>
/// Contrôle du PARCOURS NORMAL (option <c>-- --flowcheck [--shots=dossier]</c>) : le jeu démarre comme pour un joueur (écran titre, aucun
/// contournement), puis un joueur virtuel utilise les vrais contrôles (actions clavier réelles, boutons) : titre → introduction → hall → …
/// Contrairement à <see cref="AutoPlay"/>, rien n'est supprimé : les modales sont là et doivent se laisser traverser.
/// À chaque étape : une seule <c>GameSession</c> (même instance, même graine), aucun son, aucune modale résiduelle.
/// Les lignes <c>FLOW_*_CHECK</c> sont lues par tools/run-flow.sh ; tout échec fait quitter avec un code non nul.
/// </summary>
public sealed class FlowCheck(GameShell shell)
{
    private Probe _p = null!;
    private GameSession _session = null!;
    private long _seed;
    private string _freshSnapshot = "";

    public async Task Run()
    {
        string? shots = null;
        foreach (var arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--shots=")) shots = arg["--shots=".Length..];
        if (shots is not null) DirAccess.MakeDirRecursiveAbsolute(shots);
        _p = new Probe(shell, shots);
        _session = shell.Session;
        _seed = _session.Seed;
        _freshSnapshot = Probe.Snapshot(_session);
        await _p.Frame(3);

        AssetsB53();
        await TitleAndIntro();
        SampleSilence();
        await HelpFromTitleAndGame();
        SampleSilence();
        await SettingsAndComfort();
        SampleSilence();
        await CreditsScreenCheck();
        SampleSilence();
        await VictoryRun();
        SampleSilence();
        await NewGameAndDefeatRun();
        SampleSilence();

        _p.Check("FLOW_SILENCE_CHECK", _players == 0 && _peak < -60f && AudioServer.BusCount >= 1,
            $"silence : {_silenceSamples} relevés pendant le parcours, {_players} lecteur(s) audio dans l'arbre de scène, crête maximale du mixage {_peak:0.#} dB");
        _p.Check("FLOW_SESSION_CHECK", ReferenceEquals(_session, shell.Session) && shell.Session.Seed == _seed && _seed == shell.Session.Data.Rules.TestSeed,
            $"une seule GameSession pendant tout le parcours, graine de référence {_seed} inchangée");
        GD.Print($"FLOW_INFO dossier des données utilisateur : {OS.GetUserDataDir()}");
        GD.Print($"FLOW_RESULT failures={_p.Failures}");
        shell.GetTree().Quit(_p.Failures == 0 ? 0 : 1);
    }

    /// <summary>Capture après la fin du fondu d'ouverture éventuel (sinon l'écran serait semi-transparent).</summary>
    private async Task Shot(string name)
    {
        await _p.Frame(2);
        for (var i = 0; i < 60 && shell.TopOverlay is { Fading: true }; i++) await _p.Frame();
        await _p.ShotNow(name);
    }

    // ---- Silence : aucun lecteur audio, aucune crête sur le bus maître ----------------------------------------------------------
    private int _silenceSamples, _players;
    private float _peak = -200f;

    private void SampleSilence()
    {
        _silenceSamples++;
        void Walk(Node n)
        {
            if (n is AudioStreamPlayer or AudioStreamPlayer2D or AudioStreamPlayer3D) _players++;
            foreach (var c in n.GetChildren()) Walk(c);
        }
        Walk(shell.GetTree().Root);
        for (var bus = 0; bus < AudioServer.BusCount; bus++)
            _peak = Math.Max(_peak, Math.Max(AudioServer.GetBusPeakVolumeLeftDb(bus, 0), AudioServer.GetBusPeakVolumeRightDb(bus, 0)));
    }

    /// <summary>Les 27 PNG du kit B5.3 sont présents et se chargent (dans l'éditeur comme dans un export, où seul « x.png.import » est listé).</summary>
    private void AssetsB53()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        void Walk(string dir)
        {
            foreach (var d in DirAccess.GetDirectoriesAt(dir)) Walk($"{dir}/{d}");
            foreach (var f in DirAccess.GetFilesAt(dir))
            {
                var name = f.EndsWith(".import") ? f[..^".import".Length] : f;
                if (name.EndsWith(".png")) found.Add($"{dir}/{name}"["res://Assets/".Length..]);
            }
        }
        Walk("res://Assets/b5_3");
        var broken = found.Where(f => UiKit.Tex(f) is not { } t || t.GetWidth() == 0).ToList();

        // Contenu embarqué : aucun fichier audio, ni sources/ ni previews/ du kit (vrai dans l'éditeur comme dans l'export).
        var audio = new List<string>();
        var extras = new List<string>();
        void Scan(string dir)
        {
            foreach (var d in DirAccess.GetDirectoriesAt(dir))
            {
                if (d is "sources" or "previews") extras.Add($"{dir}/{d}");
                if (d is not ".godot") Scan($"{dir}/{d}");
            }
            foreach (var f in DirAccess.GetFilesAt(dir))
                if (new[] { ".ogg", ".wav", ".mp3", ".opus", ".flac" }.Any(e => f.Contains(e, StringComparison.OrdinalIgnoreCase))) audio.Add($"{dir}/{f}");
        }
        Scan("res://");
        _p.Check("FLOW_ASSETS_CHECK", found.Count == 27 && broken.Count == 0 && audio.Count == 0 && extras.Count == 0,
            $"{found.Count} PNG B5.3 listés (27 attendus), illisibles={broken.Count} {string.Join(",", broken)} ; fichiers audio embarqués={audio.Count} {string.Join(",", audio)} ; dossiers sources/previews={extras.Count}");
    }

    private bool SameSession => ReferenceEquals(_session, shell.Session) && shell.Session.Seed == _seed;

    private static Button? Find(Node root, string prefix) => Probe.FindButton(root, prefix);

    // ---- Titre et introduction ------------------------------------------------------------------------------------------

    private async Task TitleAndIntro()
    {
        var title = shell.Title;
        var labels = title.MenuButtons.Where(b => b.Visible).Select(b => b.Text).ToList();
        _p.Check("FLOW_TITLE_CHECK", title.IsOpen && shell.TopOverlay == title && !shell.HasProgress && !title.ResumeButton.Visible
                                      && _p.Focused() is Button { Text: "Nouvelle partie" } && !labels.Any(l => l.StartsWith("Continuer")),
            $"démarrage normal : titre ouvert, session intacte, focus sur « Nouvelle partie », aucun « Continuer » ni « Reprendre » inventé ; menu [{string.Join(" | ", labels)}]");
        await Shot("t01-title");

        // Clavier : la boucle de focus reste dans le titre ; Échap ne ferme rien.
        var seen = new List<string>();
        for (var i = 0; i < labels.Count + 1; i++)
        {
            seen.Add((_p.Focused() as Button)?.Text ?? "?");
            await _p.Press("ui_focus_next");
        }
        await _p.Press("ui_cancel");
        await Shot("t01b-title-focus");
        var inRing = seen.All(t => labels.Contains(t)) && seen.Distinct().Count() == labels.Count;
        _p.Check("FLOW_TITLE_KEYBOARD_CHECK", inRing && title.IsOpen && shell.TopOverlay == title,
            $"Tab parcourt les {labels.Count} boutons du titre sans en sortir ({string.Join(" > ", seen)}) ; Échap ne ferme pas le titre");

        // Nouvelle partie (session intacte : aucune confirmation) → introduction.
        var eventsBefore = shell.Session.Events.Count;
        await _p.TabTo(c => c is Button { Text: "Nouvelle partie" });
        await _p.Press("ui_accept");
        await _p.Frame(3);
        _p.Check("FLOW_INTRO_CHECK", shell.Intro.IsOpen && !title.IsOpen && !shell.Confirm.IsOpen && shell.Session.Events.Count == eventsBefore && SameSession
                                      && _p.Focused() is Button { Text: "Commencer" },
            "« Nouvelle partie » sur une session intacte : introduction affichée sans confirmation, focus sur « Commencer », rien n'a avancé dans Core");
        await Shot("t02-intro");

        // « Commencer » mène au hall.
        await _p.Press("ui_accept");
        await _p.Frame(3);
        _p.Check("FLOW_START_CHECK", !shell.Intro.IsOpen && !shell.AnyOverlayOpen && shell.Current == View.Hall && _p.Focused() is not null && SameSession
                                      && shell.Session.Events.Count == eventsBefore,
            "Commencer : hall affiché, aucune modale, focus clavier présent, partie intacte");
    }

    // ---- Aide et tutoriel -----------------------------------------------------------------------------------------------

    /// <summary>L'aide depuis le titre (ouverte avant la partie) puis depuis le jeu (HUD, F1) : quatre pages, clavier, Échap, focus, session intacte.</summary>
    private async Task HelpFromTitleAndGame()
    {
        // Depuis le jeu d'abord (le hall est affiché) : F1.
        var snap = Probe.Snapshot(shell.Session);
        var before = _p.Focused();
        await _p.PressKey(Key.F1);
        await _p.Frame(2);
        var help = shell.Help;
        var ok = help.IsOpen && shell.Current == View.Hall && help.Page == 0 && _p.Focused() == help.NextButton;
        await Shot("h5-help-ingame");
        await _p.Press("ui_cancel");
        await _p.Frame(2);
        _p.Check("FLOW_HELP_INGAME_CHECK", ok && !help.IsOpen && shell.Current == View.Hall && _p.Focused() == before && snap == Probe.Snapshot(shell.Session) && SameSession,
            "F1 en jeu : l'aide s'ouvre sur la page 1, Échap la ferme sans changer de vue, focus restitué au contrôle d'origine, session intacte");

        // Bouton « Aide » du HUD, à la souris (signal pressé).
        var hud = Find(shell, "Aide");
        hud?.EmitSignal(BaseButton.SignalName.Pressed);
        await _p.Frame(2);
        var viaHud = hud is not null && help.IsOpen;
        help.Close();
        await _p.Frame(2);
        _p.Check("FLOW_HELP_HUD_CHECK", viaHud && !help.IsOpen, "bouton « Aide » du HUD : ouvre puis ferme l'aide");

        // Depuis le titre : Menu → titre → « Aide et règles ».
        shell.OpenTitle();
        await _p.Frame(3);
        await _p.TabTo(c => c is Button { Text: "Aide et règles" });
        var origin = _p.Focused();
        await _p.Press("ui_accept");
        await _p.Frame(3);
        var pages = new List<string>();
        var allFit = true;
        var okTexture = true;
        for (var page = 0; page < help.PageCount; page++)
        {
            if (page > 0) { await _p.Press("ui_accept"); await _p.Frame(3); }      // « Suivant » (focus déjà dessus)
            pages.Add($"{help.Page + 1}/{help.PageCount}");
            allFit &= help.Body.GetContentHeight() <= help.BodyScroll.Size.Y && help.Body.GetParsedText().Length > 60;
            okTexture &= B53.Tutorial(page + 1).GetSize() == new Vector2(192, 128);
            await Shot($"h{page + 1}-help-page{page + 1}");
        }
        var lastLabel = help.NextButton.Text;
        // Retour arrière au clavier : Précédent.
        await _p.Press("ui_focus_prev");
        var onPrev = _p.Focused() == help.PreviousButton;
        await _p.Press("ui_accept");
        await _p.Frame(2);
        var backTo3 = help.Page == 2;
        _p.Check("FLOW_HELP_PAGES_CHECK", string.Join(",", pages) == "1/4,2/4,3/4,4/4" && lastLabel == "Terminer" && allFit && okTexture && onPrev && backTo3,
            $"pages {string.Join(" ", pages)} par Suivant, dernier bouton « {lastLabel} », texte entier visible sans défilement={allFit}, illustrations 192×128={okTexture}, Précédent atteint et utilisé au clavier");

        // Focus piégé dans l'aide, Échap restitue le focus au bouton du titre.
        var ring = new List<string>();
        for (var i = 0; i < 6; i++) { ring.Add((_p.Focused() as Button)?.Text ?? "?"); await _p.Press("ui_focus_next"); }
        var trapped = ring.All(t => t is "Précédent" or "Suivant" or "Terminer" or "Revoir l'introduction" or "Fermer (Échap)");
        // « Revoir l'introduction » ouvre l'introduction PAR-DESSUS l'aide ; Échap la referme et ramène à l'aide.
        Find(help, "Revoir")?.EmitSignal(BaseButton.SignalName.Pressed);
        await _p.Frame(2);
        var replay = shell.Intro.IsOpen && shell.TopOverlay == shell.Intro && help.IsOpen;
        await Shot("h6-help-replay-intro");
        await _p.Press("ui_cancel");
        await _p.Frame(2);
        var backToHelp = !shell.Intro.IsOpen && shell.TopOverlay == help && shell.Current == View.Hall;
        await _p.Press("ui_cancel");
        await _p.Frame(2);
        _p.Check("FLOW_HELP_FOCUS_CHECK", trapped && replay && backToHelp && !help.IsOpen && shell.Title.IsOpen && _p.Focused() == origin && snap == Probe.Snapshot(shell.Session),
            $"focus piégé dans l'aide ({string.Join(" > ", ring)}), introduction rouverte volontairement par-dessus puis fermée, Échap ferme l'aide et rend le focus à « Aide et règles », session intacte");

        // Retour au jeu : « Reprendre » n'existe pas (session intacte) ; on referme le titre par « Nouvelle partie » -> intro -> « Passer ».
        await _p.TabTo(c => c is Button { Text: "Nouvelle partie" });
        await _p.Press("ui_accept");
        await _p.Frame(2);
        await _p.Press("ui_cancel");   // Échap = passer l'introduction
        await _p.Frame(3);
        _p.Check("FLOW_SKIP_INTRO_CHECK", !shell.AnyOverlayOpen && shell.Current == View.Hall && SameSession && snap == Probe.Snapshot(shell.Session),
            "Échap sur l'introduction = « Passer » : hall, aucune modale, session intacte");
    }

    // ---- Paramètres, confort, animations réduites -------------------------------------------------------------------------

    private async Task<bool> WaitFor(Func<bool> condition, int frames = 90)
    {
        for (var i = 0; i < frames && !condition(); i++) await _p.Frame();
        return condition();
    }

    private async Task SettingsAndComfort()
    {
        var set = shell.Settings;
        var view = shell.SettingsView;
        var win = shell.GetWindow();
        var snap = Probe.Snapshot(shell.Session);

        // Le « Menu » du HUD ramène au titre ; « Paramètres » s'ouvre au clavier ; la partie est intacte.
        Find(shell, "Menu")?.EmitSignal(BaseButton.SignalName.Pressed);
        await _p.Frame(3);
        await _p.TabTo(c => c is Button { Text: "Paramètres" });
        var origin = _p.Focused();
        await _p.Press("ui_accept");
        await _p.Frame(3);
        await Shot("p1-settings");
        _p.Check("FLOW_SETTINGS_OPEN_CHECK", view.IsOpen && shell.TopOverlay == view && _p.Focused() == view.FullscreenButton && !set.Fullscreen && !set.ReduceMotion
                                              && view.FullscreenButton.Text.EndsWith("NON") && view.MotionButton.Text.EndsWith("NON"),
            $"Paramètres ouverts depuis le titre, états écrits en toutes lettres (« {view.FullscreenButton.Text} » · « {view.MotionButton.Text} »)");

        // Rendu net : résolution logique 640×360, mise à l'échelle entière, filtre au plus proche.
        var filter = (int)ProjectSettings.GetSetting("rendering/textures/canvas_textures/default_texture_filter");
        var visible = shell.GetViewport().GetVisibleRect().Size;
        _p.Check("FLOW_RENDER_CHECK", filter == 0 && win.ContentScaleMode == Window.ContentScaleModeEnum.Viewport && win.ContentScaleStretch == Window.ContentScaleStretchEnum.Integer
                                       && win.ContentScaleSize == new Vector2I(640, 360) && visible == new Vector2(640, 360),
            $"filtre nearest ({filter}), mise à l'échelle {win.ContentScaleMode}/{win.ContentScaleStretch}, résolution logique {win.ContentScaleSize}, viewport {visible}");

        // Échelle entière : réellement appliquée à la fenêtre.
        var max = set.MaxScale;
        var windowed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed;
        var results = new List<string>();
        var scaleOk = windowed;
        if (max >= 2)
        {
            await _p.TabTo(c => c == view.SmallerButton);
            await _p.Press("ui_accept");
            scaleOk &= await WaitFor(() => win.Size == new Vector2I(640, 360));
            results.Add($"x1 -> {win.Size}");
            await _p.TabTo(c => c == view.LargerButton || c == view.FullscreenButton || c == view.MotionButton);
            view.LargerButton.EmitSignal(BaseButton.SignalName.Pressed);
            scaleOk &= await WaitFor(() => win.Size == new Vector2I(1280, 720));
            results.Add($"x2 -> {win.Size}");
        }
        scaleOk &= set.Scale == 2;
        _p.Check("FLOW_SCALE_CHECK", scaleOk && set.MaxScale == max, $"échelle entière appliquée à la fenêtre ({string.Join(", ", results)}) ; maximum sur cet écran : x{max}");

        // Plein écran : appliqué au système, échelle automatique (boutons d'échelle écartés), puis retour fenêtré à 1280×720.
        await _p.TabTo(c => c == view.FullscreenButton);
        await _p.Press("ui_accept");
        var full = await WaitFor(() => DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen);
        await _p.Frame(10);
        var noScale = view.SmallerButton.Disabled && view.LargerButton.Disabled && view.FullscreenButton.Text.EndsWith("OUI");
        var viewportInFull = shell.GetViewport().GetVisibleRect().Size == new Vector2(640, 360);
        await Shot("p2-settings-fullscreen");
        await _p.Press("ui_accept");
        var back = await WaitFor(() => DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed);
        back &= await WaitFor(() => win.Size == new Vector2I(1280, 720));
        _p.Check("FLOW_FULLSCREEN_CHECK", full && noScale && viewportInFull && back && !set.Fullscreen,
            $"plein écran appliqué ({full}), échelle automatique sans boutons actifs ({noScale}), viewport logique 640×360 ({viewportInFull}), retour fenêtré {win.Size} ({back})");

        // Réduire les animations : l'ambiance du hall est coupée immédiatement, puis rétablie.
        var hall = shell.GetNode<Control>("Content/Hall");
        var fx = hall.GetChildren().OfType<AmbientFx>().ToList();
        var f0 = fx.Select(f => ((AtlasTexture)f.Texture).Region.Position.X).ToList();
        await _p.Frame(40);
        var f1 = fx.Select(f => ((AtlasTexture)f.Texture).Region.Position.X).ToList();
        var animatedBefore = fx.Count == 2 && fx.All(f => f.Visible && f.IsProcessing()) && !f0.SequenceEqual(f1);
        await _p.TabTo(c => c == view.MotionButton);
        await _p.Press("ui_accept");
        await _p.Frame(2);
        var frozen = fx.All(f => !f.Visible && !f.IsProcessing()) && set.ReduceMotion && view.MotionButton.Text.EndsWith("OUI");
        await Shot("p3-settings-reduced");
        _p.Check("FLOW_REDUCE_MOTION_CHECK", animatedBefore && frozen,
            $"ambiance du hall animée avant ({animatedBefore}) ; après « Réduire les animations » : masquée et arrêtée immédiatement ({frozen}), état écrit « {view.MotionButton.Text} »");

        // Fondu d'ouverture : présent par défaut, supprimé en mode réduit.
        shell.OpenHelp();
        await _p.Frame(1);
        var fadeReduced = shell.Help.Fading;
        shell.Help.Close();
        set.SetReduceMotion(false);
        await _p.Frame(2);
        var ambientBack = fx.All(f => f.Visible && f.IsProcessing());
        shell.OpenHelp();
        await _p.Frame(1);
        var fadeNormal = shell.Help.Fading;
        shell.Help.Close();
        await _p.Frame(2);
        _p.Check("FLOW_FADE_CHECK", !fadeReduced && fadeNormal && ambientBack,
            $"fondu d'ouverture : absent en mode réduit ({!fadeReduced}), présent sinon ({fadeNormal}) ; ambiance rétablie en mode normal ({ambientBack})");

        // Préférences : fichier ConfigFile limité aux réglages d'affichage ; fichier absent, illisible ou aux valeurs absurdes = réglages par défaut.
        var path = $"{OS.GetUserDataDir()}/flowcheck-display.cfg";
        var a = new DisplaySettings(win, path);
        a.SetReduceMotion(true);
        var cfg = new ConfigFile();
        var readOk = cfg.Load(path) == Error.Ok;
        var sections = cfg.GetSections();
        var keys = readOk && sections.Length == 1 ? cfg.GetSectionKeys("display").Order().ToArray() : [];
        var b = new DisplaySettings(win, path);
        var loaded = b.Load() && b.ReduceMotion && !b.Fullscreen && b.Scale == 2;
        File.WriteAllText(path, "[display]\nfullscreen=\"oui\"\nscale=99\nreduce_motion=7\n");
        var c = new DisplaySettings(win, path);
        var absurd = c.Load() && !c.Fullscreen && !c.ReduceMotion && c.Scale == c.MaxScale;
        File.WriteAllText(path, "\u0001\u0002 pas un fichier de configuration {{{");
        var d = new DisplaySettings(win, path);
        d.Load();   // aucune exception, quel que soit le contenu
        var garbage = !d.Fullscreen && !d.ReduceMotion && d.Scale == 2;
        File.Delete(path);
        var missing = !new DisplaySettings(win, path).Load();
        _p.Check("FLOW_PREFS_CHECK", readOk && sections.SequenceEqual(["display"]) && keys.SequenceEqual(["fullscreen", "reduce_motion", "scale"]) && loaded && absurd && garbage && missing,
            $"ConfigFile : une seule section « display » ({string.Join(",", keys)}), relecture exacte ({loaded}), valeurs absurdes bornées ({absurd}), fichier illisible ({garbage}) ou absent ({missing}) = réglages par défaut, sans exception");

        // Fermer : Échap ramène au titre, focus restitué à « Paramètres », partie intacte.
        await _p.TabTo(c2 => c2 == view.FullscreenButton);
        shell.SettingsView.Close();
        await _p.Frame(2);
        // (le titre est toujours ouvert dessous)
        var titleStill = shell.Title.IsOpen && shell.TopOverlay == shell.Title;
        shell.OpenSettings();
        await _p.Frame(2);
        await _p.Press("ui_cancel");
        await _p.Frame(2);
        _p.Check("FLOW_SETTINGS_CLOSE_CHECK", titleStill && !view.IsOpen && shell.TopOverlay == shell.Title && _p.Focused() is Button { Text: "Paramètres" } && Probe.Snapshot(shell.Session) == snap && SameSession,
            "Échap ferme les paramètres, le titre reste dessous avec le focus sur « Paramètres », session intacte");
        await _p.TabTo(c2 => c2 is Button { Text: "Nouvelle partie" });
        await _p.Press("ui_accept");
        await _p.Frame(2);
        await _p.Press("ui_cancel");
        await _p.Frame(3);
    }

    // ---- Parties complètes ----------------------------------------------------------------------------------------------------

    /// <summary>Politique de référence « première offre × premier héros éligible » appliquée directement à une session Core (pour comparaison).</summary>
    private static GameSession CoreOnly(GameSession like, bool idle)
    {
        var s = GameSession.Start(like.Data, like.Seed);
        while (s.Status == GameStatus.InProgress)
        {
            if (!idle)
            {
                for (var n = 0; n < s.Data.Rules.MaxMissionsPerDay; n++)
                {
                    if (AutoPlay.Pick(s) is not { } pick) break;
                    s.Assign(pick.offer, pick.hero);
                }
                foreach (var q in s.Quests.Where(q => q.State == QuestState.Assigned).ToList()) s.Resolve(q.Id);
                foreach (var c in s.OpenComplaints.ToList()) s.ResolveComplaint(c.Id, AutoPlay.Choose(s, c));
            }
            s.EndDay();
        }
        return s;
    }

    private static string Journal(GameSession s) => string.Join("\n", s.Events.Select(e => $"#{e.Sequence} (J{e.Day}) {e.Message}"));

    private async Task PressEndDay()
    {
        shell.Show(View.Report);
        await _p.Frame(2);
        var end = Find(shell, "Terminer la journée");
        if (end is null) { _p.Check("FLOW_END_DAY_CHECK", false, "bouton « Terminer la journée » introuvable"); return; }
        end.EmitSignal(BaseButton.SignalName.Pressed);
        await _p.Frame(2);
    }

    /// <summary>Sept jours de gestion avec les vrais contrôles, depuis la nouvelle partie jusqu'au bilan royal, dont la victoire.</summary>
    private async Task VictoryRun()
    {
        var s = shell.Session;
        var set = shell.Settings;
        var played0 = CosmeticFx.Played.Count;
        var reducedFx = "non évalué";
        var liveCut = "non évalué";
        var missions = 0;
        var failures = 0;
        var endEarly = false;
        var days = 0;
        shell.Show(View.Hall);

        while (s.Status == GameStatus.InProgress)
        {
            days++;
            for (var n = 0; n < s.Data.Rules.MaxMissionsPerDay; n++)
            {
                if (AutoPlay.Pick(s) is not { } pick) break;
                shell.Show(View.Board); shell.SelectOffer(pick.offer);
                shell.Show(View.Roster); shell.SelectHero(pick.hero);
                shell.Assign();
            }
            foreach (var quest in s.Quests.Where(q => q.State == QuestState.Assigned).ToList())
            {
                shell.SelectQuest(quest.Id);
                shell.Show(View.Missions);
                await _p.Frame(2);
                if (missions == 0) set.SetReduceMotion(true);     // 1re mission : animations réduites
                if (missions == 1) set.SetReduceMotion(false);    // 2e : normales
                var before = CosmeticFx.Played.Count;
                var press = Find(shell, "Résoudre la mission");
                press?.EmitSignal(BaseButton.SignalName.Pressed);
                await _p.Frame(3);
                if (missions == 0) reducedFx = $"{CosmeticFx.Played.Count - before} effet lancé, {CosmeticFx.Active} actif";
                if (missions == 0) _p.Check("FLOW_REDUCE_FX_CHECK", press is not null && CosmeticFx.Played.Count == before && CosmeticFx.Active == 0 && shell.Outcomes.ContainsKey(quest.Id),
                    $"animations réduites : la mission est résolue (verdict en texte) mais {reducedFx}");
                if (missions == 1) _p.Check("FLOW_NORMAL_FX_CHECK", press is not null && CosmeticFx.Played.Count > before && CosmeticFx.Active > 0,
                    $"animations normales : {CosmeticFx.Played.Count - before} effet lancé, {CosmeticFx.Active} actif");
                if (missions == 2)
                {
                    // Réduction EN COURS d'effet : il s'arrête aussitôt, sans toucher au résultat déjà produit.
                    var active = CosmeticFx.Active > 0;
                    var outcome = shell.Outcomes[quest.Id];
                    set.SetReduceMotion(true);
                    await _p.Frame(3);
                    liveCut = $"effet en cours={active}, après réduction actifs={CosmeticFx.Active}";
                    _p.Check("FLOW_REDUCE_LIVE_CHECK", active && CosmeticFx.Active == 0 && shell.Outcomes[quest.Id] == outcome, $"réduction pendant un effet : {liveCut}, résultat inchangé");
                    set.SetReduceMotion(false);
                }
                missions++;
                if (!shell.Outcomes[quest.Id].Success) failures++;
                if (shell.End.IsOpen) endEarly = true;     // un échec de mission ordinaire ne doit JAMAIS ouvrir l'écran de défaite
                await _p.Frame(2);
            }
            foreach (var complaint in s.OpenComplaints.ToList())
            {
                shell.SelectComplaint(complaint.Id);
                shell.Show(View.Complaints);
                shell.ResolveComplaint(AutoPlay.Choose(s, complaint));
            }
            if (s.Guild.Day < s.Data.Rules.Days && shell.End.IsOpen) endEarly = true;
            await PressEndDay();
        }
        await _p.Frame(3);
        await Shot("e1-victory");

        // Comparaison avec la même politique appliquée directement à Core : l'interface, les modales et les animations ne changent rien.
        var reference = CoreOnly(s, idle: false);
        var identical = Probe.Snapshot(s) == Probe.Snapshot(reference) && Journal(s) == Journal(reference);
        var report = s.Report;
        _p.Check("FLOW_DAYS_CHECK", days == s.Data.Rules.Days && failures > 0 && !endEarly && s.Status == GameStatus.Finished && report is not null,
            $"{days} jours joués avec les vrais boutons, {missions} missions dont {failures} échecs : aucun écran de fin avant le bilan royal ({(endEarly ? "ÉCHEC" : "OK")})");
        _p.Check("FLOW_DETERMINISM_CHECK", identical && report is not null && reference.Report!.Success == report.Success,
            $"état final et journal ({s.Events.Count} événements) identiques à la même partie jouée directement dans Core (jour {s.Guild.Day}, trésorerie {s.Guild.TreasuryCopper}, réputation {s.Guild.Reputation}, missions réussies {s.Guild.MissionsSucceeded}), malgré bascules d'animations réduites");

        var end = shell.End;
        var texts = end.GetChildren().Count > 0 ? end.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text).ToList() : [];
        var realValues = report is not null && texts.Any(t => t.Contains($"Réputation : {report.Reputation}")) && texts.Any(t => t.Contains($"Missions réussies : {report.MissionsSucceeded}"));
        _p.Check("FLOW_END_VICTORY_CHECK", report is { Success: true } && end.IsOpen && end.ShowsVictory && ReferenceEquals(end.Report, report) && end.Picture.Texture == B53.Victory
                                            && _p.Focused() == end.DetailButton && realValues && !shell.Title.IsOpen,
            "victoire : écran affiché d'après FinalReport.Success, illustration de victoire, valeurs réelles du rapport, focus sur « Voir le bilan détaillé »");

        // « Voir le bilan détaillé » : même vue Report que d'habitude.
        await _p.Press("ui_accept");
        await _p.Frame(3);
        await Shot("e3-report-final");
        _p.Check("FLOW_END_DETAIL_CHECK", !end.IsOpen && shell.Current == View.Report && SameSession && s.Status == GameStatus.Finished,
            "« Voir le bilan détaillé » : écran de fin fermé, bilan et journal existants affichés, partie conservée");
    }

    /// <summary>Retour au titre, reprise du bilan, nouvelle partie confirmée (même graine), puis une partie perdue (défaite d'après FinalReport).</summary>
    private async Task NewGameAndDefeatRun()
    {
        var s = shell.Session;
        var finalSnap = Probe.Snapshot(s);

        // Retour au titre : la session n'est PAS réinitialisée ; « Reprendre » devient « Revoir le bilan ».
        Find(shell, "Menu")?.EmitSignal(BaseButton.SignalName.Pressed);
        await _p.Frame(3);
        var title = shell.Title;
        var resumeLabel = title.ResumeButton.Visible ? title.ResumeButton.Text : "(absent)";
        _p.Check("FLOW_RETURN_TITLE_CHECK", title.IsOpen && resumeLabel == "Revoir le bilan" && Probe.Snapshot(s) == finalSnap && SameSession,
            $"retour au titre sans réinitialiser : le menu propose « {resumeLabel} » (partie en mémoire), session identique");

        // « Nouvelle partie » demande confirmation ; Échap annule sans rien changer ; le focus initial est sur le choix sûr.
        await _p.TabTo(c => c is Button { Text: "Nouvelle partie" });
        await _p.Press("ui_accept");
        await _p.Frame(3);
        var confirm = shell.Confirm;
        var asked = confirm.IsOpen && _p.Focused() is Button { Text: "Annuler" } && Probe.Snapshot(s) == finalSnap;
        await Shot("e4-confirm-new-game");
        await _p.Press("ui_cancel");
        await _p.Frame(3);
        var cancelled = !confirm.IsOpen && title.IsOpen && Probe.Snapshot(s) == finalSnap && _p.Focused() is Button { Text: "Nouvelle partie" };
        _p.Check("FLOW_CONFIRM_CANCEL_CHECK", asked && cancelled, "« Nouvelle partie » sur une partie terminée : confirmation (focus sur « Annuler »), Échap annule, partie et focus intacts");

        // Même demande, confirmée : réinitialisation à l'état initial exact (même graine), introduction, puis le hall.
        await _p.Press("ui_accept");
        await _p.Frame(3);
        await _p.TabTo(c => c is Button { Text: "Nouvelle partie" } && confirm.IsAncestorOf(c));
        await _p.Press("ui_accept");
        await _p.Frame(4);
        var reset = shell.Intro.IsOpen && !confirm.IsOpen && !title.IsOpen && SameSession && Probe.Snapshot(s) == _freshSnapshot && s.Seed == _seed;
        _p.Check("FLOW_NEW_GAME_CHECK", reset, "nouvelle partie confirmée : introduction affichée, même instance de GameSession, état initial identique octet pour octet (même graine)");
        await _p.Press("ui_accept");   // « Commencer »
        await _p.Frame(3);

        // Une partie sans aucune mission : sept « Terminer la journée ». Défaite globale d'après FinalReport.
        var days = 0;
        while (s.Status == GameStatus.InProgress) { days++; await PressEndDay(); }
        await _p.Frame(3);
        var end = shell.End;
        var report = s.Report;
        await Shot("e2-defeat");
        var reference = CoreOnly(s, idle: true);
        _p.Check("FLOW_END_DEFEAT_CHECK", report is { Success: false } && end.IsOpen && !end.ShowsVictory && ReferenceEquals(end.Report, report) && end.Picture.Texture == B53.Defeat
                                           && days == s.Data.Rules.Days && Probe.Snapshot(s) == Probe.Snapshot(reference) && Journal(s) == Journal(reference),
            $"défaite : écran de défaite d'après FinalReport.Success=false, illustration de défaite, état identique à Core seul ({days} jours, réputation {s.Guild.Reputation}, trésorerie {s.Guild.TreasuryCopper})");

        // Défaite -> « Retour au titre » (session conservée) -> « Revoir le bilan » -> la vue Report, sans rouvrir l'écran de fin.
        var defeatSnap = Probe.Snapshot(s);
        end.TitleButton.EmitSignal(BaseButton.SignalName.Pressed);
        await _p.Frame(3);
        var toTitle = !end.IsOpen && title.IsOpen && Probe.Snapshot(s) == defeatSnap;
        title.ResumeButton.EmitSignal(BaseButton.SignalName.Pressed);
        await _p.Frame(3);
        _p.Check("FLOW_END_NAV_CHECK", toTitle && !title.IsOpen && !end.IsOpen && shell.Current == View.Report && Probe.Snapshot(s) == defeatSnap && SameSession,
            "défaite : « Retour au titre » conserve la session, « Revoir le bilan » ramène au bilan sans rouvrir l'écran de fin");

        // Retour à une partie neuve pour la suite (hall) : Nouvelle partie confirmée depuis l'écran de fin affiché à la main.
        end.Present(report!);
        await _p.Frame(3);
        end.NewGameButton.EmitSignal(BaseButton.SignalName.Pressed);
        await _p.Frame(3);
        var asksFromEnd = confirm.IsOpen && end.IsOpen;
        confirm.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text == "Nouvelle partie").EmitSignal(BaseButton.SignalName.Pressed);
        await _p.Frame(4);
        var intro = shell.Intro.IsOpen && !end.IsOpen && Probe.Snapshot(s) == _freshSnapshot;
        await _p.Press("ui_cancel");
        await _p.Frame(3);
        _p.Check("FLOW_END_NEW_GAME_CHECK", asksFromEnd && intro && shell.Current == View.Hall && !shell.AnyOverlayOpen && SameSession,
            "« Nouvelle partie » depuis l'écran de fin : confirmation, puis introduction ignorée et hall d'une partie neuve (même graine)");
    }

    // ---- Crédits ------------------------------------------------------------------------------------------------------------------

    private async Task CreditsScreenCheck()
    {
        var snap = Probe.Snapshot(shell.Session);
        var view = shell.Credits;
        Find(shell, "Menu")?.EmitSignal(BaseButton.SignalName.Pressed);
        await _p.Frame(3);
        await _p.TabTo(c => c is Button { Text: "Crédits" });
        var origin = _p.Focused();
        await _p.Press("ui_accept");
        await _p.Frame(3);
        var text = view.Text.GetParsedText();
        await Shot("c1-credits");
        var verified = new[] { "Sébastien Ribière", "Pixeloid Sans 1.0", "GGBotNet", "SIL Open Font License 1.1", "Godot Engine", ".NET", "silencieux" };
        var missing = verified.Where(v => !text.Contains(v)).ToList();
        var info = Engine.GetVersionInfo();
        var godotVersion = $"{info["major"]}.{info["minor"]}.{info["patch"]} {info["status"]}";
        _p.Check("FLOW_CREDITS_CHECK", view.IsOpen && shell.TopOverlay == view && missing.Count == 0 && text.Contains(godotVersion) && text.Contains(System.Environment.Version.ToString()),
            $"crédits ouverts depuis le titre : contributions vérifiables présentes ({verified.Length - missing.Count}/{verified.Length} {string.Join(",", missing)}), versions lues à l'exécution (Godot {godotVersion}, .NET {System.Environment.Version})");

        // Licences : texte OFL du dépôt + licence du moteur ; défilement au clavier.
        view.LicensesButton.EmitSignal(BaseButton.SignalName.Pressed);
        await _p.Frame(3);
        var licenses = view.Text.GetParsedText();
        var scrollBar = view.Text.GetVScrollBar();
        await _p.TabTo(c => c == view.Text);
        var before = scrollBar.Value;
        await _p.PressKey(Key.Pagedown);   // touche réelle : RichTextLabel ignore les actions synthétiques
        await _p.Frame(2);
        var scrolled = scrollBar.Value > before;
        await Shot("c2-credits-licenses");
        _p.Check("FLOW_LICENSES_CHECK", view.ShowingLicenses && licenses.Contains("SIL OPEN FONT LICENSE") && licenses.Contains("Permission is hereby granted") && licenses.Contains("(")
                                        && scrolled && _p.Focused() == view.Text,
            $"licences : texte OFL du dépôt et licence MIT du moteur, composants tiers listés ({licenses.Length} caractères), défilement au clavier ({before} -> {scrollBar.Value})");

        await _p.Press("ui_cancel");
        await _p.Frame(3);
        _p.Check("FLOW_CREDITS_CLOSE_CHECK", !view.IsOpen && shell.Title.IsOpen && _p.Focused() == origin && Probe.Snapshot(shell.Session) == snap && SameSession,
            "Échap ferme les crédits, le titre reste dessous, focus rendu au bouton « Crédits », session intacte");
        await _p.TabTo(c => c is Button { Text: "Nouvelle partie" });
        await _p.Press("ui_accept");
        await _p.Frame(2);
        await _p.Press("ui_cancel");
        await _p.Frame(3);
    }
}
