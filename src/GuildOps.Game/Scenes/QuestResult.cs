using Godot;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>
/// Missions engagées/terminées : liste compacte à gauche, fiche détaillée à droite. Le verdict de Core (SUCCÈS/ÉCHEC) vient en premier,
/// suivi des conséquences (prime, énergie, réputation, incident). Le détail du tirage est repliable. Rien ici ne tire ni ne mute :
/// seul le bouton « Résoudre » appelle <c>GameSession.Resolve</c>, et un rafraîchissement ou l'ouverture des détails ne rejoue jamais rien.
/// </summary>
public partial class QuestResult : PanelBase
{
    private Label _count = null!;
    private VBoxContainer _list = null!;
    private VBoxContainer _detail = null!;
    private readonly HashSet<string> _bannerAnimated = [];
    private Control? _bannerHost;
    private readonly HashSet<string> _detailsOpen = [];
    private Button? _toggle;

    protected override void Build()
    {
        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 6; root.OffsetRight = -6; root.OffsetTop = 4; root.OffsetBottom = -4;
        root.AddThemeConstantOverride("separation", 4);
        AddChild(root);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(212, 0) };
        root.AddChild(left);
        _count = UiKit.Title("Missions");
        left.AddChild(_count);
        var scroll = UiKit.Scroll();
        left.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(_list);

        var panel = UiKit.Panel("parchment", 8);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(panel);
        var detailScroll = UiKit.Scroll();
        panel.AddChild(detailScroll);
        _detail = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _detail.AddThemeConstantOverride("separation", 4);
        detailScroll.AddChild(_detail);

        // Effets cosmétiques : uniquement sur résolution effective (événement de GameShell), jamais sur Refresh/navigation.
        Shell.MissionResolved += PlayResultFx;
    }

    /// <summary>
    /// Succès ou échec (verdict de Core), puis récompense si une prime a réellement été versée, puis incident s'il a réellement été généré.
    /// Purement visuel : aucune lecture de RNG, aucune mutation ; l'effet est posé à droite du bandeau de verdict, sans le masquer.
    /// </summary>
    private void PlayResultFx(MissionOutcome o)
    {
        if (!Visible || _bannerHost is not { } host || !IsInstanceValid(host) || Shell.SelectedQuestId != o.QuestId) return;
        var sequence = new List<FxKind> { o.Success ? FxKind.Succes : FxKind.Echec };
        if (o.RewardPaidCopper > 0) sequence.Add(FxKind.Recompense);
        if (o.Incident is not null) sequence.Add(FxKind.Incident);
        CosmeticFx.Attach(host, sequence);
    }

    public override void FocusFirst() => (FirstButton(_list) ?? FirstButton(_detail))?.GrabFocus();

    public override void Refresh()
    {
        if (Shell is null) return;
        var s = Shell.Session;

        // Le focus clavier survit au rafraîchissement : même ligne de liste, ou premier bouton du détail si c'était lui qui l'avait.
        var focused = GetViewport()?.GuiGetFocusOwner();
        var keepIndex = focused is not null && focused.GetParent() == _list ? focused.GetIndex() : -1;
        var detailHadFocus = focused is not null && _detail.IsAncestorOf(focused);
        var toggleHadFocus = focused is not null && focused == _toggle;

        _count.Text = s.Quests.Count == 0 ? "Missions" : $"Missions ({s.Quests.Count(q => q.State == QuestState.Assigned)} en cours / {s.Quests.Count})";
        BuildList(s);
        BuildDetail(s);

        if (!Visible) return;
        if (keepIndex >= 0 && keepIndex < _list.GetChildCount()) _list.GetChild<Button>(keepIndex).GrabFocus();
        else if (toggleHadFocus && _toggle is not null) _toggle.GrabFocus();
        else if (detailHadFocus) FirstButton(_detail)?.GrabFocus();
    }

    // ---- Liste ------------------------------------------------------------------------------------------------------

    private void BuildList(GameSession s)
    {
        UiKit.Clear(_list);
        if (s.Quests.Count == 0)
        {
            _list.AddChild(UiKit.Lbl("Aucune mission pour l'instant.", UiKit.Muted, wrap: true));
            return;
        }
        foreach (var quest in s.Quests)
        {
            var hero = s.Heroes.First(h => h.Id == quest.HeroId).Definition;
            var state = quest.Id == Shell.SelectedQuestId ? CardState.Selected : quest.State == QuestState.Assigned ? CardState.Normal : CardState.Dim;
            var marker = quest.State switch { QuestState.Assigned => "scroll", QuestState.Succeeded => "success", _ => "failure" };
            var card = Cards.ListRow(quest.Template.Title, hero.Name, state, marker);
            card.TooltipText += $"\n{StateText(quest.State)}";
            var id = quest.Id;
            card.Pressed += () => Shell.SelectQuest(id);
            _list.AddChild(card);
        }
    }

    private static string StateText(QuestState state) => state switch
    {
        QuestState.Assigned => "En cours", QuestState.Succeeded => "Réussie", _ => "Échouée",
    };

    // ---- Détail -------------------------------------------------------------------------------------------------------

    private void BuildDetail(GameSession s)
    {
        UiKit.Clear(_detail);
        _toggle = null;
        _bannerHost = null;

        if (s.Quests.Count == 0)
        {
            _detail.AddChild(UiKit.AsTitle(UiKit.Lbl("Aucune mission engagée", UiKit.Ink)));
            _detail.AddChild(UiKit.Lbl("Affectez un héros à un contrat : sa mission apparaîtra ici, prête à être résolue.", UiKit.Ink, wrap: true));
            _detail.AddChild(UiKit.Btn("Aller au tableau des contrats ->", () => Shell.Show(View.Board)));
            return;
        }
        var selected = Shell.SelectedQuest;
        if (selected is null)
        {
            _detail.AddChild(UiKit.AsTitle(UiKit.Lbl("Aucune mission choisie", UiKit.Ink)));
            _detail.AddChild(UiKit.Lbl("Sélectionnez une mission dans la liste pour la résoudre ou en lire le résultat.", UiKit.Ink, wrap: true));
            return;
        }

        var h = s.Heroes.First(x => x.Id == selected.HeroId).Definition;
        var t = selected.Template;

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 6);
        head.AddChild(Cards.PortraitWithFrame(h.Id));
        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 2);
        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 4);
        titleRow.AddChild(Cards.CategoryHeader(t.Category));
        var title = UiKit.AsTitle(UiKit.Lbl(t.Title, UiKit.Ink, wrap: true));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleRow.AddChild(title);
        info.AddChild(titleRow);
        info.AddChild(UiKit.AsTitle(UiKit.Lbl($"{h.Name} — {h.Role}", UiKit.Ink, wrap: true)));
        var stats = new HBoxContainer();
        stats.AddThemeConstantOverride("separation", 8);
        stats.AddChild(UiKit.IconText("energy", $"{t.EnergyCost}", UiKit.Ink));
        stats.AddChild(UiKit.IconText("coin", $"prime {s.Money(t.RewardCopper)}", UiKit.Ink));
        info.AddChild(stats);
        var needs = new HBoxContainer();
        needs.AddThemeConstantOverride("separation", 8);
        needs.AddChild(Cards.SkillLine(t.Skill, UiKit.Ink));
        needs.AddChild(Cards.LevelMarker(t.MinLevel, UiKit.Ink));
        info.AddChild(needs);
        head.AddChild(info);
        _detail.AddChild(head);

        // Illustration HD (192×128 = 384×256 ÷ 2) à droite de la description et du verdict ; clic ou Entrée : 384×256 à l'échelle 1.
        // Elle est neutre : jamais d'image de réussite ou d'échec avant la résolution. Les conséquences restent à gauche, en premier.
        var middle = new HBoxContainer();
        middle.AddThemeConstantOverride("separation", 6);
        _detail.AddChild(middle);
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 4);
        middle.AddChild(col);
        middle.AddChild(Cards.IllustrationButton(Shell, t, hdHalf: true));
        col.AddChild(UiKit.Lbl(t.Description, UiKit.MutedOnLight, wrap: true));

        if (selected.State == QuestState.Assigned) BuildPending(s, col);
        else if (Shell.Outcomes.TryGetValue(selected.Id, out var o)) BuildOutcome(s, selected, o, col);
        else col.AddChild(UiKit.Lbl("Mission terminée (résultat consigné au journal).", UiKit.Ink, wrap: true));
    }

    private void BuildPending(GameSession s, Control col)
    {
        col.AddChild(Banner("En cours", "scroll", new Color(0.60f, 0.38f, 0.04f), animate: false, size: UiKit.TextSize));
        col.AddChild(UiKit.Lbl("Le héros est parti. Le tirage a lieu à la résolution : aucun résultat n'est connu d'avance.", UiKit.Ink, wrap: true));
        var button = UiKit.Btn("Résoudre la mission", Shell.Resolve);
        button.Disabled = s.Status == GameStatus.Finished;
        col.AddChild(button);
    }

    private void BuildOutcome(GameSession s, Quest quest, MissionOutcome o, Control col)
    {
        // La pose en fondu du bandeau n'a lieu qu'à la première présentation de ce résultat (jamais à la réouverture de la fiche).
        var banner = Banner(o.Success ? "SUCCÈS" : "ÉCHEC", o.Success ? "success" : "failure",
            o.Success ? new Color(0.14f, 0.38f, 0.16f) : new Color(0.60f, 0.12f, 0.12f), animate: _bannerAnimated.Add(quest.Id));
        _bannerHost = banner;
        col.AddChild(banner);

        // Conséquences : récompenses et pertes en premier.
        var grid = new GridContainer { Columns = 1 };
        grid.AddThemeConstantOverride("v_separation", 2);
        grid.AddChild(UiKit.IconText("coin", $"Prime versée : {s.Money(o.RewardPaidCopper)}", UiKit.Ink));
        grid.AddChild(UiKit.IconText("energy", $"Énergie dépensée : {o.EnergySpent}", UiKit.Ink));
        var rep = UiKit.IconText("reputation", $"Réputation {o.ReputationBefore} -> {o.ReputationAfter} ({o.ReputationDelta:+#;-#;0})",
            o.ReputationDelta > 0 ? UiKit.GoodOnLight : o.ReputationDelta < 0 ? UiKit.BadOnLight : UiKit.Ink);
        grid.AddChild(rep);
        col.AddChild(grid);
        // Le motif de réputation (texte de Core) passe sur toute la largeur, sous l'illustration.
        _detail.AddChild(UiKit.Lbl(o.ReputationReason, UiKit.MutedOnLight, wrap: true));

        if (o.Incident is { } incident)
        {
            var box = new PanelContainer();
            box.AddThemeStyleboxOverride("panel", UiKit.Flat(new Color(0.96f, 0.80f, 0.70f), UiKit.BadOnLight, 2, 3));
            var inner = new VBoxContainer();
            inner.AddChild(UiKit.IconText("warning", $"Incident — {incident.Title}", UiKit.BadOnLight));
            inner.AddChild(UiKit.Lbl($"{incident.Text} (dommage collatéral {incident.CollateralReputationDelta}).", UiKit.Ink, wrap: true));
            box.AddChild(inner);
            _detail.AddChild(box);
            if (s.Complaints.FirstOrDefault(c => c.Id == incident.ComplaintId) is { } complaint)
            {
                var cid = complaint.Id;
                _detail.AddChild(UiKit.Btn("Voir la réclamation ->", () => { Shell.SelectComplaint(cid); Shell.Show(View.Complaints); }));
            }
        }

        // Détails du calcul : repliés par défaut, ouverts à la demande (clavier compris). Aucun nouveau tirage.
        var open = _detailsOpen.Contains(quest.Id);
        var qid = quest.Id;
        _toggle = UiKit.Btn(open ? "Détails du calcul : masquer" : "Détails du calcul : afficher", () =>
        {
            if (!_detailsOpen.Remove(qid)) _detailsOpen.Add(qid);
            Refresh();
        });
        _detail.AddChild(_toggle);
        if (open)
        {
            var box = new PanelContainer();
            box.AddThemeStyleboxOverride("panel", UiKit.Flat(new Color(0.84f, 0.74f, 0.52f), CardButton.ParchmentEdge, 1, 4));
            var inner = new VBoxContainer();
            inner.AddChild(UiKit.Lbl($"Tirage : {o.Roll} — probabilité de réussite : {o.ChancePercent} %.", UiKit.Ink, wrap: true));
            inner.AddChild(UiKit.Lbl($"Verdict de Core : {(o.Success ? "succès" : "échec")}. Engagement payé : {s.Money(quest.EngagementPaidCopper)}.", UiKit.Ink, wrap: true));
            inner.AddChild(UiKit.Lbl("Ces valeurs sont celles consignées lors de la résolution ; les consulter ne tire rien de nouveau.", UiKit.MutedOnLight, wrap: true));
            box.AddChild(inner);
            _detail.AddChild(box);
        }
    }

    /// <summary>Bandeau de verdict : fond plein, icône et mot en grand. Animation purement cosmétique (opacité/échelle).</summary>
    private Control Banner(string text, string icon, Color fill, bool animate, int size = UiKit.VerdictSize)
    {
        var banner = new PanelContainer();
        banner.SetAnchorsPreset(LayoutPreset.FullRect);
        banner.AddThemeStyleboxOverride("panel", UiKit.Flat(fill, UiKit.Ink, 2, 3));
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(UiKit.Icon(icon, 1.5f));
        var label = UiKit.AsTitle(UiKit.Lbl(text, UiKit.Cream), size);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        banner.AddChild(row);
        if (animate)
        {
            banner.Resized += () => banner.PivotOffset = banner.Size / 2;
            banner.Modulate = new Color(1, 1, 1, 0);
            banner.Scale = new Vector2(1, 0.6f);
            var tween = banner.CreateTween().SetParallel();
            tween.TweenProperty(banner, "modulate:a", 1.0, 0.25);
            tween.TweenProperty(banner, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back);
        }
        var host = new Control { CustomMinimumSize = new Vector2(0, 30), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        host.AddChild(banner);
        return host;
    }
}
