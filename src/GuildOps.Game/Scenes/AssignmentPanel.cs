using Godot;
using GuildOps.Core.Domain;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>Aperçu explicable d'une affectation (<c>GameSession.Preview</c>), préparation et bouton Affecter. Les états actif/inactif viennent de Core.</summary>
public partial class AssignmentPanel : PanelBase
{
    private Label _title = null!;
    private RichTextLabel _detail = null!;
    private Button _assign = null!, _prepare = null!, _navigate = null!;

    protected override void Build()
    {
        var panel = UiKit.Panel("dark", 6);
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(panel);
        var box = new VBoxContainer();
        panel.AddChild(box);

        _title = UiKit.Lbl("AFFECTATION", UiKit.Gold, clip: true);
        box.AddChild(_title);
        _detail = UiKit.Rich(onLight: false);
        box.AddChild(_detail);
        _prepare = UiKit.Btn("", Shell.Prepare);
        _prepare.ClipText = true;
        box.AddChild(_prepare);
        _assign = UiKit.Btn("Affecter le héros", Shell.Assign);
        box.AddChild(_assign);
        _navigate = UiKit.Btn("", () => Shell.Show(Shell.Current == View.Board ? View.Roster : View.Board));
        box.AddChild(_navigate);
    }

    public override void FocusFirst() { }

    public override void Refresh()
    {
        if (Shell is null) return;
        var s = Shell.Session;
        var hero = Shell.SelectedHero?.Definition;
        var offer = Shell.SelectedOffer;
        var finished = s.Status == GameStatus.Finished;

        _navigate.Text = Shell.Current == View.Board ? "Choisir un héros →" : "← Choisir une quête";

        var text = "";
        if (hero is not null)
            text += $"[b]{hero.Name}[/b] — {hero.Role}\nTrait : {hero.TraitText}\n[color=#{UiKit.Hex(UiKit.GoodOnDark)}]Atout :[/color] {hero.TraitAdvantage}\n[color=#{UiKit.Hex(UiKit.BadOnDark)}]Contrepartie :[/color] {hero.TraitDrawback}\n";
        else text += "Aucun héros sélectionné.\n";
        if (offer is not null) text += $"[b]{offer.Template.Title}[/b]\n";
        else text += "Aucune quête sélectionnée.\n";
        if (offer is not null && hero is not null)
        {
            var check = s.Preview(offer.Id, hero.Id);
            text += $"Engagement : {s.Money(check.EngagementCostCopper)}\nÉnergie dépensée : {check.EnergyCost}\nPrime si succès : {s.Money(check.RewardCopper)}\n";
            foreach (var note in check.Notes) text += $" • {note}\n";
            if (check.Chance is { } c)
                text += $"Chance : [b]{c.Final} %[/b]\n(base {c.Base} − risque {c.RiskPenalty} + niveau {c.LevelBonus:+#;-#;0} + trait {c.TraitModifier:+#;-#;0} − fatigue {c.LowEnergyPenalty})\n";
            text += check.CanAssign
                ? $"[color=#{UiKit.Hex(UiKit.GoodOnDark)}]Affectation possible.[/color]"
                : $"[color=#{UiKit.Hex(UiKit.BadOnDark)}]Impossible : {check.Message}[/color]";
        }
        _detail.Text = text;

        var prep = Shell.SelectedHeroId is null ? null : s.PreparationCostFor(Shell.SelectedHeroId);
        _prepare.Visible = prep is not null;
        if (prep is not null)
        {
            var category = offer?.Template.Category;
            _prepare.Text = category is null ? "Préparer (choisir une quête)" : $"Préparer {category} (E-{prep.EnergyCost}, {s.Money(prep.CopperCost)})";
            _prepare.Disabled = finished || category is null || s.PreparationCheck(Shell.SelectedHeroId!, category.Value).Count > 0;
        }
        _assign.Disabled = finished || offer is null || hero is null || !s.Preview(offer.Id, hero.Id).CanAssign;
    }
}
