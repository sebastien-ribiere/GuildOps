using Godot;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;

namespace GuildOps.Game.Ui;

/// <summary>Fabriques de cartes (héros, offre) : présentation pure, états visuels dictés par les verdicts de Core.</summary>
public static class Cards
{
    public const int HeroCardWidth = 104;
    public const int HeroCardHeight = 100;

    /// <summary>Portrait 64×64 dans son cadre 72×72, avec sprite d'attente optionnel ailleurs.</summary>
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

    public static CardButton HeroCard(GameSession session, Hero hero, CardState state, string? reason)
    {
        var d = hero.Definition;
        var card = new CardButton
        {
            CustomMinimumSize = new Vector2(HeroCardWidth, HeroCardHeight),
            TooltipText = $"{d.Name} — {d.Role}\n{d.Skill} niveau {d.Level}, énergie {hero.Energy}, engagement {session.Money(d.BaseCostCopper)}\nTrait : {d.TraitText}" +
                          (hero.IsFree ? "" : "\nEn mission.") + (reason is null ? "" : $"\n{reason}"),
        };
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        box.OffsetLeft = 2; box.OffsetRight = -2; box.OffsetTop = 2; box.OffsetBottom = -2;
        card.AddChild(box);

        var portrait = PortraitWithFrame(d.Id);
        if (!hero.IsFree)
        {
            var badge = UiKit.Icon("warning");
            badge.Position = new Vector2(54, 54);
            portrait.AddChild(badge);
            portrait.Modulate = new Color(0.75f, 0.75f, 0.85f);
        }
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.AddChild(portrait);
        box.AddChild(center);

        // Coût (bas) et énergie (haut) en pastilles posées sur le portrait : la carte tient dans la grille 4×3.
        portrait.AddChild(Chip(session.Money(d.BaseCostCopper), new Vector2(4, 72 - 4 - 10), 64));
        portrait.AddChild(Chip($"E{hero.Energy}", new Vector2(4, 4), 0));

        foreach (var text in new[] { d.Name, $"{d.Skill} {d.Level}" })
        {
            var label = card.Track(UiKit.Lbl(text, clip: true));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.CustomMinimumSize = new Vector2(HeroCardWidth - 8, 8);
            box.AddChild(label);
        }
        card.SetState(state);
        return card;
    }

    private static Control Chip(string text, Vector2 position, int width)
    {
        var chip = new PanelContainer { Position = position, MouseFilter = Control.MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat { BgColor = new Color(UiKit.Ink, 0.85f) };
        style.SetContentMarginAll(1);
        chip.AddThemeStyleboxOverride("panel", style);
        var label = UiKit.Lbl(text, UiKit.Cream);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        chip.AddChild(label);
        if (width > 0) chip.CustomMinimumSize = new Vector2(width, 0);
        return chip;
    }

    public static CardButton OfferRow(GameSession session, QuestOffer offer, CardState state, int width, int height)
    {
        var t = offer.Template;
        var card = new CardButton { CustomMinimumSize = new Vector2(width, height), TooltipText = $"{t.Title}\n{t.Description}" };
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 1);
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        box.OffsetLeft = 8; box.OffsetRight = -8; box.OffsetTop = 7; box.OffsetBottom = -5;
        card.AddChild(box);

        var head = card.Track(UiKit.Lbl($"{t.Title}" + (offer.State == OfferState.Accepted ? "  (ACCEPTÉE)" : ""), clip: true));
        box.AddChild(head);
        var stats = card.Track(UiKit.Lbl($"{t.Category} · {t.Skill} niv.{t.MinLevel} · énergie {t.EnergyCost} · prime {session.Money(t.RewardCopper)}", clip: true));
        box.AddChild(stats);
        var desc = card.Track(UiKit.Lbl(t.Description, wrap: true));
        desc.MaxLinesVisible = 3;
        desc.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        desc.ClipText = true;
        box.AddChild(desc);
        card.SetState(state);
        return card;
    }
}
