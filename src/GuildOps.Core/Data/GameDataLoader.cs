using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using GuildOps.Core.Domain;

namespace GuildOps.Core.Data;

/// <summary>Charge et valide les JSON. Toute incohérence lève une GameDataException explicite (pas de valeur par défaut silencieuse).</summary>
public static partial class GameDataLoader
{
    private const string ResourcePrefix = "GuildOps.GameData.";

    public static GameData LoadEmbedded() => Load(
        ReadEmbedded("rules.v0.1.json"),
        ReadEmbedded("hero-balancing.v0.1.json"),
        ReadEmbedded("roster.v0.1.json"),
        ReadEmbedded("quest-catalogue.v0.1.json"));

    public static GameData LoadFromDirectory(string directory) => Load(
        File.ReadAllText(Path.Combine(directory, "rules.v0.1.json")),
        File.ReadAllText(Path.Combine(directory, "hero-balancing.v0.1.json")),
        File.ReadAllText(Path.Combine(directory, "roster.v0.1.json")),
        File.ReadAllText(Path.Combine(directory, "quest-catalogue.v0.1.json")));

    public static GameData Load(string rulesJson, string balancingJson, string rosterJson, string catalogueJson)
    {
        try
        {
            using var rules = JsonDocument.Parse(rulesJson);
            using var balancing = JsonDocument.Parse(balancingJson);
            using var roster = JsonDocument.Parse(rosterJson);
            using var catalogue = JsonDocument.Parse(catalogueJson);

            var gameRules = ParseRules(rules.RootElement);
            var heroes = ParseHeroes(balancing.RootElement, roster.RootElement, gameRules);
            var templates = ParseTemplates(catalogue.RootElement, gameRules);
            return new GameData(gameRules, heroes, templates);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new GameDataException($"Données de jeu invalides : {e.Message}");
        }
    }

    private static string ReadEmbedded(string name)
    {
        var assembly = typeof(GameDataLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new GameDataException($"Ressource embarquée introuvable : {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static GameRules ParseRules(JsonElement r)
    {
        var guild = r.GetProperty("initialGuild");
        var game = r.GetProperty("game");
        var rep = r.GetProperty("reputation");
        var gen = r.GetProperty("offerGeneration");
        var res = r.GetProperty("missionResolution");
        var delta = rep.GetProperty("delta");

        var order = gen.GetProperty("categories").EnumerateArray()
            .Select(c => ParseCategory(c.GetString()!)).ToList();

        var tiers = rep.GetProperty("tiers").EnumerateArray()
            .Select(t => new ReputationTier(t.GetProperty("id").GetString()!, t.GetProperty("min").GetInt32(), t.GetProperty("max").GetInt32()))
            .ToList();

        var weights = new Dictionary<string, IReadOnlyList<int>>();
        foreach (var p in gen.GetProperty("weightsPercentByReputationTier").EnumerateObject())
            weights[p.Name] = p.Value.EnumerateArray().Select(x => x.GetInt32()).ToList();

        var categories = new Dictionary<QuestCategory, CategoryParameters>();
        foreach (var p in r.GetProperty("categoryParameters").EnumerateObject())
        {
            var c = p.Value;
            categories[ParseCategory(p.Name)] = new CategoryParameters(
                c.GetProperty("rewardCopperMin").GetInt32(), c.GetProperty("rewardCopperMax").GetInt32(),
                c.GetProperty("energyCostMin").GetInt32(), c.GetProperty("energyCostMax").GetInt32(),
                c.GetProperty("levelMin").GetInt32(), c.GetProperty("levelMax").GetInt32(),
                c.GetProperty("riskPenaltyPp").GetInt32(),
                c.GetProperty("waiveHeroEngagementCost").GetBoolean());
        }

        var recovery = gen.GetProperty("guaranteedRecoveryOffer");
        var when = recovery.GetProperty("when").GetString()!;
        var match = RecoveryThresholdRegex().Match(when);
        if (!match.Success) throw new GameDataException($"Seuil de réputation introuvable dans guaranteedRecoveryOffer.when : « {when} »");

        var rules = new GameRules(
            guild.GetProperty("treasuryCopper").GetInt32(),
            guild.GetProperty("reputation").GetInt32(),
            guild.GetProperty("day").GetInt32(),
            r.GetProperty("currency").GetProperty("copperPerGold").GetInt32(),
            r.GetProperty("heroes").GetProperty("count").GetInt32(),
            r.GetProperty("heroes").GetProperty("maxEnergy").GetInt32(),
            game.GetProperty("offersPerDay").GetInt32(),
            game.GetProperty("maxMissionsPerDay").GetInt32(),
            game.GetProperty("days").GetInt32(),
            game.GetProperty("energyRegenerationAtDayStart").GetInt32(),
            new VictoryConditions(
                game.GetProperty("victory").GetProperty("minimumTreasuryCopper").GetInt32(),
                game.GetProperty("victory").GetProperty("minimumReputation").GetInt32(),
                game.GetProperty("victory").GetProperty("minimumSucceededMissions").GetInt32()),
            rep.GetProperty("min").GetInt32(),
            rep.GetProperty("max").GetInt32(),
            tiers, weights, order, categories,
            new ReputationDeltas(
                delta.GetProperty("regularSuccess").GetInt32(), delta.GetProperty("communitySuccess").GetInt32(),
                delta.GetProperty("regularFailure").GetInt32(), delta.GetProperty("communityFailure").GetInt32()),
            new SuccessFormula(
                res.GetProperty("baseSuccessPercent").GetInt32(), res.GetProperty("levelBonusPerAdditionalLevelPp").GetInt32(),
                res.GetProperty("lowEnergyMultiple").GetInt32(), res.GetProperty("lowEnergyPenaltyPp").GetInt32(),
                res.GetProperty("minSuccessPercent").GetInt32(), res.GetProperty("maxSuccessPercent").GetInt32()),
            recovery.GetProperty("templateId").GetString()!,
            int.Parse(match.Groups[1].Value),
            r.GetProperty("rng").GetProperty("testSeed").GetInt64());

        ValidateRules(rules);
        return rules;
    }

    private static void ValidateRules(GameRules rules)
    {
        if (rules.CopperPerGold <= 0) throw new GameDataException("copperPerGold doit être > 0.");
        if (rules.OffersPerDay <= 0 || rules.MaxMissionsPerDay <= 0) throw new GameDataException("offersPerDay et maxMissionsPerDay doivent être > 0.");
        if (rules.Days < 1 || rules.EnergyRegenerationAtDayStart < 0) throw new GameDataException("days doit être >= 1 et la régénération d'énergie >= 0.");
        if (rules.Victory.MinTreasuryCopper < 0 || rules.Victory.MinSucceededMissions < 0
            || rules.Victory.MinReputation < rules.ReputationMin || rules.Victory.MinReputation > rules.ReputationMax)
            throw new GameDataException("Conditions de victoire invalides.");
        if (rules.InitialTreasuryCopper < 0) throw new GameDataException("Trésorerie initiale négative.");
        if (rules.InitialReputation < rules.ReputationMin || rules.InitialReputation > rules.ReputationMax)
            throw new GameDataException("Réputation initiale hors plage.");

        // Les paliers doivent couvrir [min, max] sans trou ni chevauchement.
        var expected = rules.ReputationMin;
        foreach (var tier in rules.Tiers.OrderBy(t => t.Min))
        {
            if (tier.Min != expected) throw new GameDataException($"Palier « {tier.Id} » : trou ou chevauchement à {expected}.");
            expected = tier.Max + 1;
        }
        if (expected != rules.ReputationMax + 1) throw new GameDataException("Les paliers ne couvrent pas toute la plage de réputation.");

        foreach (var tier in rules.Tiers)
        {
            if (!rules.CategoryWeightsByTier.TryGetValue(tier.Id, out var w))
                throw new GameDataException($"Pondérations manquantes pour le palier « {tier.Id} ».");
            if (w.Count != rules.CategoryOrder.Count || w.Sum() != 100 || w.Any(x => x < 0))
                throw new GameDataException($"Pondérations invalides pour « {tier.Id} » (attendu {rules.CategoryOrder.Count} valeurs >= 0 sommant à 100).");
        }
        foreach (var c in rules.CategoryOrder)
            if (!rules.Categories.ContainsKey(c)) throw new GameDataException($"categoryParameters manquant pour {c}.");

        var f = rules.Formula;
        if (f.MinSuccessPercent < 0 || f.MaxSuccessPercent > 100 || f.MinSuccessPercent > f.MaxSuccessPercent)
            throw new GameDataException("Bornes de probabilité de réussite invalides.");
    }

    private static IReadOnlyList<HeroDefinition> ParseHeroes(JsonElement balancing, JsonElement roster, GameRules rules)
    {
        var rosterById = roster.GetProperty("heroes").EnumerateArray().ToDictionary(h => h.GetProperty("id").GetString()!);
        var heroes = new List<HeroDefinition>();

        foreach (var b in balancing.GetProperty("levelsAndCosts").EnumerateArray())
        {
            var id = b.GetProperty("heroId").GetString()!;
            if (!rosterById.TryGetValue(id, out var h)) throw new GameDataException($"Héros « {id} » absent du roster.");

            var level = b.GetProperty("level").GetInt32();
            var energy = b.GetProperty("startingEnergy").GetInt32();
            var cost = b.GetProperty("baseCostCopper").GetInt32();
            if (level is < 1 or > 5) throw new GameDataException($"Héros « {id} » : niveau hors 1..5.");
            if (energy < 0 || energy > rules.MaxEnergy) throw new GameDataException($"Héros « {id} » : énergie initiale hors 0..{rules.MaxEnergy}.");
            if (cost < 0) throw new GameDataException($"Héros « {id} » : coût négatif.");

            heroes.Add(new HeroDefinition(
                id, h.GetProperty("name").GetString()!, h.GetProperty("role").GetString()!,
                ParseSkill(h.GetProperty("skill").GetString()!),
                level, energy, cost,
                // Divergence connue : le roster (traitId) et l'équilibrage (traitKey) utilisent deux identifiants différents.
                // Les deux sont conservés tels quels ; aucun n'a d'effet mécanique en Phase A.
                h.GetProperty("traitId").GetString()!, b.GetProperty("traitKey").GetString()!,
                h.GetProperty("trait").GetString()!, h.GetProperty("advantage").GetString()!, h.GetProperty("drawback").GetString()!,
                h.GetProperty("accessory").GetString()!, h.GetProperty("line").GetString()!));
        }

        if (heroes.Count != rules.HeroCount) throw new GameDataException($"{rules.HeroCount} héros attendus, {heroes.Count} trouvés.");
        if (heroes.Select(h => h.Id).Distinct().Count() != heroes.Count) throw new GameDataException("Identifiants de héros dupliqués.");
        if (rosterById.Count != heroes.Count) throw new GameDataException("Le roster et l'équilibrage ne décrivent pas les mêmes héros.");
        return heroes;
    }

    private static IReadOnlyList<QuestTemplate> ParseTemplates(JsonElement catalogue, GameRules rules)
    {
        var list = new List<QuestTemplate>();
        foreach (var t in catalogue.GetProperty("templates").EnumerateArray())
        {
            var template = new QuestTemplate(
                t.GetProperty("id").GetString()!, t.GetProperty("title").GetString()!, t.GetProperty("description").GetString()!,
                ParseCategory(t.GetProperty("category").GetString()!), ParseSkill(t.GetProperty("skill").GetString()!),
                t.GetProperty("minLevel").GetInt32(), t.GetProperty("energyCost").GetInt32(), t.GetProperty("rewardCopper").GetInt32());

            var p = rules.Categories[template.Category];
            if (template.RewardCopper < p.RewardCopperMin || template.RewardCopper > p.RewardCopperMax
                || template.EnergyCost < p.EnergyCostMin || template.EnergyCost > p.EnergyCostMax
                || template.MinLevel < p.LevelMin || template.MinLevel > p.LevelMax)
                throw new GameDataException($"Modèle « {template.Id} » hors des bornes de la catégorie {template.Category}.");
            list.Add(template);
        }

        if (list.Select(t => t.Id).Distinct().Count() != list.Count) throw new GameDataException("Identifiants de modèles dupliqués.");
        list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

        if (list.All(t => t.Id != rules.RecoveryTemplateId))
            throw new GameDataException($"Modèle de redressement « {rules.RecoveryTemplateId} » absent du catalogue.");
        return list;
    }

    private static QuestCategory ParseCategory(string s) => s switch
    {
        "corvee" => QuestCategory.Corvee,
        "village" => QuestCategory.Village,
        "aventure" => QuestCategory.Aventure,
        "notable" => QuestCategory.Notable,
        "royale" => QuestCategory.Royale,
        _ => throw new GameDataException($"Catégorie inconnue : « {s} »."),
    };

    private static Skill ParseSkill(string s) => s switch
    {
        "combat" => Skill.Combat,
        "magie" => Skill.Magie,
        "exploration" => Skill.Exploration,
        "ingenierie" => Skill.Ingenierie,
        _ => throw new GameDataException($"Compétence inconnue : « {s} »."),
    };

    [GeneratedRegex(@"reputation\s*<\s*(\d+)")]
    private static partial Regex RecoveryThresholdRegex();
}
