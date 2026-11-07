# Phase B1 — rapport (jours, énergie, limites, bilan)

Branche `feat/phase-b1-days-and-assessment`, **empilée sur la Phase A** (non fusionnée). Même source de contrat que la Phase A (`75764b0`).

## Livré
- `GameSession.EndDay()` : refusée sans mutation s'il reste une mission engagée non résolue (`PendingMissions`) ; les offres non acceptées passent à `Expired` ; jour suivant : compteur quotidien remis à 0, **+30 énergie plafonné à 100**, 4 nouvelles offres générées sur la **réputation actuelle** (Médor garanti si besoin). Aucune énergie récupérée au jour 1.
- Limite de 3 missions par jour (déjà en A) levée au jour suivant. Historique de toutes les offres conservé (`Offers`), offres du jour via `CurrentOffers`.
- Bilan après le jour 7 : `FinalReport` (réputation ≥ 55, trésorerie ≥ 1 000 cuivres, ≥ 8 missions réussies ; seuils lus dans `rules.v0.1.json`, inclusifs, critère par critère). Échec = « bilan inachevé », **pas de game-over** : état conservé, toute commande refusée (`GameFinished`) jusqu'au `Reset()`.
- **`regenerateDayOnce`** (décidé avec le formateur) : `RegenerateOffers()` redemande le tableau de la journée en cours, **une seule fois par partie**, **n'importe quel jour**, **seulement si aucune mission n'est engagée ce jour-là**, pour **−2 réputation** ; le nouveau tirage prolonge le flux seedé et utilise la réputation *après* coût (garantie Médor incluse). Offres remplacées : expirées, conservées dans l'historique. Second appel ou après fin de partie : refusé sans mutation ; un refus ne consomme pas le droit ; `Reset()` le rend.
- UI Godot : bouton « Terminer la journée », jour x/7, bilan affiché ; toujours aucune règle dans la scène.

## Preuves exécutées (Linux x64, cette machine)
- `dotnet test` : **83/83** (50 de la Phase A + 33 nouveaux), Core sans Godot. Couvre T22, T14, T20 (offres futures seulement), seuils du bilan aux bornes (±1), refus après fin de partie, reprise après échec, déterminisme d'une semaine entière, invariants (caisse ≥ 0, réputation 0–100, énergie 0–100, ≤ 3 missions/jour) à chaque commande d'un robot sur 150 graines.
- Build du projet Godot (`net10.0`, Godot.NET.Sdk 4.7.2) et démarrage `--headless` de Godot 4.7.2 .NET : OK. **UI toujours jamais vue ni cliquée** (pas d'affichage), pas d'export.

## Constat d'équilibrage (mesure, rien n'a été réglé)
Un robot « glouton » déterministe (maximise chance × prime − engagement, 3 missions/jour) sur 300 graines : **287 victoires (95,7 %)**, ~15,4 missions réussies, réputation moyenne 73, trésorerie moyenne ≈ 2 491, trésorerie minimale 1 238. La baseline est donc **très facile** pour un joueur compétent. Un joueur inactif échoue (0 mission). Aucune trajectoire « raisonnable mais perdante » n'est encore démontrée ; T27 n'est donc pas clos. Le contrat impose d'ajuster par PR d'équilibrage séparée avec test seedé avant/après : **à arbitrer par le formateur**, non modifié ici.

## Choix NON validés
1. `EndDay` impose de résoudre les missions engagées (lecture de « toutes les missions acceptées sont résolues dans la même journée ») ; il n'y a pas d'expiration automatique.
2. **Écart au contrat** : le coût de −2 réputation n'existe pas dans le JSON formateur ; j'ai ajouté `game.regenerateDayReputationCost: 2` à `game-data/rules.v0.1.json` (copie locale). À répercuter dans le dépôt formateur. Lecture composite de tes réponses : « rejouer une fois par partie » + « re-tirage du tableau » (les offres du jour sont redonnées, pas l'état des missions). Un re-tirage peut, rarement, redonner les mêmes modèles.
3. Le bilan est établi à `EndDay()` du jour 7 ; le jour reste 7 après la fin.
4. Un héros arrive à 100 d'énergie maximum ; le plafond est le `maxEnergy` des règles.
5. Les offres du jour suivant sont filtrées sur l'énergie réelle des héros ; en cas de héros épuisés, seul le repli documenté en Phase A s'applique (moins d'offres, Médor toujours publié).

## Hors périmètre (B2+)
Réclamations et incidents, storylets, effets de traits, benchmark, playtest perdant de référence, dette volontaire.
