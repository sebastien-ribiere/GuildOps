using Godot;
using GuildOps.Core.Data;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;

namespace GuildOps.Game;

/// <summary>
/// Écran unique de la tranche verticale. Cette scène ne contient AUCUNE règle métier : elle affiche l'état de
/// GameSession et transmet les commandes du joueur. Admissibilité, probabilité, coûts : tout vient de Core.
/// </summary>
public partial class Main : Control
{
    private GameSession _session = null!;
    private string? _selectedHeroId;
    private string? _selectedOfferId;
    private string? _selectedQuestId;

    private Label _status = null!;
    private ItemList _heroList = null!;
    private ItemList _offerList = null!;
    private ItemList _questList = null!;
    private RichTextLabel _detail = null!;
    private RichTextLabel _journal = null!;
    private Button _assignButton = null!;
    private Button _resolveButton = null!;
    private Button _endDayButton = null!;
    private Button _regenButton = null!;
    private ItemList _complaintList = null!;
    private readonly Button[] _complaintButtons = new Button[3];
    private string? _selectedComplaintId;
    private Label _feedback = null!;

    public override void _Ready()
    {
        var data = GameDataLoader.LoadEmbedded();
        _session = GameSession.Start(data, data.Rules.TestSeed);
        BuildUi();
        Refresh();
        GD.Print($"GuildOps prêt : {_session.Heroes.Count} aventuriers, {_session.Offers.Count} offres, graine {_session.Seed}.");
    }

    private void BuildUi()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 12);
        AddChild(margin);

        var root = new VBoxContainer();
        margin.AddChild(root);

        var top = new HBoxContainer();
        root.AddChild(top);
        _status = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        top.AddChild(_status);
        var reset = new Button { Text = "Recommencer (même graine)" };
        reset.Pressed += () => { _session.Reset(); _selectedHeroId = _selectedOfferId = _selectedQuestId = null; _feedback.Text = "Partie réinitialisée."; Refresh(); };
        top.AddChild(reset);

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(columns);

        columns.AddChild(Column("Les douze aventuriers", out _heroList, 1.3f));
        _heroList.ItemSelected += i => { _selectedHeroId = _session.Heroes[(int)i].Id; Refresh(); };

        var middle = Column("Tableau des contrats", out _offerList, 1.0f);
        columns.AddChild(middle);
        _offerList.ItemSelected += i => { _selectedOfferId = _session.CurrentOffers[(int)i].Id; Refresh(); };

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.2f };
        columns.AddChild(right);
        right.AddChild(new Label { Text = "Détail de l'affectation" });
        _detail = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddChild(_detail);
        _assignButton = new Button { Text = "Affecter le héros à la quête" };
        _assignButton.Pressed += OnAssign;
        right.AddChild(_assignButton);

        right.AddChild(new Label { Text = "Missions en cours / terminées" });
        _questList = new ItemList { CustomMinimumSize = new Vector2(0, 110) };
        _questList.ItemSelected += i => { _selectedQuestId = _session.Quests[(int)i].Id; Refresh(); };
        right.AddChild(_questList);
        _resolveButton = new Button { Text = "Résoudre la mission sélectionnée" };
        _resolveButton.Pressed += OnResolve;
        right.AddChild(_resolveButton);

        right.AddChild(new Label { Text = "Réclamations clients" });
        _complaintList = new ItemList { CustomMinimumSize = new Vector2(0, 80) };
        _complaintList.ItemSelected += i => { _selectedComplaintId = _session.Complaints[(int)i].Id; Refresh(); };
        right.AddChild(_complaintList);
        var complaintRow = new HBoxContainer();
        right.AddChild(complaintRow);
        foreach (var choice in Enum.GetValues<ComplaintChoice>())
        {
            var button = new Button { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var captured = choice;
            button.Pressed += () => OnComplaint(captured);
            _complaintButtons[(int)choice] = button;
            complaintRow.AddChild(button);
        }

        _regenButton = new Button();
        _regenButton.Pressed += OnRegenerate;
        right.AddChild(_regenButton);

        _endDayButton = new Button { Text = "Terminer la journée" };
        _endDayButton.Pressed += OnEndDay;
        right.AddChild(_endDayButton);

        _feedback = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_feedback);
        _journal = new RichTextLabel { CustomMinimumSize = new Vector2(0, 140), ScrollFollowing = true };
        root.AddChild(_journal);
    }

    private static VBoxContainer Column(string title, out ItemList list, float ratio)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = ratio };
        box.AddChild(new Label { Text = title });
        list = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        box.AddChild(list);
        return box;
    }

    private void OnAssign()
    {
        if (_selectedOfferId is null || _selectedHeroId is null) { _feedback.Text = "Choisissez d'abord une quête et un héros."; return; }
        var result = _session.Assign(_selectedOfferId, _selectedHeroId);
        _feedback.Text = result.Ok ? "Héros engagé. Résolvez la mission quand vous voulez." : "Refusé : " + result.Message;
        if (result.Ok) _selectedQuestId = result.Value!.Id;
        Refresh();
    }

    private void OnResolve()
    {
        if (_selectedQuestId is null) { _feedback.Text = "Choisissez une mission en cours."; return; }
        var result = _session.Resolve(_selectedQuestId);
        if (!result.Ok) { _feedback.Text = "Refusé : " + result.Message; Refresh(); return; }
        var o = result.Value!;
        _feedback.Text = $"{(o.Success ? "SUCCÈS" : "ÉCHEC")} — tirage {o.Roll} pour {o.ChancePercent} % de chance. " +
                         $"Prime : {_session.Money(o.RewardPaidCopper)}. Énergie -{o.EnergySpent}. " +
                         $"Réputation {o.ReputationBefore} → {o.ReputationAfter} ({o.ReputationDelta:+#;-#;0}).";
        if (o.Incident is { } incident)
        {
            _selectedComplaintId = incident.ComplaintId;
            _feedback.Text += $"\nINCIDENT — {incident.Title} : {incident.Text} (dommage collatéral {incident.CollateralReputationDelta}). Une réclamation est ouverte.";
        }
        Refresh();
    }

    private void OnComplaint(ComplaintChoice choice)
    {
        if (_selectedComplaintId is null) { _feedback.Text = "Choisissez une réclamation."; return; }
        var result = _session.ResolveComplaint(_selectedComplaintId, choice);
        _feedback.Text = result.Ok
            ? $"{result.Value!.Message} Réputation {result.Value.ReputationBefore} → {result.Value.ReputationAfter} ({result.Value.ReputationDelta:+#;-#;0})."
            : "Refusé : " + result.Message;
        Refresh();
    }

    private void OnRegenerate()
    {
        var result = _session.RegenerateOffers();
        if (!result.Ok) { _feedback.Text = "Refusé : " + result.Message; Refresh(); return; }
        _selectedOfferId = null;
        var v = result.Value!;
        _feedback.Text = $"Tableau renouvelé. Réputation {v.ReputationBefore} → {v.ReputationAfter}. Ce droit ne peut servir qu'une fois par partie.";
        Refresh();
    }

    private void OnEndDay()
    {
        var result = _session.EndDay();
        if (!result.Ok) { _feedback.Text = "Refusé : " + result.Message; Refresh(); return; }
        _selectedOfferId = null;
        _feedback.Text = result.Value!.Report is { } report
            ? report.Message
            : $"Jour {result.Value.NewDay} : énergie récupérée, {result.Value.NewOffers.Count} nouvelles offres.";
        Refresh();
    }

    private void Refresh()
    {
        var g = _session.Guild;
        _status.Text = $"Jour {g.Day}/{_session.Data.Rules.Days}   |   Trésorerie : {_session.Money(g.TreasuryCopper)}   |   Réputation : {g.Reputation}/100 ({_session.ReputationTierId})   |   " +
                       $"Missions engagées aujourd'hui : {g.MissionsAcceptedToday}/{_session.Data.Rules.MaxMissionsPerDay}   |   Graine : {_session.Seed}";

        var offer = _session.Offers.FirstOrDefault(o => o.Id == _selectedOfferId);

        _heroList.Clear();
        for (var i = 0; i < _session.Heroes.Count; i++)
        {
            var h = _session.Heroes[i];
            var d = h.Definition;
            _heroList.AddItem($"{d.Name} — {d.Role} | {d.Skill} niv.{d.Level} | énergie {h.Energy} | {_session.Money(d.BaseCostCopper)}{(h.IsFree ? "" : " | EN MISSION")}");
            // La grisaille vient du verdict de Core, pas d'une règle recopiée ici.
            if (offer is not null && !_session.Preview(offer.Id, h.Id).CanAssign) _heroList.SetItemCustomFgColor(i, new Color(0.55f, 0.55f, 0.55f));
            if (h.Id == _selectedHeroId) _heroList.Select(i);
        }

        _offerList.Clear();
        var current = _session.CurrentOffers;
        for (var i = 0; i < current.Count; i++)
        {
            var o = current[i];
            var t = o.Template;
            _offerList.AddItem($"[{t.Category}] {t.Title} — niv.{t.MinLevel} {t.Skill} | énergie {t.EnergyCost} | prime {_session.Money(t.RewardCopper)}" +
                               (o.State == OfferState.Accepted ? " (acceptée)" : ""));
            if (o.State != OfferState.Available) _offerList.SetItemCustomFgColor(i, new Color(0.55f, 0.55f, 0.55f));
            if (o.Id == _selectedOfferId) _offerList.Select(i);
        }

        _questList.Clear();
        for (var i = 0; i < _session.Quests.Count; i++)
        {
            var q = _session.Quests[i];
            var hero = _session.Heroes.First(h => h.Id == q.HeroId).Definition.Name;
            _questList.AddItem($"{q.Template.Title} — {hero} — {q.State}");
            if (q.Id == _selectedQuestId) _questList.Select(i);
        }

        _complaintList.Clear();
        for (var i = 0; i < _session.Complaints.Count; i++)
        {
            var c = _session.Complaints[i];
            _complaintList.AddItem($"{c.Storylet.Title} — {c.State}" + (c.State == ComplaintState.Open ? $" (réparation {_session.Money(c.RepairCostCopper)}, avant la fin du jour {c.ExpiresAfterDay})" : ""));
            if (c.State != ComplaintState.Open) _complaintList.SetItemCustomFgColor(i, new Color(0.55f, 0.55f, 0.55f));
            if (c.Id == _selectedComplaintId) _complaintList.Select(i);
        }
        var selectedComplaint = _session.Complaints.FirstOrDefault(c => c.Id == _selectedComplaintId);
        foreach (var choice in Enum.GetValues<ComplaintChoice>())
        {
            var button = _complaintButtons[(int)choice];
            var labels = selectedComplaint?.Storylet.Labels;
            button.Text = labels is null ? choice.ToString() : choice switch
            {
                ComplaintChoice.Repair => $"{labels.Repair} ({_session.Money(selectedComplaint!.RepairCostCopper)})",
                ComplaintChoice.Apologize => labels.Apologize,
                _ => labels.Deny,
            };
            button.Disabled = selectedComplaint is null || _session.ComplaintCheck(selectedComplaint.Id, choice).Count > 0;
        }

        RefreshDetail(offer);
        var quest = _session.Quests.FirstOrDefault(q => q.Id == _selectedQuestId);
        var finished = _session.Status == GameStatus.Finished;
        _resolveButton.Disabled = finished || quest is null || quest.State != QuestState.Assigned;
        _endDayButton.Disabled = finished;
        _regenButton.Disabled = _session.RegenerationCheck().Count > 0;
        _regenButton.Text = _session.RegenerationUsed
            ? "Renouveler le tableau (déjà utilisé)"
            : $"Renouveler le tableau (−{_session.Data.Rules.RegenerationReputationCost} réputation, une fois par partie)";
        _endDayButton.Text = _session.Guild.Day >= _session.Data.Rules.Days ? "Terminer la journée et passer au bilan royal" : "Terminer la journée";
        _assignButton.Disabled = finished || offer is null || _selectedHeroId is null || !_session.Preview(offer.Id, _selectedHeroId).CanAssign;
        _journal.Text = string.Join("\n", _session.Events.Select(e => $"#{e.Sequence} (J{e.Day}) {e.Message}"));
    }

    private void RefreshDetail(QuestOffer? offer)
    {
        var hero = _session.Heroes.FirstOrDefault(h => h.Id == _selectedHeroId)?.Definition;
        var text = "";
        if (hero is not null)
            text += $"[b]{hero.Name}[/b] — {hero.Role}\nTrait : {hero.TraitText}\nAtout : {hero.TraitAdvantage}\nContrepartie : {hero.TraitDrawback}\n" +
                    "[i](Phase A : les traits sont affichés mais n'ont pas encore d'effet chiffré.)[/i]\n\n";
        if (offer is not null)
            text += $"[b]{offer.Template.Title}[/b]\n{offer.Template.Description}\n\n";
        if (offer is not null && hero is not null)
        {
            var check = _session.Preview(offer.Id, hero.Id);
            text += $"Coût d'engagement : {_session.Money(check.EngagementCostCopper)}\n";
            if (check.Chance is { } c)
                text += $"Chance de réussite : [b]{c.Final} %[/b]  (base {c.Base} − risque {c.RiskPenalty} + niveau {c.LevelBonus:+#;-#;0} + trait {c.TraitModifier:+#;-#;0} − fatigue {c.LowEnergyPenalty})\n";
            text += check.CanAssign ? "[color=green]Affectation possible.[/color]" : $"[color=red]Impossible : {check.Message}[/color]";
        }
        else if (hero is null && offer is null && _session.Report is { } report)
        {
            text = $"[b]{(report.Success ? "Redressement réussi" : "Bilan inachevé")}[/b]\n{report.Message}";
        }
        else if (hero is null && offer is null)
        {
            text = "Sélectionnez un aventurier et un contrat.";
        }
        _detail.Text = text;
    }
}
