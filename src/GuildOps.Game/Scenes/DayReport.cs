using System.Text;
using Godot;
using GuildOps.Core.Domain;
using GuildOps.Core.Engine;
using GuildOps.Game.Ui;

namespace GuildOps.Game;

/// <summary>
/// Journal des événements (regroupé par jour), bilan de la guilde avec objectifs du bilan royal, fin de journée (<c>EndDay</c>) et redémarrage.
/// Les objectifs et leur atteinte viennent de <c>rules.Victory</c> et de <c>FinalReport</c> : aucune règle n'est recalculée ici.
/// </summary>
public partial class DayReport : PanelBase
{
    private RichTextLabel _journal = null!;
    private VBoxContainer _side = null!;
    private int _lastDay;
    private readonly Dictionary<int, (int reputation, int treasury)> _dayStart = [];

    protected override void Build()
    {
        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 6; root.OffsetRight = -6; root.OffsetTop = 4; root.OffsetBottom = -4;
        root.AddThemeConstantOverride("separation", 4);
        AddChild(root);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.15f };
        root.AddChild(left);
        left.AddChild(UiKit.Title("Journal de la guilde"));
        var panel = UiKit.Panel("dark", 6);
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        left.AddChild(panel);
        _journal = UiKit.Rich(onLight: false);
        _journal.ScrollFollowing = true;
        panel.AddChild(_journal);

        var right = UiKit.Panel("parchment", 8);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(right);
        var scroll = UiKit.Scroll();
        right.AddChild(scroll);
        _side = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _side.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(_side);
    }

    public override void FocusFirst() => (FirstButton(_side) ?? FirstButton(this))?.GrabFocus();

    public override void Refresh()
    {
        if (Shell is null) return;
        var s = Shell.Session;
        var g = s.Guild;

        // Repères de début de journée (présentation : simple différence avec l'état courant de Core).
        if (g.Day < _lastDay) _dayStart.Clear();
        _lastDay = g.Day;
        if (!_dayStart.ContainsKey(g.Day)) _dayStart[g.Day] = (g.Reputation, g.TreasuryCopper);

        BuildJournal(s);

        var hadFocus = GetViewport()?.GuiGetFocusOwner() is { } f && _side.IsAncestorOf(f);
        BuildSide(s);
        if (hadFocus && Visible) FirstButton(_side)?.GrabFocus();
    }

    // ---- Journal ----------------------------------------------------------------------------------------------------

    private void BuildJournal(GameSession s)
    {
        var sb = new StringBuilder();
        if (s.Events.Count == 0) sb.Append("[color=#").Append(UiKit.Hex(UiKit.Muted)).Append("]Le journal est vide : rien ne s'est encore passé.[/color]");
        var currentDay = -1;
        foreach (var e in s.Events)
        {
            if (e.Day != currentDay)
            {
                currentDay = e.Day;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("[color=#").Append(UiKit.Hex(UiKit.Gold)).Append("][b]Jour ").Append(e.Day).Append("[/b][/color]\n");
            }
            sb.Append("[color=#").Append(UiKit.Hex(UiKit.Muted)).Append("]#").Append(e.Sequence).Append("[/color] ")
              .Append("[color=#").Append(UiKit.Hex(EventColor(e.Type))).Append(']').Append(UiKit.Plain(e.Message).Replace("[", "[lb]")).Append("[/color]\n");
        }
        _journal.Text = sb.ToString().TrimEnd('\n');
    }

    /// <summary>Couleur selon le type d'événement (jamais le seul vecteur du sens : le texte du message reste complet).</summary>
    private static Color EventColor(GameEventType type) => type switch
    {
        GameEventType.ComplaintOpened or GameEventType.ComplaintExpired => UiKit.BadOnDark,
        GameEventType.ComplaintResolved or GameEventType.GameFinished => UiKit.GoodOnDark,
        GameEventType.ReputationChanged => UiKit.Gold,
        GameEventType.DayEnded or GameEventType.OffersPublished or GameEventType.OffersRegenerated or GameEventType.EnergyRecovered => new Color(0.72f, 0.68f, 0.82f),
        _ => UiKit.Cream,
    };

    // ---- Bilan ------------------------------------------------------------------------------------------------------

    private void BuildSide(GameSession s)
    {
        var g = s.Guild;
        var rules = s.Data.Rules;
        var v = rules.Victory;
        var finished = s.Status == GameStatus.Finished;
        var report = s.Report;
        UiKit.Clear(_side);

        var heading = UiKit.AsTitle(UiKit.Lbl($"Bilan — jour {g.Day}/{rules.Days}", UiKit.Ink));
        heading.MouseFilter = MouseFilterEnum.Pass;
        heading.TooltipText = $"Graine de la partie : {s.Seed}";
        _side.AddChild(heading);
        if (report is { } r)
            _side.AddChild(Banner(r.Success ? "Redressement réussi" : "Bilan inachevé", r.Success ? "success" : "failure", r.Success ? new Color(0.14f, 0.38f, 0.16f) : new Color(0.60f, 0.12f, 0.12f)));
        else if (g.Day >= rules.Days)
            _side.AddChild(Banner("Dernier jour — bilan royal", "warning", new Color(0.60f, 0.38f, 0.04f)));

        // Objectifs du bilan royal : valeur actuelle / objectif (Core). L'atteinte n'est affirmée qu'une fois le bilan rendu.
        if (report is null) _side.AddChild(UiKit.Lbl($"Objectifs du bilan royal (jour {rules.Days})", UiKit.MutedOnLight));
        var (repStart, treasuryStart) = _dayStart.GetValueOrDefault(g.Day, (g.Reputation, g.TreasuryCopper));
        _side.AddChild(Objective("reputation", "Réputation", $"{g.Reputation} / {v.MinReputation}", Ratio(g.Reputation, v.MinReputation), report?.ReputationOk,
            $"{s.ReputationTierId} · aujourd'hui {g.Reputation - repStart:+#;-#;0}"));
        _side.AddChild(Objective("coin", "Trésorerie", $"{s.Money(g.TreasuryCopper)} / {s.Money(v.MinTreasuryCopper)}", Ratio(g.TreasuryCopper, v.MinTreasuryCopper), report?.TreasuryOk,
            $"aujourd'hui {Signed(s, g.TreasuryCopper - treasuryStart)}"));
        _side.AddChild(Objective("scroll", "Missions réussies", $"{g.MissionsSucceeded} / {v.MinSucceededMissions}", Ratio(g.MissionsSucceeded, v.MinSucceededMissions), report?.MissionsOk,
            $"engagées aujourd'hui {g.MissionsAcceptedToday}/{rules.MaxMissionsPerDay}"));

        if (report is { } final) _side.AddChild(UiKit.Lbl(UiKit.Plain(final.Message), UiKit.Ink, wrap: true));

        var open = s.OpenComplaints.Count;
        if (open > 0)
        {
            _side.AddChild(UiKit.IconText("warning", $"{open} réclamation(s) ouverte(s) à traiter", UiKit.BadOnLight, wrap: true));
            _side.AddChild(UiKit.Btn("Voir les réclamations ->", () => Shell.Show(View.Complaints)));
        }
        var pending = s.Quests.Count(q => q.State == QuestState.Assigned);
        if (pending > 0 && !finished)
            _side.AddChild(UiKit.IconText("warning", $"{pending} mission(s) à résoudre avant de terminer la journée.", UiKit.Ink, wrap: true));

        var end = UiKit.Btn(g.Day >= rules.Days ? "Terminer la journée -> bilan royal" : "Terminer la journée", Shell.EndDay);
        end.ClipText = true;
        end.Disabled = finished;
        _side.AddChild(end);
        if (finished) _side.AddChild(UiKit.Btn("Recommencer (même graine)", Shell.Reset));
    }

    private static int Ratio(int value, int target) => target <= 0 ? 100 : Math.Clamp(value * 100 / target, 0, 100);
    private static string Signed(GameSession s, int copper) => copper == 0 ? "0" : (copper > 0 ? "+" : "−") + s.Money(Math.Abs(copper));

    /// <summary>Ligne d'objectif : icône, libellé, valeur/objectif, barre de proportion, état (✔/✖ seulement après le bilan) et ligne d'appoint.</summary>
    private static Control Objective(string icon, string label, string value, int percent, bool? ok, string note)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 1);
        var row = new HBoxContainer();
        row.AddChild(UiKit.IconText(icon, label, UiKit.Ink));
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var val = UiKit.Lbl(value, UiKit.Ink);
        row.AddChild(val);
        if (ok is { } reached) row.AddChild(UiKit.Icon(reached ? "success" : "failure"));
        box.AddChild(row);
        box.AddChild(UiKit.Bar(percent, ok switch { true => UiKit.GoodOnLight, false => UiKit.BadOnLight, _ => CardButton.ParchmentEdge }, new Color(0.78f, 0.66f, 0.44f)));
        box.AddChild(UiKit.Lbl(note, UiKit.MutedOnLight));
        return box;
    }

    private static Control Banner(string text, string icon, Color fill)
    {
        var banner = new PanelContainer { CustomMinimumSize = new Vector2(0, 22) };
        banner.AddThemeStyleboxOverride("panel", UiKit.Flat(fill, UiKit.Ink, 2, 3));
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(UiKit.Icon(icon));
        var label = UiKit.AsTitle(UiKit.Lbl(text, UiKit.Cream));
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        banner.AddChild(row);
        return banner;
    }
}
