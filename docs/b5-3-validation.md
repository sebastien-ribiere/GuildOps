# B5.3 — Rapport de validation

Poste : Linux (X11), Godot 4.7.2 .NET, .NET 10. Toutes les commandes ont été exécutées sur le code final ; codes de retour non masqués.

| # | Contrôle | Résultat |
|---|---|---|
| 1 | `tools/validate_b5_3.py` + `AssetsB53Tests` (27 PNG, manifest, SHA-256) | OK (rc 0) |
| 2 | `dotnet build` (sln et `src/GuildOps.Game`) | 0 avertissement, 0 erreur |
| 3 | `dotnet test` | **174 réussis, 0 échec, 3 ignorés** (benchmarks opt-in, comme avant ; base : 160/0/3) |
| 4 | Import Godot, cache `.godot` supprimé | rc 0, 0 erreur |
| 5-7, 10, 11 | `tools/run-flow.sh` (démarrage normal, clavier réel) : titre, intro, aide, paramètres, plein écran/échelle appliqués à la fenêtre, crédits, 7 jours, victoire, défaite, confirmations | rc 0, **36 contrôles OK, 0 échec**, 0 erreur de journal |
| 8 | Animations réduites : ambiance masquée/arrêtée, effets non lancés, coupés en cours d'effet, fondu supprimé | OK (`FLOW_REDUCE_*`, `FLOW_FADE`) |
| 9 | Silence : 0 lecteur audio, crête du mixage −200 dB, aucun fichier audio (source et export), test statique | OK |
| 12 | AutoPlay, 5 politiques | résultats **identiques** à la base `main` ; 3 journaux identiques à `docs/playtests/` (voir ci-dessous) |
| 13 | Exports Linux et Windows (`tools/package.sh`) | générés, rc 0 |
| 14 | Lancement : Linux dans un environnement propre (`tools/test-dist.sh`) | **OK** (AutoPlay de référence, parcours normal, démarrage simple) |
| 14 | Lancement : Windows | **impossible ici** (ni Windows ni Wine) : archive générée, format PE32+ x64 et contenu vérifiés, jamais exécutée |

## Preuve « règles et scénarios inchangés »
- `git diff origin/main -- src/GuildOps.Core game-data` : vide.
- AutoPlay avant/après : first-fit 2077/87/18/113 ; greedy-ignore 1397/37/15/105 ; greedy-apologize 1967/54/15/107 ; greedy-repair 1707/64/15/107 ; idle 1200/35/0/22 (trésorerie/réputation/réussites/événements).
- Le parcours normal (interface, animations basculées) donne le même état et le même journal (113 événements) que la même partie jouée dans Core seul.
- Hall en AutoPlay : **0 pixel** de différence avec B5.2. Autres vues : 0,1 à 0,4 % de pixels (grain de panneau corrigé), HUD identique.

## Limitations et incidents
- **Anomalies sporadiques d'AutoPlay** (3 sur ~25 exécutions, avant correctif) : cause identifiée sur des captures, l'aide s'ouvrait en plein scénario par un focus mal placé activant un bouton HUD ajouté en B5.3. Corrigé : en `--autoplay`, « Aide », « Menu » et F1 n'existent pas. Série contrôlée de 8 exécutions sur B5.3 et 8 sur `main` : 16/16 propres. Une déviation de résultat Core observée une fois sur l'export n'a pas été reproduite en 6 reprises ; je la suppose due à une entrée réelle sur le bureau partagé, sans preuve. Lancer les scénarios sans toucher à la fenêtre.
- Windows non exécuté ; icône de l'`.exe` = icône Godot (pas de `rcedit`).
- Rectangle translucide résiduel dans les PNG intro/victoire/défaite du kit (figés par le manifest).
- Aucune licence pour GuildOps lui-même dans le dépôt ; droits des illustrations à confirmer (voir l'inventaire livré).
- Pas de sauvegarde sur disque (hors périmètre).

## Distributions (locales, non publiées)
`dist/GuildOps-B5.3-linux-x86_64.tar.gz` — `4d5b68549627b48b0077c71175d3cad536b6c5e252ce4a0d552713dd6ed132d2`
`dist/GuildOps-B5.3-windows-x86_64.zip` — `cdfdb40f141c30f81d64d27afebbb8a59ce21f1bf43fdc0f01895da943a1871f`
Régénération : `tools/package.sh <godot-mono>` ; test : `tools/test-dist.sh <archive>`. Captures : `docs/b5-3-captures/`.
