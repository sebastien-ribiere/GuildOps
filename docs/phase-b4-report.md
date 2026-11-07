# Phase B4 — rapport (benchmark, parties de référence, mesures d'équilibrage)

Branche `feat/phase-b4-benchmark-playtests`, créée depuis `main` après la fusion de la PR #5 (B3). Contrat : `75764b0` du dépôt formateur (T27, `performance.catalogueSizesForBenchmark`).

## Décisions prises avec le formateur pour B4
| Sujet | Décision |
| --- | --- |
| Valeurs de difficulté | **Mesurer seulement, ne rien régler** : aucune valeur de `game-data/` n'est modifiée dans cette PR |
| Forme du benchmark | **Tests xUnit marqués lents** (ignorés par défaut, `GUILDOPS_SLOW=1`) |
| Ce que mesure le benchmark | Génération des offres · meilleure affectation (modèles × héros) · partie complète sur 7 jours |

## Livré
- **T27 — parties de référence** (`PlaytestTests`, rapides, dans la suite normale) : sur la **graine officielle 1142026**, trois parties du robot glouton qui ne diffèrent que par la réponse aux réclamations. Journaux complets versionnés dans `docs/playtests/`.
- **Outil de mesure d'équilibrage** (`BalanceReportTests`, lent) : tableau comparatif sur 1 000 graines par cellule, quatre variantes de données × trois politiques de réclamation, avec la **cause** de chaque défaite (réputation / trésorerie / missions).
- **Benchmark** (`BenchmarkTests`, lent) : catalogue synthétique déterministe (graine 42, bornes de chaque catégorie respectées, vérifié par un test), tailles lues dans `rules.v0.1.json`, un échauffement + 15 mesures, médiane. Protocole identique avant/après.
- `ComplaintPolicy` (Ignore / Apologize / RepairIfPossible) ajouté au robot glouton des tests.
- README : section « Benchmark et équilibrage ».

## Parties de référence (graine 1142026, déterministes, épinglées par test)
| Réponse aux réclamations | Bilan | Réputation (≥ 55) | Caisse (≥ 10 or) | Succès (≥ 8) |
| --- | --- | ---: | ---: | ---: |
| Répare si possible | **Redressement réussi** | 64 | 17 or 7 cuivres | 15 |
| Excuses partout | Bilan inachevé | **54** (un point de moins) | 19 or 67 cuivres | 15 |
| Ignore tout | Bilan inachevé | 37 | 13 or 97 cuivres | 15 |
Ce sont 5 réclamations dans les trois cas : **la seule décision de service client fait basculer l'issue** sur la même graine. Sans rien faire pendant sept jours, la partie se perd aussi sans erreur.

## Mesures d'équilibrage — robot glouton, 1 000 graines par cellule (machine de dev, Linux x64)
| Variante de données | Ignore | Excuses | Répare si possible | Incidents/partie |
| --- | ---: | ---: | ---: | ---: |
| **Baseline actuelle** (8 storylets + traits) | 29,5 % | 70,6 % | 93,1 % | 6,4–6,7 |
| Sans traits | 37,9 % | 72,3 % | 92,9 % | 5,4–6,1 |
| E05 et E08 à 15 % (au lieu de 30 %) | 53,9 % | 83,4 % | 94,6 % | 4,2–4,4 |
| 2 storylets seulement (état B2), traits | 89,3 % | 94,9 % | 95,9 % | ~1 |

### Constats
1. **La réputation est quasi la seule condition de victoire qui compte** : 100 % des défaites des politiques « Ignore » et « Excuses » viennent de la réputation ; la trésorerie n'intervient que dans 11 défaites sur 69 en « Répare ». Le seuil de 1 000 cuivres (10 or) est presque acquis dès le départ (caisse initiale 1 200), et le robot réussit environ 15 missions pour un seuil de 8.
2. **La difficulté dépend presque entièrement du nombre d'incidents** (donc de E05 et E08, les deux storylets à 30 % les plus fréquents), pas des traits (qui déplacent le taux de victoire de 0 à 8 points selon la politique).
3. **Les trois politiques sont bien séparées** (30 % / 71 % / 93 %) : le choix de réponse aux réclamations compte, c'est l'effet voulu. Mais un joueur qui répare systématiquement gagne presque toujours.
4. Le robot n'utilise ni la préparation de Jean-Michel ni l'adaptation des héros aux traits : ces mesures sont un **plancher** pour un joueur attentif, pas une mesure de la difficulté pour un humain.

### Leviers de réglage possibles (NON appliqués, à arbitrer)
- Probabilités de E05 / E08 (mesuré : 30 % → 15 % fait passer « Ignore » de 29,5 % à 53,9 %).
- Seuils de victoire : le seuil de trésorerie (10 or) et le seuil de missions (8) ne contraignent presque rien ; les remonter donnerait une deuxième dimension à la partie. **Non mesuré**, et ces seuils viennent du contrat (`rules.v0.1.json`) : toute modification passe par une décision datée du formateur.
- Coûts de réparation des storylets, ou dommage collatéral (−4) : non mesurés.

## Benchmark (machine de dev : Ubuntu 24.04, X64, 20 cœurs, .NET 10.0.14 ; médiane de 15 mesures)
| Modèles | Génération d'un tableau | Meilleure affectation (modèles × 12 héros) | Partie complète, 7 jours |
| ---: | ---: | ---: | ---: |
| 100 | 0,5 ms | 0,3 ms | 8,4 ms |
| 1 000 | 2,7 ms | 1,5 ms | 23,4 ms |
| 10 000 | 28,0 ms | 15,9 ms | 206 ms |
Croissance à peu près linéaire avec la taille du catalogue. Aucun de ces temps n'est un problème pour le jeu ; ils servent de **référence propre avant l'injection des défauts (Phase C)**. Les tests n'imposent aucun seuil de temps absolu (instable d'une machine à l'autre) : ils vérifient la cohérence des résultats (4 offres distinctes, partie terminée, meilleure affectation trouvée).

## Preuves exécutées (Linux x64, cette machine)
- `dotnet test` : 153 réussis, 3 ignorés (les trois tests lents), 0 échec. `GUILDOPS_SLOW=1 dotnet test --filter "Category=Slow"` : 3/3 réussis (environ 25 s).
- Godot non relancé : aucun fichier de `src/GuildOps.Game` ni de `game-data/` n'a changé dans cette PR.

## Limites et choix NON validés
1. Un seul joueur automatique (glouton) : c'est mon choix de critère, pas une stratégie validée par le formateur. Les trois politiques de réclamation sont également de mon fait.
2. Le catalogue synthétique est tiré au hasard dans les bornes de chaque catégorie ; il ne reflète pas la distribution du vrai contenu.
3. Les temps ne portent que sur le Core (pas Godot, pas l'interface) et sur un seul poste ; les postes CESI ne sont pas mesurés.
4. « Trajectoire raisonnable perdante » (T27) : j'ai retenu « ignorer les réclamations », qui est plausible mais reste ma définition.
5. Le journal des parties de référence est une trace de ce moteur ; il changera si une PR modifie les données ou l'ordre des tirages.

## Hors périmètre
Réglage des valeurs de difficulté (en attente d'arbitrage), Phase C (dette intentionnelle, seulement après relecture de la base propre).
