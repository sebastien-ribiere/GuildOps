using System.Text;
using Godot;

namespace GuildOps.Game.Ui;

/// <summary>
/// Crédits et licences. N'énumère QUE des contributions et licences vérifiables : l'auteur du dépôt (historique git), la police Pixeloid Sans
/// (fichier OFL du projet), le moteur Godot et l'environnement .NET (versions lues à l'exécution, texte de licence fourni par le moteur lui-même).
/// Aucun titulaire n'est inventé. Le texte est défilable au clavier (flèches, PageHaut/PageBas) : il prend le focus comme un contrôle ordinaire.
/// </summary>
public partial class CreditsScreen : Overlay
{
    public const string FontLicensePath = "res://Assets/fonts/OFL-PixeloidSans.txt";

    private RichTextLabel _text = null!;
    private Button _toggle = null!, _close = null!;
    private bool _licenses;

    public RichTextLabel Text => _text;
    public Button LicensesButton => _toggle;
    public bool ShowingLicenses => _licenses;

    protected override void Build()
    {
        AddDim(0.84f);
        var center = AddCenter();
        var panel = UiKit.Panel("parchment", 8);
        center.AddChild(panel);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);
        panel.AddChild(box);

        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 6);
        box.AddChild(head);
        head.AddChild(B53.Picture(B53.Crest(64)));
        var titles = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        head.AddChild(titles);
        titles.AddChild(UiKit.AsTitle(UiKit.Lbl("Crédits", UiKit.Ink)));
        titles.AddChild(UiKit.Lbl("GuildOps — jeu de gestion de guilde, édition B5.3", UiKit.MutedOnLight));
        box.AddChild(B53.OrnamentDivider());

        _text = UiKit.Rich(onLight: true);
        _text.FocusMode = FocusModeEnum.All;
        _text.MouseFilter = MouseFilterEnum.Stop;
        _text.CustomMinimumSize = new Vector2(480, 190);
        box.AddChild(_text);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        box.AddChild(row);
        _toggle = IconButton("Licences des composants", "credits", ToggleLicenses, 190, 20);
        row.AddChild(_toggle);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _close = UiKit.Btn("Fermer (Échap)", Close, 110);
        row.AddChild(_close);
        SetFocusRing([_text, _toggle, _close]);
    }

    protected override void OnOpening()
    {
        _licenses = false;
        Refresh();
    }

    private void ToggleLicenses()
    {
        _licenses = !_licenses;
        Refresh();
    }

    private void Refresh()
    {
        _text.Text = _licenses ? LicensesText() : CreditsText();
        _text.ScrollToLine(0);
        _toggle.Text = _licenses ? "Retour aux crédits" : "Licences des composants";
    }

    private static string CreditsText()
    {
        var godot = Engine.GetVersionInfo();
        var sb = new StringBuilder();
        void Section(string title, string body) => sb.Append("[b]").Append(title).Append("[/b]\n").Append(body).Append("\n\n");
        Section("Conception et développement", "Sébastien Ribière (auteur du dépôt GuildOps).");
        Section("Moteur", $"Godot Engine {godot["major"]}.{godot["minor"]}.{godot["patch"]} {godot["status"]} (édition .NET) — licence MIT.\nMicrosoft .NET {System.Environment.Version} (exécution embarquée) — licence MIT.");
        Section("Typographie", "Pixeloid Sans 1.0, © 2020-2025 GGBotNet (Reserved Font Name « Pixeloid ») — SIL Open Font License 1.1.");
        Section("Illustrations, sprites et interface", "Créés pour le projet GuildOps ; aucun élément de tiers sous licence n'y est reproduit " +
                "(origine détaillée dans docs/b5-3/SOURCES_ET_LICENCES.md et docs/b5-art-kit/README.md).");
        Section("Son", "GuildOps est volontairement silencieux : ni musique, ni effet sonore.");
        sb.Append("Le texte complet des licences est accessible par le bouton « Licences des composants ».");
        return sb.ToString();
    }

    private static string LicensesText()
    {
        var sb = new StringBuilder();
        sb.Append("[b]Pixeloid Sans — SIL Open Font License 1.1[/b]\n");
        using (var file = Godot.FileAccess.Open(FontLicensePath, Godot.FileAccess.ModeFlags.Read))
            sb.Append((file?.GetAsText() ?? "Fichier de licence introuvable.").Replace("[", "[lb]")).Append("\n\n");
        sb.Append("[b]Godot Engine[/b]\n").Append(Engine.GetLicenseText().Replace("[", "[lb]")).Append("\n\n");
        sb.Append("[b]Composants tiers de Godot[/b] (liste fournie par le moteur ; textes complets dans THIRD_PARTY_NOTICES.txt de la distribution)\n");
        var seen = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var entry in Engine.GetCopyrightInfo())
        {
            var licenses = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var part in (Godot.Collections.Array)entry["parts"]) licenses.Add((string)((Godot.Collections.Dictionary)part)["license"]);
            seen.Add($"- {entry["name"]} ({string.Join(", ", licenses)})");
        }
        sb.Append(string.Join('\n', seen).Replace("[", "[lb]"));
        return sb.ToString();
    }

    protected override Control InitialFocus() => _close;
}
