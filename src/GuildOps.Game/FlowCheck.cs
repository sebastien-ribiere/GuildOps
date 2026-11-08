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
}
