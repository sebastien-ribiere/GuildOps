# Phase B2 — rapport (réclamations et 2 storylets)

Branche `feat/phase-b2-complaints-storylets`, créée depuis `main` (Phase A et B1 fusionnées). Contrat : `75764b0` du dépôt formateur, plus les décisions de travail ci-dessous.

## Décisions prises avec le formateur (pendant cette session, à consigner dans le dépôt formateur)
| Sujet | Décision |
| --- | --- |
| Déclencheur d'un incident | Storylet à conditions de données **+ probabilité** tirée dans le flux seedé et lisible dans les données |
| Les 2 storylets | **E01** (succès de Gérard Dev sur une corvée/mission de village → mur porteur, 40 %, réparation 80 cuivres) et **E02** (échec de « Le Wi-Fi du dragon » → routeur grillé, 60 %, réparation 120). « Le blason royal à l'envers » est **exclu** (réservé au TP final) |
| Réclamation non traitée | Reste ouverte, **n'empêche pas** de finir la journée, **expire après 2 jours** (à la fin du jour d'ouverture + 2) : close comme un « nier » (−2) |
| Montant de réparation | **Défini par storylet** |

## Livré
- `game-data/storylets.v0.1.json` : storylets 100 % données (conditions héros / issue / catégorie / modèle, priorité, probabilité, coût, libellés), validés au chargement (héros ou modèle inconnu, probabilité hors 0–100, expiration < 1).
- `StoryletSelector` : au plus **un** storylet par mission (priorité décroissante puis id, premier qui passe son tirage). **Aucun tirage n'est consommé** s'il n'y a pas de storylet éligible.
- Ordre du contrat respecté : mission résolue (prime, énergie, réputation) → dommage collatéral **−4** (journalisé séparément) → ouverture du dossier.
- `ResolveComplaint(id, Repair|Apologize|Deny)` : réparation (−coût, **+3**), excuses (**+1**), refus (**−2**). Une fois seulement (double clic refusé sans mutation) ; réparation impayable refusée sans mutation, excuses et refus restent possibles ; coût exactement égal au solde accepté ; réputation bornée ; la mission n'est jamais requalifiée.
- Expiration automatique à `EndDay()` (avant la génération des offres suivantes, donc la baisse influe sur le tableau du lendemain). Le bilan du jour 7 liste les réclamations encore ouvertes, sans effet.
- UI Godot : liste des réclamations, trois boutons avec libellés et coût, texte de l'incident après la mission.
- Un nouveau storylet s'ajoute **en données seulement** (test dédié) : base du TP final.

## Preuves exécutées (Linux x64, cette machine)
- `dotnet test` : **110/110** (83 précédents + 27), Core sans Godot : T13, T17, T18, T19, T20 couverts ; seuils de probabilité (39 passe, 40 non) ; simulation sur 150 graines avec incidents et traitement des réclamations, invariants à chaque commande, rejeu identique.
- Build Godot `net10.0` + démarrage `--headless` de Godot 4.7.2 .NET : OK. **UI toujours jamais vue ni cliquée** ; pas d'export.

## Mesures (rien n'a été réglé)
Robot glouton, 300 graines, ~0,8 incident par partie : **269 à 286 victoires sur 300** selon la politique de réponse (refus : 269 ; excuses : 282 ; réparer si possible : 286). Les incidents pèsent peu : la baseline reste **facile** (cf. B1). T27 (trajectoire perdante raisonnable) reste ouvert pour une PR d'équilibrage.

## Choix et écarts NON validés
1. Les décisions du tableau ci-dessus n'existent encore que dans cette conversation : à reporter dans `docs/07-decisions.md` du dépôt formateur.
2. `storylets.v0.1.json` est un **nouveau fichier** ; textes et libellés des choix rédigés par moi à partir de `docs/10`.
3. `docs/10` prévoit 0 à 2 choix par storylet ; le contrat impose trois options de réclamation (réparer / excuses / nier) : j'applique le contrat.
4. E01 : « mission de combat en ville » traduit par catégories `corvee` et `village` ; E02 par l'identifiant de modèle `wifi-du-dragon`.
5. Le dommage collatéral −4 s'applique à chaque incident, qu'il vienne d'un succès (E01) ou d'un échec (E02).
6. Une réclamation ouverte au jour 6 ou 7 n'expire jamais avant le bilan.

## Hors périmètre (B3+)
Effets de traits (dont Alice UX sur la réputation des réponses), les 6 autres storylets, benchmark, playtest perdant de référence, dette volontaire.
