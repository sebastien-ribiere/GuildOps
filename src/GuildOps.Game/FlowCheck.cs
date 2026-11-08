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

    public async Task Run()
    {
        string? shots = null;
        foreach (var arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--shots=")) shots = arg["--shots=".Length..];
        if (shots is not null) DirAccess.MakeDirRecursiveAbsolute(shots);
        _p = new Probe(shell, shots);
        _session = shell.Session;
        _seed = _session.Seed;
        await _p.Frame(3);

        await TitleAndIntro();
        await HelpFromTitleAndGame();

        _p.Check("FLOW_SESSION_CHECK", ReferenceEquals(_session, shell.Session) && shell.Session.Seed == _seed && _seed == shell.Session.Data.Rules.TestSeed,
            $"une seule GameSession pendant tout le parcours, graine de référence {_seed} inchangée");
        GD.Print($"FLOW_RESULT failures={_p.Failures}");
        shell.GetTree().Quit(_p.Failures == 0 ? 0 : 1);
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
        await _p.ShotNow("t01-title");

        // Clavier : la boucle de focus reste dans le titre ; Échap ne ferme rien.
        var seen = new List<string>();
        for (var i = 0; i < labels.Count + 1; i++)
        {
            seen.Add((_p.Focused() as Button)?.Text ?? "?");
            await _p.Press("ui_focus_next");
        }
        await _p.Press("ui_cancel");
        await _p.ShotNow("t01b-title-focus");
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
        await _p.ShotNow("t02-intro");

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
        await _p.ShotNow("h5-help-ingame");
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
            await _p.ShotNow($"h{page + 1}-help-page{page + 1}");
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
        await _p.ShotNow("h6-help-replay-intro");
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
}
