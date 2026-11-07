# Phase A — rapport de PR (tranche verticale saine)

Source du contrat : branche `docs/guildops-v01-contract-claude` du dépôt formateur, commit `75764b0` (**PR de contrat non fusionnée au moment du développement** ; baseline proposée).

## Livré
Démarrer → voir les 12 héros → tableau de 4 offres → sélectionner héros + offre → probabilité expliquée → affecter → résoudre → gains/pertes (cuivres, énergie, réputation) → journal → reset. Hasard injecté (`ISeededRandom`, SplitMix64 maison, indépendant de l'OS). Traits affichés, sans effet (interface `ITraitEffects` prête pour la Phase B).

Hors périmètre (Phase B+) : changement de jour, énergie +30, bilan jour 7, réclamations, storylets, effets de traits, benchmark, dette volontaire (aucune n'est implantée).

## Preuves exécutées (Linux x64, cette machine uniquement)
- `dotnet test` : **50/50 verts**, Core seul, sans Godot.
- `dotnet build src/GuildOps.Game` : OK en `net10.0` avec `Godot.NET.Sdk 4.7.2`, 0 avertissement.
- Éditeur `Godot 4.7.2 stable mono` en `--headless` : build C# OK, `Main._Ready` exécutée (« GuildOps prêt : 12 aventuriers, 4 offres, graine 1142026 »).
- Les tests de règles T01–T09, T11–T15, T21–T26 du doc 05 sont couverts ; T10, T16–T20, T27, T28 relèvent de la Phase B.

## Non fait / non prouvé
- **Aucun export desktop** (templates d'export non installés). Windows non testé. Pas de test réseau coupé réel.
- **UI jamais vue ni cliquée** : pas d'affichage graphique disponible, donc pas de capture. Seul le démarrage headless est prouvé.
- Pas de playtest victoire/échec (nécessite la Phase B).

## Choix NON validés (à arbitrer)
1. `game-data/quest-catalogue.v0.1.json` : **20 modèles rédigés par moi**, dans les bornes de `categoryParameters`. Contenu, titres et répartition par compétence non validés.
2. « `cheapestEligibleHeroBaseCost` » interprété comme le coût d'engagement minimal parmi les 12 héros (10 cuivres).
3. Seuil de réputation « < 40 » extrait par regex du texte `guaranteedRecoveryOffer.when` (aucun champ numérique dédié dans le JSON).
4. Repli si aucun modèle réalisable pour les catégories pondérées : **moins d'offres** (jamais une catégorie interdite par le palier).
5. Tirage de catégorie puis de modèle (uniforme, catalogue trié par id) ; tirage de résolution `NextInt(100)` dans le même flux que la génération (reproductible pour une même suite de commandes).
6. Accepter une offre = affecter un héros dans la même commande ; énergie débitée à la résolution, coût d'engagement à l'affectation.
7. Limite de 3 missions/jour appliquée dès la Phase A.
8. Les valeurs figées des tests (« pinned ») sont des tests de caractérisation de mon implémentation, sauf SplitMix64, vérifié contre sa sortie de référence publiée.

## Divergences relevées dans le contexte
- Roster (`traitId`, ex. `observer-puis-automatiser`) et balancing (`traitKey`, ex. `investigation-automation`) utilisent des identifiants de trait différents ; les deux sont conservés, sans effet.
- Le roster donne une compétence par héros alors que `02-regles-metier` parle d'un ensemble de compétences : une seule est utilisée.
- Le contrat et `CLAUDE.md` de `main` (formateur) divergent : `main` ne contient pas encore docs 14–17 ni les JSON de règles.
