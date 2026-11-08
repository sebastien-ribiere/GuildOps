using GuildOps.Core.Engine;

namespace GuildOps.Game.Ui;

/// <summary>Une page du tutoriel : titre, texte (BBCode) et légende éventuelle de l'illustration.</summary>
public sealed record HelpPage(string Title, string Body, string? Caption = null);

/// <summary>
/// Texte de l'aide, à UN seul endroit pour rester facile à modifier (jamais incrusté dans les PNG). Les nombres (jours, énergie, objectifs du
/// Conseil royal…) sont lus dans <c>rules</c> de la session : rien n'est recopié ni recalculé ici. Présentation pure.
/// </summary>
public static class HelpContent
{
    /// <summary>Rappel des commandes clavier, repris par l'aide et par les paramètres.</summary>
    public static readonly string[] Keyboard =
    [
        "Tab / Maj+Tab ou flèches : changer de contrôle",
        "Entrée ou Espace : valider le contrôle sélectionné",
        "Échap : fermer l'écran ouvert, sinon retourner au hall",
        "F1 : ouvrir l'aide",
    ];

    public const string KeyboardLine = "Clavier — Tab ou flèches : changer de contrôle · Entrée : valider · Échap : fermer ou retour au hall · F1 : aide";

    public static IReadOnlyList<HelpPage> Pages(GameSession s)
    {
        var rules = s.Data.Rules;
        var v = rules.Victory;
        var renew = rules.RegenerateDayOnce
            ? $"\n\nUne seule fois par partie, vous pouvez [b]renouveler le tableau[/b] contre {rules.RegenerationReputationCost} points de réputation."
            : "";
        return
        [
            new HelpPage("1. Le tableau des contrats",
                $"Chaque jour, le tableau propose {rules.OffersPerDay} contrats (salle [b]Contrats[/b]). Chaque offre indique sa catégorie, la compétence " +
                "et le niveau minimum demandés, l'énergie à dépenser et la prime.\n\n" +
                "Sélectionnez une offre (clic, ou Entrée au clavier) : le panneau de droite vous guide pas à pas. " +
                $"Vous pouvez engager au plus {rules.MaxMissionsPerDay} missions par jour." + renew),

            new HelpPage("2. Aventuriers et compétences",
                $"Votre guilde compte {rules.HeroCount} aventuriers (salle [b]Aventuriers[/b]). Chacun a une compétence (Combat, Magie, Exploration ou Ingénierie), " +
                $"un niveau, une énergie (maximum {rules.MaxEnergy}) et un coût d'engagement.\n\n" +
                "Pour une offre donnée, un héros doit avoir la bonne compétence, le niveau minimum et assez d'énergie. Sélectionnez-le : " +
                "l'aperçu affiche la chance de réussite calculée par le jeu, ou la raison précise du refus.\n\n" +
                "L'énergie se récupère au début de chaque jour."),

            new HelpPage("3. Affecter et résoudre une mission",
                "Choisissez une offre, puis un héros, puis [b]Affecter le héros[/b]. Selon ses traits, un héros peut aussi se préparer à une catégorie " +
                "de contrats avant la mission.\n\n" +
                "Dans la salle [b]Missions[/b], [b]Résoudre la mission[/b] joue la mission : le résultat dépend d'un tirage contre la chance affichée. " +
                "Un succès n'est jamais garanti, et un échec fait partie du jeu.\n\n" +
                "Lisez ensuite le verdict : prime, énergie, réputation, et parfois un incident.",
                "Illustration d'exemple : elle ne prédit aucune issue."),

            new HelpPage("4. Réclamations, réputation et Conseil royal",
                "Un incident ouvre une réclamation (salle [b]Réclamations[/b]) : plusieurs réponses sont possibles, chacune annonce son coût et son effet " +
                "sur la réputation avant que vous ne confirmiez.\n\n" +
                $"Quand vous avez fini, [b]Terminer la journée[/b] (salle [b]Trésorerie[/b]). Au jour {rules.Days}, le Conseil royal rend son bilan. Objectifs :\n" +
                $"- réputation d'au moins {v.MinReputation} ;\n" +
                $"- trésorerie d'au moins {s.Money(v.MinTreasuryCopper)} ;\n" +
                $"- au moins {v.MinSucceededMissions} missions réussies.\n" +
                "La partie est gagnée si les trois sont atteints."),
        ];
    }
}
