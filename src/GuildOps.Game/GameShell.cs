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

    /// <summary>Préférences d'affichage et de confort (hors Core, hors partie).</summary>
    public DisplaySettings Settings { get; private set; } = null!;

    public string? SelectedHeroId { get; private set; }
    public string? SelectedOfferId { get; private set; }
    public string? SelectedQuestId { get; private set; }
    public string? SelectedComplaintId { get; private set; }
    public View Current { get; private set; } = View.Hall;

    /// <summary>Résultats de mission affichés (cache d'affichage uniquement ; la vérité reste dans Core).</summary>
    public Dictionary<string, MissionOutcome> Outcomes { get; } = [];
    public ComplaintOutcome? LastComplaintOutcome { get; private set; }

    public event Action? Changed;

    /// <summary>
    /// Levé UNE fois par résolution effective acceptée par Core (jamais par un rafraîchissement ni une navigation), après mise à jour des
    /// vues. Sert uniquement aux effets cosmétiques : les abonnés ne doivent ni muter la session ni tirer.
    /// </summary>
    public event Action<MissionOutcome>? MissionResolved;

    private IllustrationViewer _viewer = null!;

    // Écrans superposés (B5.3). Ils ne possèdent aucune session : tout passe par les commandes de cette classe.
    private TitleScreen _title = null!;
    private IntroScreen _intro = null!;
    private ConfirmDialog _confirm = null!;
    private HelpScreen _help = null!;
    private SettingsScreen _settingsScreen = null!;
    private EndScreen _end = null!;
    private CreditsScreen _credits = null!;
    private readonly List<Overlay> _overlays = [];
    private int _pristineEvents;

    /// <summary>Lancement automatisé (<c>--autoplay</c>) : ni titre, ni introduction, ni écran de fin imposé (aucune modale ne doit bloquer les tests).</summary>
    public bool Automated { get; private set; }

    /// <summary>Action « quitter l'application » (remplaçable par les contrôles automatisés, qui ne doivent pas fermer le jeu).</summary>
    public Action QuitAction { get; set; } = () => { };

    private readonly Dictionary<View, PanelBase> _panels = [];
    private Label _toast = null!;
    private HBoxContainer _hud = null!;
    private Button _resetButton = null!;
    private bool _resetArmed;
    private double _resetTimer;
    private Label _dayLabel = null!, _moneyLabel = null!, _repLabel = null!, _missionsLabel = null!;
    private Button _homeButton = null!, _menuButton = null!, _helpButton = null!;
    private bool _toastOk = true;
    private string _toastText = "";

    public override void _Ready()
    {
        UiKit.Init();
        Theme = UiKit.Theme;
        SetAnchorsPreset(LayoutPreset.FullRect);

        Automated = OS.GetCmdlineUserArgs().Contains("--autoplay");
        QuitAction = () => GetTree().Quit();

        // Préférences : lues et appliquées seulement pour un joueur (jamais en lancement automatisé, qui doit rester déterministe et sans fichier).
        var flowCheck = OS.GetCmdlineUserArgs().Contains("--flowcheck");
        Settings = new DisplaySettings(GetWindow(), Automated || flowCheck ? null : "user://display.cfg");
        if (Settings.Load()) Settings.ApplyWindow();

        var data = GameDataLoader.LoadEmbedded();
        Session = GameSession.Start(data, data.Rules.TestSeed);   // l'UNIQUE session de la partie ; la graine de référence ne change jamais
        _pristineEvents = Session.Events.Count;

        var backdrop = new ColorRect { Name = "Backdrop", Color = UiKit.Backdrop, MouseFilter = MouseFilterEnum.Ignore };
        backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

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
        _viewer = new IllustrationViewer();
        AddChild(_viewer);
        BuildOverlays();
        Show(View.Hall);
        Notify($"Bienvenue à la guilde. Jour 1 : {Session.CurrentOffers.Count} contrats vous attendent.");
        GD.Print($"GuildOps prêt : {Session.Heroes.Count} aventuriers, {Session.Offers.Count} offres, graine {Session.Seed}.");

        if (Automated) _ = new AutoPlay(this).Run();
        else
        {
            _title.Open();   // démarrage normal : le titre d'abord ; le hall est déjà prêt dessous (même session)
            if (flowCheck) _ = new FlowCheck(this).Run();
        }
    }

    private T AddOverlay<T>(string name) where T : Overlay, new()
    {
        var overlay = new T();
        AddChild(overlay);
        overlay.Bind(this, name);
        return overlay;
    }

    /// <summary>Ordre d'empilement = ordre de création : la confirmation passe au-dessus de tout, le titre au-dessus du jeu.</summary>
    private void BuildOverlays()
    {
        _title = AddOverlay<TitleScreen>("TitleScreen");
        _intro = AddOverlay<IntroScreen>("IntroScreen");
        _help = AddOverlay<HelpScreen>("HelpScreen");
        _settingsScreen = AddOverlay<SettingsScreen>("SettingsScreen");
        _credits = AddOverlay<CreditsScreen>("CreditsScreen");
        _end = AddOverlay<EndScreen>("EndScreen");
        _confirm = AddOverlay<ConfirmDialog>("ConfirmDialog");
    }

    // ---- Pile d'écrans superposés ----------------------------------------------------------------------------------

    public Overlay? TopOverlay => _overlays.Count > 0 ? _overlays[^1] : null;
    public bool AnyOverlayOpen => _overlays.Count > 0;
    public TitleScreen Title => _title;
    public IntroScreen Intro => _intro;
    public ConfirmDialog Confirm => _confirm;
    public HelpScreen Help => _help;
    public SettingsScreen SettingsView => _settingsScreen;
    public EndScreen End => _end;
    public CreditsScreen Credits => _credits;

    public void PushOverlay(Overlay overlay) => _overlays.Add(overlay);
    public void PopOverlay(Overlay overlay) => _overlays.Remove(overlay);

    // ---- Parcours : titre, nouvelle partie, reprise ----------------------------------------------------------------

    /// <summary>
    /// Vrai dès que la partie en mémoire n'est plus à son état initial (un événement de plus dans le journal, un jour passé, partie terminée).
    /// Déduit uniquement de l'état de Core ; décide s'il faut confirmer avant d'écraser la partie.
    /// </summary>
    public bool HasProgress => Session.Status == GameStatus.Finished || Session.Guild.Day > 1 || Session.Events.Count != _pristineEvents;

    public void OpenTitle() => _title.Open();

    /// <summary>Aide en quatre pages, consultable à tout moment (titre, HUD, F1). Ne modifie jamais la session.</summary>
    public void OpenHelp() => _help.Open();

    /// <summary>Paramètres d'affichage et de confort (depuis le titre ; en jeu, via « Menu »).</summary>
    public void OpenSettings() => _settingsScreen.Open();

    /// <summary>Crédits et licences.</summary>
    public void OpenCredits() => _credits.Open();

    /// <summary>« Reprendre » : retour à la partie en mémoire, dans la vue où on l'avait laissée. Aucune écriture dans Core.</summary>
    public void ResumeGame()
    {
        _title.Close();
        Show(Session.Status == GameStatus.Finished ? View.Report : Current);
    }

    /// <summary>« Nouvelle partie » : confirmation d'abord si une partie est en cours, puis réinitialisation (même graine) et introduction.</summary>
    public void RequestNewGame()
    {
        if (!HasProgress) { BeginNewGame(); return; }
        var finished = Session.Status == GameStatus.Finished;
        _confirm.Ask("Commencer une nouvelle partie ?",
            finished ? "La partie terminée (journal et bilan) sera effacée. La nouvelle partie repart de la même graine de référence."
                     : $"La partie en cours (jour {Session.Guild.Day}/{Session.Data.Rules.Days}) sera abandonnée : elle n'est pas sauvegardée. La nouvelle partie repart de la même graine de référence.",
            "Nouvelle partie", BeginNewGame, "Annuler");
    }

    private void BeginNewGame()
    {
        ResetState();   // même graine ; sans effet visible si la partie est encore à son état initial
        CloseAllOverlays();
        _intro.OpenNewGame();
    }

    /// <summary>Fin de l'introduction (ou de son report) : on entre dans le hall, la partie commence.</summary>
    public void BeginPlaying()
    {
        Notify($"Bienvenue à la guilde. Jour {Session.Guild.Day} : {Session.CurrentOffers.Count} contrats vous attendent.");
        Show(View.Hall);
    }

    public void RequestQuit()
    {
        if (HasProgress && Session.Status == GameStatus.InProgress)
            _confirm.Ask("Quitter GuildOps ?", "La partie en cours n'est pas sauvegardée : elle sera perdue.", "Quitter", QuitAction, "Rester");
        else QuitAction();
    }

    private void CloseAllOverlays()
    {
        foreach (var overlay in _overlays.ToList()) overlay.Close();
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
        _homeButton = UiKit.Btn("Hall", () => Show(View.Hall), 44, 18);
        _hud.AddChild(_homeButton);
        // En lancement automatisé, aucune modale ne doit pouvoir s'ouvrir (même par un focus clavier mal placé ou un clic perdu) :
        // « Aide » et « Menu » n'existent donc pas, et F1 est ignorée. Le HUD est alors identique à celui de B5.2.
        _helpButton = UiKit.Btn("Aide", OpenHelp, 36, 18);
        _helpButton.TooltipText = "Aide et règles (F1)";
        _helpButton.Visible = !Automated;
        _hud.AddChild(_helpButton);
        _menuButton = UiKit.Btn("Menu", OpenTitle, 44, 18);
        _menuButton.TooltipText = "Retour au titre (la partie reste en mémoire)";
        _menuButton.Visible = !Automated;
        _hud.AddChild(_menuButton);
        _resetButton = UiKit.Btn("Recommencer", OnResetPressed, 100, 18);
        _hud.AddChild(_resetButton);
    }

    private Label HudStat(string icon)
    {
        var row = UiKit.IconText(icon, "");
        row.MouseFilter = MouseFilterEnum.Pass;
        _hud.AddChild(row);
        return UiKit.AsTitle((Label)row.GetChild(1)); // HUD : Bold 9 px
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
        _toast.Text = UiKit.Plain(text).ReplaceLineEndings(" ");
        _toast.TooltipText = UiKit.Plain(text);
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
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 } && !Automated && TopOverlay is not (HelpScreen or ConfirmDialog))
        {
            OpenHelp();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event.IsActionPressed("ui_cancel") && Current != View.Hall && !AnyOverlayOpen)
        {
            Show(View.Hall);
            GetViewport().SetInputAsHandled();
        }
    }

    // ---- Navigation ------------------------------------------------------------------------------------------

    /// <summary>Ouvre l'illustration HD d'un contrat (affichage seul).</summary>
    public void OpenIllustration(QuestTemplate template) => _viewer.Open(template);
    public bool IllustrationOpen => _viewer.IsOpen;
    public void CloseIllustration() => _viewer.Close();

    public void Show(View view)
    {
        _viewer.Close();
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
        Notify($"{(o.Success ? "SUCCÈS" : "ÉCHEC")} — tirage {o.Roll} pour {o.ChancePercent} %. Réputation {o.ReputationBefore} -> {o.ReputationAfter}." +
               (o.Incident is null ? "" : " Incident : une réclamation est ouverte."), o.Success);
        RefreshAll();
        MissionResolved?.Invoke(o);
    }

    public void ResolveComplaint(ComplaintChoice choice)
    {
        if (SelectedComplaintId is null) { Notify("Choisissez une réclamation.", false); return; }
        var result = Session.ResolveComplaint(SelectedComplaintId, choice);
        if (result.Ok)
        {
            LastComplaintOutcome = result.Value;
            var v = result.Value!;
            Notify($"{v.Message} Réputation {v.ReputationBefore} -> {v.ReputationAfter}.");
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
        Notify($"Tableau renouvelé. Réputation {v.ReputationBefore} -> {v.ReputationAfter}. Ce droit ne sert qu'une fois par partie.");
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
        if (t.Report is { } final)
        {
            Show(View.Report);
            // Conclusion de la partie : seulement quand Core rend son rapport final, et jamais en lancement automatisé (aucune modale ne bloque les tests).
            if (!Automated) _end.Present(final);
        }
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

    /// <summary>« Recommencer » (HUD, bilan) : même graine, retour au hall, sans introduction.</summary>
    public void Reset()
    {
        ResetState();
        Notify("Partie réinitialisée (même graine).");
        Show(View.Hall);
    }

    private void ResetState()
    {
        Session.Reset();
        _pristineEvents = Session.Events.Count;
        SelectedHeroId = SelectedOfferId = SelectedQuestId = SelectedComplaintId = null;
        Outcomes.Clear();
        LastComplaintOutcome = null;
    }

    private void DisarmReset()
    {
        _resetArmed = false;
        _resetButton.Text = "Recommencer";
    }
}
