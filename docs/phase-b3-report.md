# Phase B3 — rapport (traits, préparation de Jean-Michel, 8 storylets)

Branche `feat/phase-b3-traits-storylets`, créée depuis `feat/phase-b2-complaints-storylets` (PR B2 pas encore fusionnée : **cette branche contient aussi les commits de B2** tant que B2 n'est pas sur `main`). Contrat : `75764b0` du dépôt formateur ; décisions D06 à D09 dans `docs/07-decisions.md` (branche `docs/guildops-decisions-phase-b` du dépôt formateur).

## Décisions prises avec le formateur pour B3
| Sujet | Décision |
| --- | --- |
| Découpage | **Une seule PR** (familles, 12 traits, Jean-Michel, 6 storylets) |
| Périmètre | **Les 12 traits** ont un effet chiffré, via des familles génériques |
| Jean-Michel « observer puis automatiser » | **Action de préparation explicite** : commande `Prepare(héros, catégorie)` qui coûte de l'énergie et des cuivres, puis bonus réutilisable sur cette catégorie |
| Source des valeurs | **En dur dans le Core** (`TraitCatalog`), pas de fichier de données pour les traits |

Écart assumé par rapport au contrat (`docs/15 §5`) : il demande de détailler Jean-Michel « en PR de design/test avant sa mise en œuvre ». Le formateur a choisi une PR unique ; le design est donc dans ce rapport et dans `TraitTests.cs`.

## Les sept familles de modificateurs
`Chance` (points de pourcentage) · `EngagementCost` (cuivres, jamais sous 0, sans effet sur une catégorie exemptée de frais) · `EnergyCost` (jamais sous 1) · `Reward` (% de la prime, arrondi bas, succès seulement) · `IncidentProbability` (points ajoutés à chaque storylet éligible, borné 0–100) · `ComplaintReputation` (points si réparation ou excuses, jamais pour « nier ») · `TeamRecovery` (énergie rendue aux autres héros).
Trois conditions : toujours, `PreparedCategory`, `ExperiencedCategory` (au moins une mission terminée dans la catégorie, succès ou non).
Aucune logique propre à un héros : `TraitMath` évalue des listes de modificateurs. Tous les effets sont **visibles avant l'engagement** (`AssignmentCheck.Notes`, énergie, prime) et le moteur ne crée aucune ressource cachée.

## Catalogue des douze traits — valeurs PROPOSÉES, non validées
| Héros (traitId) | Atout | Contrepartie |
| --- | --- | --- |
| Jean-Michel (`observer-puis-automatiser`) | +12 points de chance sur une catégorie **préparée** (réutilisable) | La préparation coûte 10 énergie + 15 cuivres, hors des 3 missions du jour |
| Gérard (`hotfix-muscle`) | −3 énergie sur corvée/village | +15 points de risque d'incident |
| Merlinda (`tester-avant-de-lancer`) | +6 points de chance | +6 cuivres d'engagement |
| Borborygme (`livrer-avant-hier`) | −4 énergie | +10 points de risque d'incident |
| Archibald (`chemin-dore`) | −4 énergie après une 1re mission de la catégorie | +4 cuivres d'engagement |
| Prudence (`valeur-avant-tout`) | +20 % de prime sur aventure/notable/royale | −6 points de chance sur corvée/village |
| Hermine (`zero-trust`) | −20 points de risque d'incident ; +4 points de chance sur notable/royale | +8 cuivres d'engagement |
| Gaston (`optimiser-chaque-cuivre`) | −6 cuivres d'engagement | −4 points de chance |
| Alice (`ecouter-les-villageois`) | +2 réputation si réclamation réparée ou apaisée | +2 énergie dépensée |
| Léon (`analyser-avant-de-conclure`) | +8 points de chance sur aventure/notable/royale | +3 énergie dépensée |
| Zélie (`elasticite-magique`) | +10 points de chance sur aventure/notable/royale | +12 cuivres d'engagement |
| Clovis (`rituel-de-coordination`) | +3 énergie rendue aux autres après chaque mission | +2 énergie dépensée |

## Huit storylets (E01 à E08)
E01 et E02 inchangés. **E03 à E08 sont des propositions de ma part** (textes, libellés, probabilités, coûts) : E03 Merlinda réussit (35 %, 60 cuivres) · E04 caisse ≤ 300 cuivres (35 %, 50) · E05 deuxième mission résolue le même jour (30 %, 40) · E06 Archibald réussit (40 %, 70) · E07 Borborygme échoue (50 %, 90) · E08 réussite avec réputation ≥ 60 (30 %, 50). Trois conditions optionnelles ajoutées au format : `minReputation`, `maxTreasuryCopper`, `minMissionsResolvedToday`, évaluées après prime et réputation de mission. Toujours au plus un storylet par mission ; « le blason royal » reste exclu. `docs/10` prévoit aussi des storylets de fin de journée (E04, E08 d'origine) : ici ils sont ramenés au déclencheur « mission résolue », seul géré.

## Preuves exécutées (Linux x64, cette machine)
- `dotnet test` : **146/146** (110 précédents + 36). Chaque trait testé contre sa valeur déclarée (comparaison avec la même affectation sans trait) ; prévisualisation = résolution (énergie, prime) ; préparation (coûts, bonus par catégorie, réutilisable, refus atomiques, `Reset`) ; bonus d'Alice sur réparation/excuses mais pas sur « nier » ; Clovis plafonné ; Archibald avant/après expérience ; bornes de probabilité ; conditions E03–E08 ; E05 sur la 2e mission du jour ; rejeu identique ; 150 graines avec invariants.
- Les tests de règles de base utilisent `NoTraitEffects` ; `GameSession.Start` utilise par défaut `TraitCatalog`.
- Build Godot `net10.0` + démarrage `--headless` de Godot 4.7.2 .NET : OK. **UI toujours jamais vue ni cliquée** (nouveau bouton de préparation et lignes de traits non vus) ; pas d'export.

## Mesures (rien n'a été réglé) — robot glouton, 300 graines
| Politique de réclamation | Sans traits | Avec traits |
| --- | --- | --- |
| Ignorer (expiration) | 104 victoires | 84 |
| Excuses | 215 | 193 |
| Réparer si possible | 277 | 281 |

Les incidents passent de ~0,8 à **5 à 6,7 par partie**, surtout à cause de E05 et E08. En B2 le robot gagnait 269–286/300 : **les six nouveaux storylets rendent le jeu nettement plus difficile et font enfin peser les réclamations**. Le robot n'utilise ni préparation ni choix de héros adapté aux traits. C'est une conséquence de storylets non validés, pas un équilibrage : à arbitrer en B4.

## Choix et écarts NON validés
1. Toutes les valeurs de traits et E03–E08 (voir ci-dessus).
2. Le générateur d'offres juge la faisabilité avec les coûts **de base** (sans traits) ; la garantie anti-blocage reste calculée sur le coût de base le moins cher.
3. `traitId` (roster) pilote le catalogue ; `traitKey` (équilibrage) reste conservé mais sans effet. Un trait inconnu est refusé au chargement.
4. L'expérience (Archibald) compte les missions terminées, succès ou échec.
5. Préparer ne peut pas se faire pendant une mission, ne compte pas parmi les 3 missions du jour et dure toute la partie.
6. Un seul trait par héros ; pas de synergies (non confirmées par le formateur).

## Hors périmètre (B4+)
Benchmark 100/1 000/10 000 quêtes, parties de référence gagnante et perdante, PR d'équilibrage, dette volontaire (Phase C).
