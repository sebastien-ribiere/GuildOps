using Godot;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>Missions engagées/terminées, bouton Résoudre et verdict de Core. L'animation est cosmétique : aucun tirage supplémentaire.</summary>
public partial class QuestResult : PanelBase
{
    private VBoxContainer _list = null!;
    private VBoxContainer _detail = null!;
    private string? _animatedFor;

    protected override void Build()
    {
        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 6; root.OffsetRight = -6; root.OffsetTop = 4; root.OffsetBottom = -4;
        AddChild(root);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1 };
        root.AddChild(left);
        left.AddChild(UiKit.Lbl("MISSIONS", UiKit.Gold));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        left.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_list);

        var panel = UiKit.Panel("parchment", 8);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsStretchRatio = 1.4f;
        root.AddChild(panel);
        _detail = new VBoxContainer();
        panel.AddChild(_detail);
    }

    public override void FocusFirst() => (FirstButton(_list) ?? FirstButton(this))?.GrabFocus();

    public override void Refresh()
    {
        if (Shell is null) return;
        var s = Shell.Session;

        UiKit.Clear(_list);
        if (s.Quests.Count == 0) _list.AddChild(UiKit.Lbl("Aucune mission engagée.\nAffectez un héros à un contrat.", wrap: true));
        foreach (var quest in s.Quests)
        {
            var hero = s.Heroes.First(h => h.Id == quest.HeroId).Definition;
            var state = quest.Id == Shell.SelectedQuestId ? CardState.Selected : quest.State == QuestState.Assigned ? CardState.Normal : CardState.Dim;
            var card = new CardButton { CustomMinimumSize = new Vector2(0, 30), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            box.AddThemeConstantOverride("separation", 0);
            box.SetAnchorsPreset(LayoutPreset.FullRect);
            box.OffsetLeft = 6; box.OffsetRight = -6; box.OffsetTop = 3;
            card.AddChild(box);
            box.AddChild(card.Track(UiKit.Lbl(quest.Template.Title, clip: true)));
            box.AddChild(card.Track(UiKit.Lbl($"{hero.Name} — {StateText(quest.State)}", clip: true)));
            card.SetState(state);
            var id = quest.Id;
            card.Pressed += () => Shell.SelectQuest(id);
            _list.AddChild(card);
        }

        UiKit.Clear(_detail);
        var selected = Shell.SelectedQuest;
        if (selected is null) { _detail.AddChild(UiKit.Lbl("Sélectionnez une mission.", UiKit.Ink)); return; }

        var h = s.Heroes.First(x => x.Id == selected.HeroId).Definition;
        var head = new HBoxContainer();
        head.AddChild(Cards.PortraitWithFrame(h.Id));
        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddChild(UiKit.Lbl(selected.Template.Title, UiKit.Ink, wrap: true));
        info.AddChild(UiKit.Lbl($"{h.Name} — {h.Role}", UiKit.Ink, clip: true));
        info.AddChild(UiKit.Lbl($"{selected.Template.Category} · énergie {selected.Template.EnergyCost} · prime {s.Money(selected.Template.RewardCopper)}", UiKit.Ink, wrap: true));
        head.AddChild(info);
        _detail.AddChild(head);
        _detail.AddChild(UiKit.Lbl(selected.Template.Description, UiKit.Ink, wrap: true));

        if (selected.State == QuestState.Assigned)
        {
            _detail.AddChild(UiKit.Lbl("Mission en cours. Le tirage a lieu à la résolution.", UiKit.Ink, wrap: true));
            var button = UiKit.Btn("Résoudre la mission", Shell.Resolve);
            button.Disabled = s.Status == GameStatus.Finished;
            _detail.AddChild(button);
        }
        else if (Shell.Outcomes.TryGetValue(selected.Id, out var o))
        {
            var banner = BuildOutcome(o);
            _detail.AddChild(banner);
            if (_animatedFor != selected.Id)
            {
                _animatedFor = selected.Id;
                banner.PivotOffset = new Vector2(80, 10);
                banner.Modulate = new Color(1, 1, 1, 0);
                banner.Scale = new Vector2(1, 0.6f);
                var tween = CreateTween().SetParallel();
                tween.TweenProperty(banner, "modulate:a", 1.0, 0.25);
                tween.TweenProperty(banner, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back);
            }
            if (o.Incident is { } incident && s.Complaints.FirstOrDefault(c => c.Id == incident.ComplaintId) is { } complaint)
            {
                var cid = complaint.Id;
                _detail.AddChild(UiKit.Btn("Voir la réclamation →", () => { Shell.SelectComplaint(cid); Shell.Show(View.Complaints); }));
            }
        }
        else _detail.AddChild(UiKit.Lbl("Mission terminée.", UiKit.Ink));
    }

    private static string StateText(QuestState state) => state switch
    {
        QuestState.Assigned => "en cours", QuestState.Succeeded => "réussie", _ => "échouée",
    };

    private Control BuildOutcome(MissionOutcome o)
    {
        var s = Shell.Session;
        var box = new VBoxContainer();
        var head = UiKit.IconText(o.Success ? "success" : "failure", o.Success ? "SUCCÈS" : "ÉCHEC", UiKit.Good(true));
        if (!o.Success) ((Label)head.GetChild(1)).AddThemeColorOverride("font_color", UiKit.Bad(true));
        box.AddChild(head);
        box.AddChild(UiKit.Lbl($"Tirage {o.Roll} pour {o.ChancePercent} % de chance.", UiKit.Ink, wrap: true));
        box.AddChild(UiKit.IconText("coin", $"Prime versée : {s.Money(o.RewardPaidCopper)}", UiKit.Ink));
        box.AddChild(UiKit.IconText("energy", $"Énergie dépensée : {o.EnergySpent}", UiKit.Ink));
        box.AddChild(UiKit.IconText("reputation", $"Réputation {o.ReputationBefore} → {o.ReputationAfter} ({o.ReputationDelta:+#;-#;0})", UiKit.Ink));
        box.AddChild(UiKit.Lbl(o.ReputationReason, UiKit.Ink, wrap: true));
        if (o.Incident is { } incident)
        {
            box.AddChild(UiKit.IconText("warning", $"INCIDENT — {incident.Title}", UiKit.Bad(true)));
            box.AddChild(UiKit.Lbl($"{incident.Text} (dommage collatéral {incident.CollateralReputationDelta}). Une réclamation est ouverte.", UiKit.Ink, wrap: true));
        }
        return box;
    }
}
