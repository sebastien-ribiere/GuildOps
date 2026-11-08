using Godot;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>
/// Aperçu explicable d'une affectation (<c>GameSession.Preview</c>), préparation et bouton Affecter.
/// Sans quête ou sans héros : guide en deux étapes et bouton d'aide contextuel. Avec les deux : verdict et motifs de refus (issus de Core),
/// chance et décomposition, coût, énergie et prime. Les états actif/inactif viennent exclusivement de Core.
/// </summary>
public partial class AssignmentPanel : PanelBase
{
    private HBoxContainer _steps = null!;
    private VBoxContainer _body = null!;
    private Button _assign = null!, _prepare = null!, _navigate = null!;
    private View _navigateTarget = View.Board;

    protected override void Build()
    {
        var panel = UiKit.Panel("dark", 6);
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(panel);
        var box = new VBoxContainer();
        panel.AddChild(box);

        box.AddChild(UiKit.Title("Affectation"));
        _steps = new HBoxContainer();
        _steps.AddThemeConstantOverride("separation", 3);
        box.AddChild(_steps);

        var scroll = UiKit.Scroll();
        box.AddChild(scroll);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(_body);

        _prepare = UiKit.Btn("", Shell.Prepare);
        _prepare.ClipText = true;
        box.AddChild(_prepare);
        _assign = UiKit.Btn("Affecter le héros", Shell.Assign);
        box.AddChild(_assign);
        _navigate = UiKit.Btn("", () => Shell.Show(_navigateTarget));
        box.AddChild(_navigate);
    }

    public override void FocusFirst() { }

    public override void Refresh()
    {
        if (Shell is null) return;
        var s = Shell.Session;
        var hero = Shell.SelectedHero;
        var offer = Shell.SelectedOffer;
        var finished = s.Status == GameStatus.Finished;

        BuildSteps(offer is not null, hero is not null);
        UiKit.Clear(_body);

        AssignmentCheck? check = null;
        if (offer is not null && hero is not null)
        {
            check = s.Preview(offer.Id, hero.Id);
            BuildVerdict(s, check);
        }
        else BuildGuide(offer is not null, hero is not null);

        if (offer is not null) BuildQuest(s, offer);
        if (hero is not null) BuildHero(s, hero);
        if (offer is null && hero is null) { /* le guide suffit */ }

        // Navigation contextuelle : mène à l'étape manquante, ou permet de changer de sélection.
        (_navigateTarget, _navigate.Text) = (offer, hero) switch
        {
            (null, _) => (View.Board, "Choisir une quête ->"),
            (_, null) => (View.Roster, "Choisir un héros ->"),
            _ => Shell.Current == View.Board ? (View.Roster, "Changer de héros ->") : (View.Board, "<- Changer de quête"),
        };
        _navigate.Visible = _navigateTarget != Shell.Current;

        var prep = Shell.SelectedHeroId is null ? null : s.PreparationCostFor(Shell.SelectedHeroId);
        _prepare.Visible = prep is not null;
        if (prep is not null)
        {
            var category = offer?.Template.Category;
            _prepare.Text = category is null ? "Préparer (choisir une quête)" : $"Préparer (E-{prep.EnergyCost}, {s.Money(prep.CopperCost)})";
            _prepare.TooltipText = category is null ? "Choisissez d'abord une quête : la préparation vaut pour sa catégorie."
                : $"Préparer la catégorie {Names.Category(category.Value)} : énergie -{prep.EnergyCost}, {s.Money(prep.CopperCost)}.";
            _prepare.Disabled = finished || category is null || s.PreparationCheck(Shell.SelectedHeroId!, category.Value).Count > 0;
        }
        _assign.Disabled = finished || check is not { CanAssign: true };
    }

    // ---- Étapes ---------------------------------------------------------------------------------------------------

    private void BuildSteps(bool hasQuest, bool hasHero)
    {
        UiKit.Clear(_steps);
        _steps.AddChild(Step(1, "Quête", hasQuest, current: !hasQuest));
        _steps.AddChild(Step(2, "Héros", hasHero, current: hasQuest && !hasHero));
    }

    /// <summary>Étape : « ✔ 1 QUÊTE » une fois choisie ; l'étape en cours est cerclée d'or. Jamais la couleur seule.</summary>
    private static Control Step(int n, string text, bool done, bool current)
    {
        var chip = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        var border = current ? UiKit.Gold : new Color(0.32f, 0.26f, 0.45f);
        chip.AddThemeStyleboxOverride("panel", UiKit.Flat(done ? new Color(0.13f, 0.30f, 0.16f) : new Color(0.16f, 0.12f, 0.26f), border, current ? 2 : 1, 2));
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 2);
        if (done) row.AddChild(UiKit.Icon("success"));
        row.AddChild(UiKit.AsTitle(UiKit.Lbl($"{n} {text}", done || current ? UiKit.Cream : UiKit.Muted)));
        chip.AddChild(row);
        return chip;
    }

    private void BuildGuide(bool hasQuest, bool hasHero)
    {
        var text = (hasQuest, hasHero) switch
        {
            (false, false) => "Pour engager un héros :\n1. choisissez une quête ;\n2. choisissez un héros.\nL'aperçu s'affichera ici, avec les chances de réussite.",
            (true, false) => "Quête choisie. Choisissez maintenant un héros : l'aperçu montrera ses chances et son coût.",
            _ => "Héros choisi. Choisissez maintenant une quête pour voir l'aperçu.",
        };
        _body.AddChild(UiKit.Lbl(text, UiKit.Cream, wrap: true));
        _body.AddChild(UiKit.Divider(new Color(UiKit.Gold, 0.5f)));
    }

    // ---- Verdict de Core ---------------------------------------------------------------------------------------------

    private void BuildVerdict(GameSession s, AssignmentCheck check)
    {
        if (check.CanAssign)
        {
            _body.AddChild(UiKit.IconText("success", "Affectation possible", UiKit.GoodOnDark));
        }
        else
        {
            _body.AddChild(UiKit.IconText("failure", "Affectation impossible", UiKit.BadOnDark));
            // Un motif par ligne, tel que Core le formule (jamais fusionnés en une phrase).
            foreach (var reason in check.Reasons)
                _body.AddChild(UiKit.IconText("warning", RefusalMessages.For(reason), UiKit.BadOnDark, wrap: true));
        }

        if (check.Chance is { } c)
        {
            var head = new HBoxContainer();
            head.AddChild(UiKit.AsTitle(UiKit.Lbl("Chance de réussite", UiKit.Gold)));
            head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            head.AddChild(UiKit.AsTitle(UiKit.Lbl($"{c.Final} %", UiKit.Cream)));
            _body.AddChild(head);
            _body.AddChild(UiKit.Bar(c.Final, UiKit.Gold, new Color(0.25f, 0.20f, 0.38f)));
            _body.AddChild(UiKit.Lbl(
                $"base {c.Base} − risque {c.RiskPenalty} + niveau {c.LevelBonus:+#;-#;0} + trait {c.TraitModifier:+#;-#;0} − fatigue {c.LowEnergyPenalty}",
                UiKit.Muted, wrap: true));
        }

        var figures = new GridContainer { Columns = 2 };
        figures.AddThemeConstantOverride("h_separation", 6);
        figures.AddThemeConstantOverride("v_separation", 1);
        Figure(figures, "coin", "Engagement", s.Money(check.EngagementCostCopper));
        Figure(figures, "energy", "Énergie", $"{check.EnergyCost}");
        Figure(figures, "coin", "Prime si succès", s.Money(check.RewardCopper));
        _body.AddChild(figures);

        foreach (var note in check.Notes)
            _body.AddChild(UiKit.IconText("scroll", note, UiKit.Cream, wrap: true));
        _body.AddChild(UiKit.Divider(new Color(UiKit.Gold, 0.5f)));
    }

    private static void Figure(GridContainer grid, string icon, string label, string value)
    {
        grid.AddChild(UiKit.IconText(icon, label, UiKit.Muted));
        grid.AddChild(UiKit.Lbl(value, UiKit.Cream));
    }

    // ---- Récapitulatifs ------------------------------------------------------------------------------------------------

    private void BuildQuest(GameSession s, QuestOffer offer)
    {
        var t = offer.Template;
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 4);
        head.AddChild(Cards.CategoryBadge(t.Category));
        var title = UiKit.AsTitle(UiKit.Lbl(t.Title, UiKit.Gold, wrap: true));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        _body.AddChild(head);
        _body.AddChild(UiKit.Lbl($"{Names.Skill(t.Skill)} niv. {t.MinLevel} · énergie {t.EnergyCost} · prime {s.Money(t.RewardCopper)}", UiKit.Cream, wrap: true));
        _body.AddChild(UiKit.Lbl(t.Description, UiKit.Muted, wrap: true));
    }

    private void BuildHero(GameSession s, Hero hero)
    {
        var d = hero.Definition;
        _body.AddChild(UiKit.Divider(new Color(UiKit.Gold, 0.25f)));
        _body.AddChild(UiKit.AsTitle(UiKit.Lbl($"{d.Name} — {d.Role}", UiKit.Gold, wrap: true)));
        _body.AddChild(UiKit.Lbl($"{Names.Skill(d.Skill)} niv. {d.Level} · énergie {hero.Energy} · engagement {s.Money(d.BaseCostCopper)}" + (hero.IsFree ? "" : " · en mission"), UiKit.Cream, wrap: true));
        _body.AddChild(UiKit.RichFit(false,
            $"Trait : {d.TraitText}\n[color=#{UiKit.Hex(UiKit.GoodOnDark)}]Atout :[/color] {d.TraitAdvantage}\n[color=#{UiKit.Hex(UiKit.BadOnDark)}]Contrepartie :[/color] {d.TraitDrawback}"));
    }
}
