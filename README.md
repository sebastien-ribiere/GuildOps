# GuildOps

Jeu de gestion de guilde fantasy (Godot 4.7.2 .NET, C# / .NET 10), 100 % hors ligne. État : **Phase B3** (7 jours, réclamations, 8 storylets, 12 traits actifs). Rapports : `docs/phase-*-report.md`.

## Structure
- `src/GuildOps.Core` — moteur C# pur (aucune dépendance Godot). Données JSON embarquées depuis `game-data/`.
- `src/GuildOps.Game` — projet Godot (UI seulement, aucune règle métier). Ouvrir avec l'éditeur **Godot 4.7.2 stable .NET** (pas l'édition standard).
- `tests/GuildOps.Tests` — xUnit.
- `game-data/` — `rules`, `hero-balancing`, `roster` (copies du contrat V0.1), `quest-catalogue` et `storylets` (**proposés, non validés**).

## Commandes
```bash
dotnet test                       # tests du Core, sans Godot (ajouter --no-restore une fois les packages restaurés)
dotnet build src/GuildOps.Game    # compile le projet Godot (Godot.NET.Sdk 4.7.2, net10.0)
godot --path src/GuildOps.Game    # lance le jeu (binaire Godot .NET)
```
`GuildOps.Game` n'est volontairement pas dans `GuildOps.sln` : `dotnet test` ne dépend ainsi ni de Godot ni du paquet `Godot.NET.Sdk`.

## Benchmark et équilibrage (tests lents, ignorés par défaut)
```bash
GUILDOPS_SLOW=1 dotnet test --filter "Category=Slow"
```
Écrit `artifacts/benchmark-latest.md` (génération d'offres, meilleure affectation, partie complète sur 100 / 1 000 / 10 000 modèles de quêtes), `artifacts/balance-latest.md` (taux de victoire du robot glouton par variante de données et par réponse aux réclamations) et `artifacts/playtests/` (journaux des parties de référence). Les journaux de la graine officielle sont versionnés dans `docs/playtests/`. `artifacts/` n'est pas versionné ; les temps ne se comparent que sur une même machine.

## Hors ligne
Aucun appel réseau, compte ou service. Seule la préparation (`dotnet restore`) nécessite Internet ; voir le kit hors ligne du dossier formateur.
