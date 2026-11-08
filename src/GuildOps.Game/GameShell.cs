using Godot;
using GuildOps.Core.Data;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

public enum View { Hall, Board, Roster, Missions, Complaints, Report }

/// <summary>
/// Composition root. Possède l'UNIQUE <see cref="GameSession"/> de la partie, l'état de sélection et la navigation.
/// Aucune règle métier ici : chaque commande est transmise à Core, qui reste seul juge (éligibilité, coûts, tirages).
/// Les panneaux sont des scènes affichées/masquées : la session et les sélections survivent à la navigation.
/// </summary>
public partial class GameShell : Control
{
    private const int HudHeight = 24;
    private const int ToastHeight = 24;

    public GameSession Session { get; private set; } = null!;

    public string? SelectedHeroId { get; private set; }
    public string? SelectedOfferId { get; private set; }
    public string? SelectedQuestId { get; private set; }
    public string? SelectedComplaintId { get; private set; }
    public View Current { get; private set; } = View.Hall;

    /// <summary>Résultats de mission affichés (cache d'affichage uniquement ; la vérité reste dans Core).</summary>
    public Dictionary<string, MissionOutcome> Outcomes { get; } = [];
    public ComplaintOutcome? LastComplaintOutcome { get; private set; }

    public event Action? Changed;

    private readonly Dictionary<View, PanelBase> _panels = [];
    private Label _toast = null!;
    private HBoxContainer _hud = null!;
    private Button _resetButton = null!;
    private bool _resetArmed;
    private double _resetTimer;
    private Label _dayLabel = null!, _moneyLabel = null!, _repLabel = null!, _missionsLabel = null!;
    private Button _homeButton = null!;
    private bool _toastOk = true;
    private string _toastText = "";

    public override void _Ready()
    {
        UiKit.Init();
        Theme = UiKit.Theme;
        SetAnchorsPreset(LayoutPreset.FullRect);

        var data = GameDataLoader.LoadEmbedded();
        Session = GameSession.Start(data, data.Rules.TestSeed);

        var content = new Control { Name = "Content" };
        content.SetAnchorsPreset(LayoutPreset.FullRect);
        content.OffsetTop = HudHeight;
        content.OffsetBottom = -ToastHeight;
        AddChild(content);

        AddPanel(content, View.Hall, "res://Scenes/GuildHall.tscn");
        AddPanel(content, View.Board, "res://Scenes/QuestBoard.tscn");
        AddPanel(content, View.Roster, "res://Scenes/HeroRoster.tscn");
        AddPanel(content, View.Missions, "res://Scenes/QuestResult.tscn");
        AddPanel(content, View.Complaints, "res://Scenes/ComplaintDialog.tscn");
        AddPanel(content, View.Report, "res://Scenes/DayReport.tscn");

        BuildHud();
        BuildToast();
        Show(View.Hall);
        Notify($"Bienvenue à la guilde. Jour 1 : {Session.CurrentOffers.Count} contrats vous attendent.");
        GD.Print($"GuildOps prêt : {Session.Heroes.Count} aventuriers, {Session.Offers.Count} offres, graine {Session.Seed}.");

        if (OS.GetCmdlineUserArgs().Contains("--autoplay")) _ = new AutoPlay(this).Run();
    }

    private void AddPanel(Control parent, View view, string scenePath)
    {
        var panel = GD.Load<PackedScene>(scenePath).Instantiate<PanelBase>();
        panel.Name = view.ToString();
        parent.AddChild(panel);
        panel.Bind(this);
        _panels[view] = panel;
    }

    // ---- HUD / toast -----------------------------------------------------------------------------------------

    private void BuildHud()
    {
        var bar = UiKit.Panel("dark", 3);
        bar.SetAnchorsPreset(LayoutPreset.TopWide);
        bar.CustomMinimumSize = new Vector2(0, HudHeight);
        bar.OffsetBottom = HudHeight;
        AddChild(bar);

        _hud = new HBoxContainer();
        _hud.AddThemeConstantOverride("separation", 8);
        bar.AddChild(_hud);

        _dayLabel = HudStat("day");
        _moneyLabel = HudStat("coin");
        _repLabel = HudStat("reputation");
        _missionsLabel = HudStat("scroll");
        _missionsLabel.GetParent<Control>().TooltipText = "Missions engagées aujourd'hui";
        _repLabel.GetParent<Control>().TooltipText = "Réputation";
        _moneyLabel.GetParent<Control>().TooltipText = "Trésorerie";
        _dayLabel.GetParent<Control>().TooltipText = "Jour en cours";

        _hud.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _homeButton = UiKit.Btn("Hall", () => Show(View.Hall), 44);
        _hud.AddChild(_homeButton);
        _resetButton = UiKit.Btn("Recommencer", OnResetPressed, 92);
        _hud.AddChild(_resetButton);
    }

    private Label HudStat(string icon)
    {
        var row = UiKit.IconText(icon, "");
        row.MouseFilter = MouseFilterEnum.Pass;
        _hud.AddChild(row);
        return (Label)row.GetChild(1);
    }

    private void BuildToast()
    {
        var bar = UiKit.Panel("dark", 3);
        bar.SetAnchorsPreset(LayoutPreset.BottomWide);
        bar.OffsetTop = -ToastHeight;
        bar.OffsetBottom = 0;
        bar.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bar);
        _toast = UiKit.Lbl("", wrap: false, clip: true);
        _toast.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(_toast);
    }

    public void Notify(string text, bool ok = true)
    {
        _toastText = text;
        _toastOk = ok;
        _toast.Text = text.ReplaceLineEndings(" ");
        _toast.TooltipText = text;
        _toast.AddThemeColorOverride("font_color", ok ? UiKit.Cream : UiKit.BadOnDark);
    }

    public string LastToast => _toastText;
    public bool LastToastOk => _toastOk;

    public override void _Process(double delta)
    {
        if (_resetArmed && (_resetTimer -= delta) <= 0) DisarmReset();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel") && Current != View.Hall)
        {
            Show(View.Hall);
            GetViewport().SetInputAsHandled();
        }
    }

    // ---- Navigation ------------------------------------------------------------------------------------------

    public void Show(View view)
    {
        Current = view;
        foreach (var (v, panel) in _panels) panel.Visible = v == view;
        _homeButton.Visible = view != View.Hall;
        RefreshAll();
        _panels[view].FocusFirst();
        if (GetViewport().GuiGetFocusOwner() is null && _homeButton.Visible) _homeButton.GrabFocus(); // vue vide : le clavier garde une prise
    }

    public void RefreshAll()
    {
        var g = Session.Guild;
        var rules = Session.Data.Rules;
        _dayLabel.Text = $"Jour {g.Day}/{rules.Days}";
        _moneyLabel.Text = Session.Money(g.TreasuryCopper);
        _repLabel.Text = $"{g.Reputation}/{rules.ReputationMax}";
        _repLabel.GetParent<Control>().TooltipText = $"Réputation ({Session.ReputationTierId})";
        _missionsLabel.Text = $"{g.MissionsAcceptedToday}/{rules.MaxMissionsPerDay}";
        foreach (var panel in _panels.Values) panel.Refresh();
        Changed?.Invoke();
    }

    // ---- Sélection -------------------------------------------------------------------------------------------

    public void SelectHero(string? id) { SelectedHeroId = id; RefreshAll(); }
    public void SelectOffer(string? id) { SelectedOfferId = id; RefreshAll(); }
    public void SelectQuest(string? id) { SelectedQuestId = id; RefreshAll(); }
    public void SelectComplaint(string? id) { SelectedComplaintId = id; RefreshAll(); }

    public Hero? SelectedHero => Session.Heroes.FirstOrDefault(h => h.Id == SelectedHeroId);
    public QuestOffer? SelectedOffer => Session.Offers.FirstOrDefault(o => o.Id == SelectedOfferId);
    public Quest? SelectedQuest => Session.Quests.FirstOrDefault(q => q.Id == SelectedQuestId);
    public Complaint? SelectedComplaint => Session.Complaints.FirstOrDefault(c => c.Id == SelectedComplaintId);

    // ---- Commandes : chacune délègue à Core et affiche son verdict -------------------------------------------

    public void Assign()
    {
        if (SelectedOfferId is null || SelectedHeroId is null) { Notify("Choisissez d'abord une quête et un héros.", false); return; }
        var result = Session.Assign(SelectedOfferId, SelectedHeroId);
        if (result.Ok)
        {
            SelectedQuestId = result.Value!.Id;
            SelectedOfferId = null; // évite qu'un double-clic ne rejoue la même commande
            Notify("Héros engagé. Résolvez la mission quand vous voulez (salle des missions).");
        }
        else Notify("Refusé : " + result.Message, false);
        RefreshAll();
    }

    public void Prepare()
    {
        var offer = SelectedOffer;
        if (offer is null || SelectedHeroId is null) { Notify("Choisissez un héros et une quête de la catégorie à préparer.", false); return; }
        var result = Session.Prepare(SelectedHeroId, offer.Template.Category);
        Notify(result.Ok ? result.Value!.Message : "Refusé : " + result.Message, result.Ok);
        RefreshAll();
    }

    public void Resolve()
    {
        if (SelectedQuestId is null) { Notify("Choisissez une mission en cours.", false); return; }
        var result = Session.Resolve(SelectedQuestId);
        if (!result.Ok) { Notify("Refusé : " + result.Message, false); RefreshAll(); return; }
        var o = result.Value!;
        Outcomes[o.QuestId] = o;
        if (o.Incident is { } incident) SelectedComplaintId = incident.ComplaintId;
        Notify($"{(o.Success ? "SUCCÈS" : "ÉCHEC")} — tirage {o.Roll} pour {o.ChancePercent} %. Réputation {o.ReputationBefore} → {o.ReputationAfter}." +
               (o.Incident is null ? "" : " Incident : une réclamation est ouverte."), o.Success);
        RefreshAll();
    }

    public void ResolveComplaint(ComplaintChoice choice)
    {
        if (SelectedComplaintId is null) { Notify("Choisissez une réclamation.", false); return; }
        var result = Session.ResolveComplaint(SelectedComplaintId, choice);
        if (result.Ok)
        {
            LastComplaintOutcome = result.Value;
            var v = result.Value!;
            Notify($"{v.Message} Réputation {v.ReputationBefore} → {v.ReputationAfter}.");
        }
        else Notify("Refusé : " + result.Message, false);
        RefreshAll();
    }

    public void Regenerate()
    {
        var result = Session.RegenerateOffers();
        if (!result.Ok) { Notify("Refusé : " + result.Message, false); RefreshAll(); return; }
        SelectedOfferId = null;
        var v = result.Value!;
        Notify($"Tableau renouvelé. Réputation {v.ReputationBefore} → {v.ReputationAfter}. Ce droit ne sert qu'une fois par partie.");
        RefreshAll();
    }

    public void EndDay()
    {
        var result = Session.EndDay();
        if (!result.Ok) { Notify("Refusé : " + result.Message, false); RefreshAll(); return; }
        SelectedOfferId = null;
        var t = result.Value!;
        Notify(t.Report is { } report
            ? report.Message
            : $"Jour {t.NewDay} : énergie récupérée, {t.NewOffers.Count} nouvelles offres.", t.Report?.Success ?? true);
        RefreshAll();
        if (t.Report is not null) Show(View.Report);
    }

    private void OnResetPressed()
    {
        if (!_resetArmed)
        {
            _resetArmed = true;
            _resetTimer = 4;
            _resetButton.Text = "Confirmer ?";
            return;
        }
        DisarmReset();
        Reset();
    }

    public void Reset()
    {
        Session.Reset();
        SelectedHeroId = SelectedOfferId = SelectedQuestId = SelectedComplaintId = null;
        Outcomes.Clear();
        LastComplaintOutcome = null;
        Notify("Partie réinitialisée (même graine).");
        Show(View.Hall);
    }

    private void DisarmReset()
    {
        _resetArmed = false;
        _resetButton.Text = "Recommencer";
    }
}
