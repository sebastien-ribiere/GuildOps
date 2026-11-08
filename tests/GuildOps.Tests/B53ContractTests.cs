using System.Text.RegularExpressions;
using GuildOps.Game.Ui;

namespace GuildOps.Tests;

/// <summary>
/// Contrat B5.3 vérifiable sans Godot : silence intégral, session unique, démarrage normal vs --autoplay, aucune sauvegarde fictive,
/// aucune condition de victoire recalculée par l'interface, texte de l'aide fidèle aux règles. Le comportement réel (écrans, focus,
/// parties de sept jours) est exercé par `tools/run-flow.sh` (FlowCheck) dans Godot ; ces tests gardent les invariants de structure.
/// </summary>
public class B53ContractTests
{
    private static readonly string Root = FindRoot();
    private static readonly string Game = Path.Combine(Root, "src", "GuildOps.Game");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "GuildOps.Game"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Racine du dépôt introuvable.");
    }

    private static IEnumerable<string> Files(string folder, params string[] extensions) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

    private static string Source(string relative) => File.ReadAllText(Path.Combine(Game, relative));

    // ---- Silence ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Aucun_fichier_audio_dans_le_projet()
    {
        var audio = Files(Path.Combine(Root, "src"), ".ogg", ".wav", ".mp3", ".opus", ".flac", ".mid", ".sf2", ".tres", ".res")
            .Where(f => !f.EndsWith(".tres") && !f.EndsWith(".res") || Regex.IsMatch(File.ReadAllText(f), "AudioStream|AudioBus"))
            .ToList();
        Assert.True(audio.Count == 0, "Fichiers audio : " + string.Join(", ", audio));
    }

    [Fact]
    public void Aucun_code_ni_reglage_audio()
    {
        var offenders = Files(Game, ".cs", ".tscn", ".godot", ".cfg", ".gd")
            .SelectMany(f => File.ReadLines(f).Select((l, i) => (f, l, i))
                .Where(x => Regex.IsMatch(x.l, @"AudioStreamPlayer|AudioStream\b|AudioServer\.(Add|Set)|PlaySound|AudioBusLayout|\[audio\]|audio/driver")
                         && Path.GetFileName(x.f) is not "FlowCheck.cs"))   // FlowCheck LIT le mixage pour prouver le silence
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}: {x.l.Trim()}").ToList();
        Assert.True(offenders.Count == 0, "Audio : " + string.Join(" | ", offenders));
    }

    [Fact]
    public void Aucun_reglage_audio_dans_les_parametres()
    {
        var settings = Source("Ui/SettingsScreen.cs") + Source("Ui/DisplaySettings.cs");
        Assert.DoesNotMatch(new Regex("volume|musique|son\\b|audio", RegexOptions.IgnoreCase), Regex.Replace(settings, @"///.*|//.*", ""));
    }

    // ---- Session unique, démarrage, absence de sauvegarde fictive ------------------------------------------------------------

    [Fact]
    public void Une_seule_GameSession_est_creee_par_le_jeu()
    {
        var starts = Files(Game, ".cs")
            .Where(f => Path.GetFileName(f) is not ("FlowCheck.cs" or "AutoPlay.cs"))   // les contrôles comparent à une simulation Core pure
            .SelectMany(f => File.ReadLines(f).Where(l => l.Contains("GameSession.Start(") && !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("///")).Select(_ => Path.GetFileName(f)))
            .ToList();
        Assert.Equal(["GameShell.cs"], starts);
    }

    [Fact]
    public void Le_demarrage_normal_ouvre_le_titre_et_autoplay_le_contourne()
    {
        var shell = Source("GameShell.cs");
        Assert.Contains("Automated = OS.GetCmdlineUserArgs().Contains(\"--autoplay\")", shell);
        // Le titre n'est ouvert que dans la branche « non automatisée », jamais avant ni dans la branche AutoPlay.
        Assert.Matches(new Regex(@"if \(Automated\) _ = new AutoPlay\(this\)\.Run\(\);\s*else\s*\{\s*_title\.Open\(\);", RegexOptions.Singleline), shell);
        // Ouvertures du titre : démarrage normal (une fois, dans _Ready) et commande « Menu » du HUD (OpenTitle) ; jamais ailleurs dans le shell.
        Assert.Equal(2, Regex.Matches(shell, @"_title\.Open\(\)").Count);
        // L'écran de fin n'est jamais imposé en mode automatisé.
        Assert.Contains("if (!Automated) _end.Present(final);", shell);
    }

    [Fact]
    public void Aucun_bouton_Continuer_ni_sauvegarde_de_partie()
    {
        var code = string.Join("\n", Files(Game, ".cs").Where(f => Path.GetFileName(f) is not "FlowCheck.cs")   // FlowCheck affirme justement l'absence
            .Select(File.ReadAllText).Select(t => Regex.Replace(t, @"(?<!:)//.*", "")));
        Assert.DoesNotMatch(new Regex("\"Continuer"), code);
        // Le seul fichier écrit dans user:// est celui des préférences d'affichage ; aucun chargement/sauvegarde de partie.
        var userPaths = Regex.Matches(code, "\"user://[^\"]*\"").Select(m => m.Value).Distinct().ToList();
        Assert.Equal(["\"user://display.cfg\""], userPaths);
        Assert.DoesNotMatch(new Regex(@"\bSaveGame|LoadGame|SaveSlot|JsonSerializer|FileAccess\.Open\(\s*\$?""user://"), code);
    }

    [Fact]
    public void Les_preferences_ne_contiennent_que_des_reglages_d_affichage()
    {
        var source = Source("Ui/DisplaySettings.cs");
        var keys = Regex.Matches(source, @"file\.SetValue\(Section, ""([a-z_]+)""").Select(m => m.Groups[1].Value).OrderBy(k => k).ToArray();
        Assert.Equal(["fullscreen", "reduce_motion", "scale"], keys);
        Assert.DoesNotContain("GuildOps.Core", source);
    }

    // ---- L'interface ne recalcule pas la victoire ---------------------------------------------------------------------------

    [Fact]
    public void La_victoire_vient_uniquement_de_FinalReport_Success()
    {
        var end = Regex.Replace(Source("Ui/EndScreen.cs"), @"///.*|//.*", "");
        Assert.Contains("ShowsVictory = r.Success;", end);
        Assert.DoesNotMatch(new Regex(@">=|<="), end);                       // aucune comparaison de seuil
        Assert.DoesNotMatch(new Regex(@"\.(Guild|Session)\.(Reputation|TreasuryCopper|MissionsSucceeded)"), end);   // lit le rapport, pas la guilde
        // Le déclenchement ne dépend que de la présence d'un rapport de Core.
        Assert.Contains("if (t.Report is { } final)", Source("GameShell.cs"));
    }

    // ---- Texte de l'aide ------------------------------------------------------------------------------------------------------

    [Fact]
    public void L_aide_a_quatre_pages_fideles_aux_regles()
    {
        var session = TestSupport.NewSession();
        var rules = session.Data.Rules;
        var pages = HelpContent.Pages(session);
        Assert.Equal(4, pages.Count);
        Assert.All(pages, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Title));
            Assert.True(p.Body.Length > 120, p.Title);
            Assert.DoesNotMatch(new Regex(@"\{|\}"), p.Body);                                          // aucun gabarit non résolu
            Assert.Equal(Regex.Matches(p.Body, @"\[b\]").Count, Regex.Matches(p.Body, @"\[/b\]").Count); // BBCode équilibré
        });
        var all = string.Join("\n", pages.Select(p => p.Body));
        // Les nombres affichés viennent des règles, pas d'une copie.
        Assert.Contains($"{rules.OffersPerDay} contrats", all);
        Assert.Contains($"au plus {rules.MaxMissionsPerDay} missions par jour", all);
        Assert.Contains($"{rules.HeroCount} aventuriers", all);
        Assert.Contains($"maximum {rules.MaxEnergy}", all);
        Assert.Contains($"Au jour {rules.Days}", all);
        Assert.Contains($"réputation d'au moins {rules.Victory.MinReputation}", all);
        Assert.Contains($"trésorerie d'au moins {session.Money(rules.Victory.MinTreasuryCopper)}", all);
        Assert.Contains($"au moins {rules.Victory.MinSucceededMissions} missions réussies", all);
        Assert.Contains($"{rules.RegenerationReputationCost} points de réputation", all);
        // Les quatre illustrations du tutoriel existent.
        for (var i = 1; i <= 4; i++) Assert.True(File.Exists(Path.Combine(Game, "Assets", "b5_3", "tutorial", $"step_{i}_192x128.png")));
    }

    [Fact]
    public void Le_rappel_clavier_est_commun_a_l_aide_et_aux_parametres()
    {
        Assert.Contains("F1", HelpContent.KeyboardLine);
        Assert.Contains("Échap", HelpContent.KeyboardLine);
        Assert.Contains("HelpContent.Keyboard", Source("Ui/SettingsScreen.cs"));
        Assert.Contains("HelpContent.KeyboardLine", Source("Ui/HelpScreen.cs"));
    }

    // ---- Projet et distribution -------------------------------------------------------------------------------------------------

    [Fact]
    public void Le_projet_declare_icone_resolution_logique_et_exports_Linux_et_Windows()
    {
        var project = Source("project.godot");
        Assert.Contains("config/icon=\"res://Assets/b5_3/branding/app_icon_256.png\"", project);
        Assert.True(File.Exists(Path.Combine(Game, "Assets", "b5_3", "branding", "app_icon_256.png")));
        Assert.Contains("window/size/viewport_width=640", project);
        Assert.Contains("window/size/viewport_height=360", project);
        Assert.Contains("window/stretch/scale_mode=\"integer\"", project);
        Assert.Contains("default_texture_filter=0", project);
        Assert.Contains("run/main_scene=\"res://Main.tscn\"", project);
        var presets = Source("export_presets.cfg");
        Assert.Contains("name=\"Linux x64\"", presets);
        Assert.Contains("name=\"Windows x64\"", presets);
        Assert.Equal(2, Regex.Matches(presets, @"binary_format/embed_pck=true").Count);
        Assert.Equal(2, Regex.Matches(presets, @"include_filter=""Assets/fonts/OFL-\*\.txt""").Count);
    }
}
