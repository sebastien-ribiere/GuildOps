using Godot;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;

namespace GuildOps.Game.Ui;

/// <summary>Libellés d'affichage (accents) des énumérations de Core. Présentation uniquement.</summary>
public static class Names
{
    public static string Category(QuestCategory c) => c switch
    {
        QuestCategory.Corvee => "Corvée", QuestCategory.Village => "Village", QuestCategory.Aventure => "Aventure",
        QuestCategory.Notable => "Notable", _ => "Royale",
    };

    public static string Skill(Skill s) => s switch
    {
        Core.Domain.Skill.Ingenierie => "Ingénierie", Core.Domain.Skill.Magie => "Magie",
        Core.Domain.Skill.Exploration => "Exploration", _ => "Combat",
    };

    /// <summary>Couleur de pastille par catégorie : toujours accompagnée du texte, jamais seule porteuse du sens.</summary>
    public static Color CategoryColor(QuestCategory c) => c switch
    {
        QuestCategory.Corvee => new Color(0.42f, 0.36f, 0.30f),
        QuestCategory.Village => new Color(0.17f, 0.40f, 0.22f),
        QuestCategory.Aventure => new Color(0.17f, 0.32f, 0.62f),
        QuestCategory.Notable => new Color(0.46f, 0.22f, 0.56f),
        _ => new Color(0.60f, 0.38f, 0.04f),
    };
}

/// <summary>Fabriques de cartes (héros, offre) : présentation pure, états visuels dictés par les verdicts de Core.</summary>
public static class Cards
{
    public const int HeroCardWidth = 104;
    public const int HeroCardHeight = 102;
    private const int PortraitBoxSize = 66;

    /// <summary>Portrait 64×64 dans son cadre 72×72 (utilisé là où la place le permet : détail de mission).</summary>
    public static Control PortraitWithFrame(string heroId)
    {
        var holder = new Control { CustomMinimumSize = new Vector2(72, 72), MouseFilter = Control.MouseFilterEnum.Ignore };
        // Le cadre fourni a un centre opaque : il se dessine d'abord, le portrait 64×64 par-dessus.
        holder.AddChild(new TextureRect { Texture = UiKit.Tex("ui/portrait_frame_72x72.png"), Size = new Vector2(72, 72), MouseFilter = Control.MouseFilterEnum.Ignore });
        holder.AddChild(new TextureRect
        {
            Texture = UiKit.Tex($"portraits/{heroId}.png"), Position = new Vector2(4, 4), Size = new Vector2(64, 64),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        return holder;
    }

    /// <summary>Portrait compact 64×64 sur fond du cadre, liseré doré de 1 px (66×66) : gagne 6 px de haut par rapport au cadre ornemental.</summary>
    private static Control PortraitCompact(string heroId)
    {
        var holder = new Panel { CustomMinimumSize = new Vector2(PortraitBoxSize, PortraitBoxSize), MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.AddThemeStyleboxOverride("panel", UiKit.Flat(new Color(0.16f, 0.20f, 0.34f), UiKit.Gold, 1, 0));
        holder.AddChild(new TextureRect
        {
            Texture = UiKit.Tex($"portraits/{heroId}.png"), Position = new Vector2(1, 1), Size = new Vector2(64, 64),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        return holder;
    }

    /// <summary>
    /// Carte de héros : portrait → nom (2 lignes au besoin, jamais tronqué) → spécialité/niveau ; énergie et coût en pastilles
    /// sur les angles du portrait (le visage reste dégagé). États : sélectionné ✔, engagé ⚠, incompatible ✖ (icône + grisé).
    /// </summary>
    public static CardButton HeroCard(GameSession session, Hero hero, CardState state, string? reason)
    {
        var d = hero.Definition;
        var card = new CardButton
        {
            CustomMinimumSize = new Vector2(HeroCardWidth, HeroCardHeight),
            TooltipText = $"{d.Name} — {d.Role}\n{Names.Skill(d.Skill)} niveau {d.Level}, énergie {hero.Energy}, engagement {session.Money(d.BaseCostCopper)}\nTrait : {d.TraitText}" +
                          (hero.IsFree ? "" : "\nEn mission.") + (reason is null ? "" : $"\n{reason}"),
        };
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        box.OffsetLeft = 2; box.OffsetRight = -2; box.OffsetTop = 2; box.OffsetBottom = -2;
        card.AddChild(box);

        var portrait = PortraitCompact(d.Id);
        if (!hero.IsFree) portrait.Modulate = new Color(0.72f, 0.72f, 0.84f);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.AddChild(portrait);
        box.AddChild(center);

        // Spécialité + niveau (haut-gauche, style titre) ; énergie et coût (bas) : pastilles à icône, hors du visage.
        portrait.AddChild(Chip(Names.Skill(d.Skill).ToUpperInvariant() + " " + d.Level, null, new Vector2(1, 1), title: true));
        var bottom = new HBoxContainer { Position = new Vector2(1, PortraitBoxSize - 17), Size = new Vector2(PortraitBoxSize - 2, 16), MouseFilter = Control.MouseFilterEnum.Ignore };
        bottom.AddChild(Chip($"{hero.Energy}", "energy", Vector2.Zero, title: false));
        bottom.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        bottom.AddChild(Chip($"{d.BaseCostCopper}", "coin", Vector2.Zero, title: false));
        portrait.AddChild(bottom);

        // Nom : deux lignes réservées pour une grille stable ; WordSmart coupe aux espaces, aucune ellipse.
        var name = card.Track(UiKit.Lbl(d.Name, wrap: true));
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.VerticalAlignment = VerticalAlignment.Top;
        name.MaxLinesVisible = 2;
        name.AddThemeConstantOverride("line_spacing", -2); // deux lignes de 15 px : le nom entier tient sous le portrait
        name.CustomMinimumSize = new Vector2(HeroCardWidth - 8, 2 * UiKit.BodySize);
        box.AddChild(name);

        // Engagé ⚠ > incompatible ✖ (verdict de Core) > choisi ✔ : un héros choisi mais incompatible garde son ✖.
        var marker = !hero.IsFree ? "warning" : reason is not null ? "failure" : state == CardState.Selected ? "success" : null;
        card.SetState(state, marker);
        return card;
    }

    /// <summary>Pastille posée sur un portrait : icône optionnelle + valeur ; style titre pour les mentions en capitales.</summary>
    private static Control Chip(string text, string? icon, Vector2 position, bool title)
    {
        var chip = new PanelContainer { Position = position, MouseFilter = Control.MouseFilterEnum.Ignore };
        chip.AddThemeStyleboxOverride("panel", UiKit.Flat(new Color(UiKit.Ink, 0.88f), null, 0, 1));
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 0);
        if (icon is not null) row.AddChild(UiKit.Icon(icon));
        var label = UiKit.Lbl(text, UiKit.Cream);
        if (title) UiKit.AsTitle(label);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        chip.AddChild(row);
        return chip;
    }

    /// <summary>
    /// Offre du tableau : pastille de catégorie (texte + couleur) et titre ; compétence requise, énergie, prime ; description sur
    /// deux lignes au plus (texte complet en infobulle et dans le panneau d'affectation).
    /// </summary>
    public static CardButton OfferRow(GameSession session, QuestOffer offer, CardState state, int width, int height)
    {
        var t = offer.Template;
        var accepted = offer.State == OfferState.Accepted;
        var card = new CardButton
        {
            CustomMinimumSize = new Vector2(width, height),
            TooltipText = $"{t.Title} — {Names.Category(t.Category)}\n{t.Description}\n{Names.Skill(t.Skill)} niveau {t.MinLevel} requis · énergie {t.EnergyCost} · prime {session.Money(t.RewardCopper)}" +
                          (accepted ? "\nDéjà acceptée." : ""),
        };
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 1);
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        box.OffsetLeft = 7; box.OffsetRight = -22; box.OffsetTop = 5; box.OffsetBottom = -4;
        card.AddChild(box);

        // Ligne 1 : [CATÉGORIE] titre
        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 4);
        head.AddChild(CategoryBadge(t.Category));
        var title = card.Track(UiKit.Lbl(t.Title, clip: true));
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(title);
        if (accepted) head.AddChild(CategoryBadge(null, "Acceptée"));
        box.AddChild(head);

        // Ligne 2 : compétence · niveau  |  énergie  | prime
        var stats = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        stats.AddThemeConstantOverride("separation", 6);
        var need = card.Track(UiKit.Lbl($"{Names.Skill(t.Skill)} · niv. {t.MinLevel}"));
        need.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        stats.AddChild(need);
        stats.AddChild(StatCell(card, "energy", $"{t.EnergyCost}"));
        stats.AddChild(StatCell(card, "coin", session.Money(t.RewardCopper)));
        box.AddChild(stats);

        // Lignes 3-4 : description (deux lignes maximum)
        var desc = card.Track(UiKit.Lbl(t.Description, wrap: true));
        desc.MaxLinesVisible = 2;
        desc.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        desc.ClipText = true;
        box.AddChild(desc);

        card.SetState(state, accepted ? "success" : null);
        return card;
    }

    private static Control StatCell(CardButton card, string icon, string text)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 1);
        row.AddChild(UiKit.Icon(icon));
        var label = card.Track(UiKit.Lbl(text));
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        return row;
    }

    /// <summary>Pastille texte : catégorie (couleur propre) ou mention neutre (« Acceptée »).</summary>
    public static Control CategoryBadge(QuestCategory? category, string? text = null)
        => UiKit.Pill(text ?? Names.Category(category!.Value).ToUpperInvariant(),
            category is { } c ? Names.CategoryColor(c) : new Color(0.20f, 0.26f, 0.20f));

    /// <summary>Ligne de liste compacte (missions, réclamations) : titre puis sous-titre, hauteur fixe qui contient les deux lignes.</summary>
    public static CardButton ListRow(string title, string subtitle, CardState state, string? marker, int height = 36)
    {
        var card = new CardButton { CustomMinimumSize = new Vector2(0, height), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        box.OffsetLeft = 6; box.OffsetRight = -22; box.OffsetTop = 2; box.OffsetBottom = -2;
        card.AddChild(box);
        box.AddChild(card.Track(UiKit.Lbl(title, clip: true)));
        var sub = card.Track(UiKit.Lbl(subtitle, clip: true));
        box.AddChild(sub);
        card.TooltipText = $"{title}\n{subtitle}";
        card.SetState(state, marker);
        return card;
    }
}
