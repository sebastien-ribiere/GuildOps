using Godot;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>
/// Réclamations ouvertes/closes/expirées. Chaque réponse annonce son coût et sa conséquence sur la réputation avant validation
/// (valeurs de <c>rules.Deltas</c> et <c>ComplaintTraitBonus</c>) ; la validité vient de <c>ComplaintCheck</c>, l'effet de <c>ResolveComplaint</c>.
/// </summary>
public partial class ComplaintDialog : PanelBase
{
    private Label _count = null!;
    private VBoxContainer _list = null!;
    private VBoxContainer _detail = null!;

    protected override void Build()
    {
        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 6; root.OffsetRight = -6; root.OffsetTop = 4; root.OffsetBottom = -4;
        root.AddThemeConstantOverride("separation", 4);
        AddChild(root);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(212, 0) };
        root.AddChild(left);
        _count = UiKit.Title("Réclamations clients");
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
    }

    public override void FocusFirst() => (FirstButton(_list) ?? FirstButton(_detail))?.GrabFocus();

    public override void Refresh()
    {
        if (Shell is null) return;
        var s = Shell.Session;

        var focused = GetViewport()?.GuiGetFocusOwner();
        var keepIndex = focused is not null && focused.GetParent() == _list ? focused.GetIndex() : -1;
        var detailHadFocus = focused is not null && _detail.IsAncestorOf(focused);

        _count.Text = s.Complaints.Count == 0 ? "Réclamations clients" : $"Réclamations ({s.OpenComplaints.Count} ouverte(s) / {s.Complaints.Count})";
        BuildList(s);
        BuildDetail(s);

        if (!Visible) return;
        if (keepIndex >= 0 && keepIndex < _list.GetChildCount()) _list.GetChild<Button>(keepIndex).GrabFocus();
        else if (detailHadFocus) (FirstButton(_detail) ?? FirstButton(_list))?.GrabFocus();
    }

    private void BuildList(GameSession s)
    {
        UiKit.Clear(_list);
        if (s.Complaints.Count == 0)
        {
            _list.AddChild(UiKit.Lbl("Aucune réclamation pour l'instant.", UiKit.Muted, wrap: true));
            return;
        }
        foreach (var c in s.Complaints)
        {
            var state = c.Id == Shell.SelectedComplaintId ? CardState.Selected : c.State == ComplaintState.Open ? CardState.Normal : CardState.Dim;
            var marker = c.State switch { ComplaintState.Open => "warning", ComplaintState.Resolved => "success", _ => "failure" };
            var sub = StateText(c.State) + (c.State == ComplaintState.Open ? $" · avant la fin du jour {c.ExpiresAfterDay}" : "");
            var card = Cards.ListRow(c.Storylet.Title, sub, state, marker);
            var id = c.Id;
            card.Pressed += () => Shell.SelectComplaint(id);
            _list.AddChild(card);
        }
    }

    private void BuildDetail(GameSession s)
    {
        UiKit.Clear(_detail);
        if (s.Complaints.Count == 0)
        {
            _detail.AddChild(UiKit.AsTitle(UiKit.Lbl("Aucune réclamation", UiKit.Ink)));
            _detail.AddChild(UiKit.Lbl("Les incidents survenus pendant les missions ouvrent un dossier ici. Aucun client mécontent pour le moment.", UiKit.Ink, wrap: true));
            return;
        }
        var complaint = Shell.SelectedComplaint;
        if (complaint is null)
        {
            _detail.AddChild(UiKit.AsTitle(UiKit.Lbl("Aucune réclamation choisie", UiKit.Ink)));
            _detail.AddChild(UiKit.Lbl("Sélectionnez un dossier dans la liste pour lire la plainte et choisir une réponse.", UiKit.Ink, wrap: true));
            return;
        }

        var open = complaint.State == ComplaintState.Open;
        _detail.AddChild(UiKit.IconText("warning", complaint.Storylet.Title, UiKit.Ink));
        var origin = s.Quests.FirstOrDefault(q => q.Id == complaint.QuestId);
        if (origin is not null)
        {
            var hero = s.Heroes.First(h => h.Id == origin.HeroId).Definition;
            _detail.AddChild(UiKit.Lbl($"Après « {origin.Template.Title} » ({hero.Name})", UiKit.MutedOnLight, wrap: true));
        }
        _detail.AddChild(UiKit.Lbl(complaint.Storylet.Text, UiKit.Ink, wrap: true));

        var status = new HBoxContainer();
        status.AddThemeConstantOverride("separation", 4);
        status.AddChild(UiKit.Pill(StateText(complaint.State), open ? new Color(0.60f, 0.38f, 0.04f) : complaint.State == ComplaintState.Resolved ? new Color(0.14f, 0.38f, 0.16f) : new Color(0.60f, 0.12f, 0.12f)));
        status.AddChild(UiKit.Lbl(open ? $"à traiter avant la fin du jour {complaint.ExpiresAfterDay}" : "dossier clos", UiKit.Ink));
        _detail.AddChild(status);

        if (open) BuildChoices(s, complaint);
        else if (complaint.Choice is { } chosen)
        {
            _detail.AddChild(UiKit.Lbl($"Réponse donnée : {ChoiceLabel(complaint, chosen)}.", UiKit.Ink, wrap: true));
            if (Shell.LastComplaintOutcome is { } o && o.ComplaintId == complaint.Id)
                _detail.AddChild(UiKit.IconText("reputation", $"{UiKit.Plain(o.Message)} Réputation {o.ReputationBefore} -> {o.ReputationAfter} ({o.ReputationDelta:+#;-#;0}).", UiKit.Ink, wrap: true));
        }
    }

    private void BuildChoices(GameSession s, Complaint complaint)
    {
        _detail.AddChild(UiKit.Divider(new Color(CardButton.ParchmentEdge, 0.6f)));
        _detail.AddChild(UiKit.Lbl($"Votre réponse (trésorerie : {s.Money(s.Guild.TreasuryCopper)})", UiKit.MutedOnLight));
        var reasons = new List<string>();
        foreach (var choice in Enum.GetValues<ComplaintChoice>())
        {
            var check = s.ComplaintCheck(complaint.Id, choice);
            foreach (var reason in check)
            {
                var text = RefusalMessages.For(reason);
                if (!reasons.Contains(text)) reasons.Add(text);
            }
            _detail.AddChild(ChoiceCard(s, complaint, choice, check.Count > 0));
        }
        foreach (var reason in reasons) _detail.AddChild(UiKit.IconText("warning", reason, UiKit.BadOnLight, wrap: true));
    }

    /// <summary>Carte de réponse : libellé, puis coût et effet sur la réputation annoncés avant validation. Désactivée si Core refuse.</summary>
    private CardButton ChoiceCard(GameSession s, Complaint complaint, ComplaintChoice choice, bool refused)
    {
        var deltas = s.Data.Rules.Deltas;
        var baseDelta = choice switch { ComplaintChoice.Repair => deltas.PaidCompensation, ComplaintChoice.Apologize => deltas.Apology, _ => deltas.DenyComplaint };
        var bonus = s.ComplaintTraitBonus(complaint.Id, choice);
        var cost = choice == ComplaintChoice.Repair ? complaint.RepairCostCopper : 0;

        var card = new CardButton { CustomMinimumSize = new Vector2(0, 46), SizeFlagsHorizontal = SizeFlags.ExpandFill, Disabled = refused };
        card.TooltipText = ChoiceLabel(complaint, choice);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = 6; box.OffsetRight = -6; box.OffsetTop = 3; box.OffsetBottom = -2;
        card.AddChild(box);

        var label = card.Track(UiKit.Lbl(ChoiceLabel(complaint, choice), wrap: true));
        label.MaxLinesVisible = 2;
        label.SizeFlagsVertical = SizeFlags.ExpandFill;
        box.AddChild(label);

        var effects = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        effects.AddThemeConstantOverride("separation", 10);
        effects.AddChild(Effect(card, "coin", cost == 0 ? "coût : aucun" : $"coût : {s.Money(cost)}"));
        var total = baseDelta + bonus;
        effects.AddChild(Effect(card, "reputation", $"réputation {total:+#;-#;0}" + (bonus != 0 ? $" (dont trait {bonus:+#;-#})" : "")));
        box.AddChild(effects);

        card.SetState(refused ? CardState.Dim : CardState.Normal, refused ? "failure" : null);
        var captured = choice;
        card.Pressed += () => Shell.ResolveComplaint(captured);
        return card;
    }

    private static Control Effect(CardButton card, string icon, string text)
    {
        var row = UiKit.IconText(icon, text);
        card.Track((Label)row.GetChild(1));
        return row;
    }

    private static string ChoiceLabel(Complaint c, ComplaintChoice choice) => choice switch
    {
        ComplaintChoice.Repair => c.Storylet.Labels.Repair,
        ComplaintChoice.Apologize => c.Storylet.Labels.Apologize,
        _ => c.Storylet.Labels.Deny,
    };

    private static string StateText(ComplaintState state) => state switch
    {
        ComplaintState.Open => "Ouverte", ComplaintState.Resolved => "Close", _ => "Expirée",
    };
}
