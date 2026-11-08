using Godot;
using GuildOps.Core.Engine;

namespace GuildOps.Game.Ui;

/// <summary>
/// Écran de fin de partie. Il n'apparaît que lorsque Core a produit un <see cref="FinalReport"/> (fin du dernier jour) ; l'issue — victoire ou
/// défaite globale — vient UNIQUEMENT de <see cref="FinalReport.Success"/> : aucune condition n'est recalculée ici, et un échec de mission
/// ordinaire n'y mène jamais. Les valeurs affichées sont celles du rapport (les objectifs viennent de <c>rules.Victory</c>, en lecture seule).
/// Aucun son. Échap = « Voir le bilan détaillé » (jamais une réinitialisation).
/// </summary>
public partial class EndScreen : Overlay
{
    private ColorRect _back = null!;
    private TextureRect _picture = null!;
    private Control _ribbonHost = null!;
    private Label _story = null!;
    private VBoxContainer _results = null!;
    private Button _detail = null!, _title = null!, _again = null!;
    private FinalReport? _report;

    public FinalReport? Report => _report;
    /// <summary>Vrai si l'écran affiché est celui de la victoire (déduit du rapport affiché).</summary>
    public bool ShowsVictory { get; private set; }
    public Button DetailButton => _detail;
    public Button TitleButton => _title;
    public Button NewGameButton => _again;
    public TextureRect Picture => _picture;

    protected override void Build()
    {
        _back = new ColorRect { Color = UiKit.Backdrop, MouseFilter = MouseFilterEnum.Ignore };
        _back.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_back);

        _picture = B53.Picture(B53.Victory);
        _picture.Position = new Vector2(10, 12);
        AddChild(_picture);
        _ribbonHost = new Control { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(106, 18) };
        AddChild(_ribbonHost);

        _story = UiKit.Lbl("", UiKit.Cream, wrap: true);
        _story.Position = new Vector2(10, 274);
        _story.CustomMinimumSize = new Vector2(384, 0);
        _story.Size = new Vector2(384, 60);
        AddChild(_story);

        var panel = UiKit.Panel("parchment", 8);
        panel.Position = new Vector2(402, 12);
        panel.CustomMinimumSize = new Vector2(228, 256);
        AddChild(panel);
        _results = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _results.AddThemeConstantOverride("separation", 4);
        panel.AddChild(_results);

        var buttons = new VBoxContainer { Position = new Vector2(402, 274) };
        buttons.AddThemeConstantOverride("separation", 2);
        buttons.CustomMinimumSize = new Vector2(228, 0);
        AddChild(buttons);
        _detail = IconButton("Voir le bilan détaillé", "trophy", ShowReport, 0, 22);
        _again = IconButton("Nouvelle partie", "play", () => Shell.RequestNewGame(), 0, 22);
        _title = IconButton("Retour au titre", "back", BackToTitle, 0, 22);
        buttons.AddChild(_detail);
        buttons.AddChild(_again);
        buttons.AddChild(_title);
        SetFocusRing([_detail, _again, _title]);
    }

    /// <summary>Affiche la conclusion de la partie d'après le rapport de Core (une seule fois par fin de partie).</summary>
    public void Present(FinalReport report)
    {
        _report = report;
        Open();
    }

    protected override void OnOpening()
    {
        if (_report is not { } r) return;
        var s = Shell.Session;
        var v = s.Data.Rules.Victory;
        ShowsVictory = r.Success;   // la seule source de la distinction : le rapport de Core

        _picture.Texture = r.Success ? B53.Victory : B53.Defeat;
        _back.Color = r.Success ? UiKit.Backdrop : new Color(0.07f, 0.04f, 0.09f);
        UiKit.Clear(_ribbonHost);
        _ribbonHost.AddChild(B53.RibbonWithText(r.Success ? "victory" : "defeat", r.Success ? "Guilde redressée !" : "Bilan inachevé"));
        _story.Text = r.Success
            ? "Le Conseil royal reconnaît votre travail : la guilde est redressée, et le hall retrouve ses couleurs."
            : "Le Conseil royal n'est pas convaincu, mais rien n'est perdu : revoyez vos choix, puis retentez votre chance.";

        UiKit.Clear(_results);
        _results.AddChild(UiKit.AsTitle(UiKit.Lbl(r.Success ? "Le Conseil royal est satisfait" : "Le Conseil royal accorde un sursis", UiKit.Ink, wrap: true)));
        _results.AddChild(Objective($"Réputation : {r.Reputation} (objectif {v.MinReputation})", r.ReputationOk));
        _results.AddChild(Objective($"Trésorerie : {s.Money(r.TreasuryCopper)} (objectif {s.Money(v.MinTreasuryCopper)})", r.TreasuryOk));
        _results.AddChild(Objective($"Missions réussies : {r.MissionsSucceeded} (objectif {v.MinSucceededMissions})", r.MissionsOk));
        _results.AddChild(UiKit.Divider(UiKit.MutedOnLight));
        var message = UiKit.Lbl(UiKit.Plain(r.Message), UiKit.MutedOnLight, wrap: true);
        message.SizeFlagsVertical = SizeFlags.ExpandFill;
        _results.AddChild(message);
    }

    private static Control Objective(string text, bool reached)
    {
        var row = UiKit.IconText(reached ? "success" : "failure", "", wrap: true);
        var label = (Label)row.GetChild(1);
        label.Text = $"{text} — {(reached ? "atteint" : "manqué")}";
        label.AddThemeColorOverride("font_color", reached ? UiKit.GoodOnLight : UiKit.BadOnLight);
        return row;
    }

    private void ShowReport()
    {
        Close();
        Shell.Show(View.Report);
    }

    private void BackToTitle()
    {
        Close();
        Shell.OpenTitle();   // la partie terminée reste en mémoire : « Revoir le bilan » / « Nouvelle partie »
    }

    protected override bool OnCancel()
    {
        ShowReport();
        return true;
    }

    protected override Control InitialFocus() => _detail;
}
