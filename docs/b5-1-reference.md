# GuildOps — référence B5.1 (base graphique et fonctionnelle)

> Figée après fusion et validation humaine. Tant que la PR n'est pas fusionnée, ce document décrit l'état de `feat/b5-1-ux-polish` au commit `87a1bcd`.

## Versions

| Élément | Version |
|---|---|
| Godot | 4.7.2 stable **.NET (mono)**, rendu Compatibility, `Godot.NET.Sdk/4.7.2` |
| .NET | SDK 10.0.114 (`global.json` : 10.0.100, `rollForward: latestFeature`), cible `net10.0` |
| Paquets de test | xunit 2.9.3, xunit.runner.visualstudio 3.1.4, Microsoft.NET.Test.Sdk 17.14.1, coverlet.collector 6.0.4 |
| Dépendances de jeu | aucune dépendance NuGet (Core pur C#) ; Python/Pillow seulement pour `tools/` de conception, hors jeu |
| Résolution | 640 × 360 logique, fenêtre 1280 × 720 (×2 entier) |

## Ressources et licences incluses dans le dépôt

| Ressource | Emplacement | Licence |
|---|---|---|
| Pixeloid Sans 1.0 (Regular, Bold) | `src/GuildOps.Game/Assets/fonts/` | SIL OFL 1.1, texte et copyright GGBotNet dans `OFL-PixeloidSans.txt` |
| Kit graphique V4 (décor, 12 portraits, 12 spritesheets, 9-slices, boutons, icônes) | `src/GuildOps.Game/Assets/`, sources et prévisualisations dans `docs/b5-art-kit/` | Assets originaux produits pour le projet. **Aucun fichier de licence explicite** : à confirmer par le propriétaire avant toute diffusion publique |

SHA-256 : `PixeloidSans.ttf` 6c74e947…bb3e, `PixeloidSans-Bold.ttf` d8bc3841…a195.

## Résultats de référence (graine 1142026, via l'interface Godot)

| Politique | Résultat | Journal de référence |
|---|---|---|
| first-fit | caisse 2077, réputation 87, 18 réussites, 113 événements | parité `UiParityTests` |
| greedy-repair | caisse 1707, réputation 64, 15 réussites, 107 événements, bilan réussi | `docs/playtests/seed-1142026-RepairIfPossible.md`, identique |
| greedy-apologize | caisse 1967, réputation 54, 107 événements, bilan inachevé | `…-Apologize.md`, identique |
| greedy-ignore | caisse 1397, réputation 37, 105 événements, bilan inachevé | `…-Ignore.md`, identique |
| idle | 0 réussite, réputation 35, 22 événements | `PlaytestTests` |

## Reproduire

```text
dotnet build GuildOps.sln && dotnet test GuildOps.sln          # 160 réussis, 3 ignorés (tests lents opt-in)
tools/shots.sh <dossier> <chemin-godot-mono>                    # build + import + scénario + 16 contrôles
tools/run-autoplay.sh <dossier> <chemin-godot-mono> <politique> # first-fit | greedy-repair | greedy-apologize | greedy-ignore | idle
```

## Identification

Après fusion et validation humaine, le propriétaire crée le tag annoté `b5.1-reference` sur le commit de fusion. Rien n'est tagué ni poussé avant. Le tag `v0-clean` et la branche `student/v0` (phase C) restent locaux et seront refaits sur cette base.
