# B5.2 — Intégration artistique (kit `guildops_b5_2_hd_complet_v1_2`)

Branche `feat/b5-2-art-integration`, depuis `main` (B5.1 fusionnée, PR #9). **Aucun changement** de `GuildOps.Core`, de `game-data`, des règles, des probabilités, du RNG ni des scénarios.

## Ressources (59 PNG runtime)

Copiées telles quelles dans `src/GuildOps.Game/Assets/b5_2/` (SHA-256 et tailles validés par `validate_b5_2.py` du kit **sur la copie du dépôt** : « OK: 39 base PNG + 20 HD PNG, 20 quests mapped »). L'ancien importeur `tools/import_b5_art.py` n'est pas utilisé.

| Dossier | PNG | Utilisation dans le jeu |
|---|---:|---|
| `quests/` | 20 | Miniature 96×64, échelle 1, sans lissage : panneau d'affectation (tableau et héros) |
| `quests_hd/` | 20 | Illustration 384×256 : visionneuse à l'échelle 1 ; version 192×128 (÷2 exact) sur la fiche de mission |
| `categories/` | 5 | Emblème 24×24 avant la pastille texte de catégorie (fiches détaillées) |
| `skills/` | 4 | Icône 16×16 avant le nom de compétence (fiches détaillées) |
| `levels/` | 4 | Marqueur du niveau **minimal requis** (jamais une difficulté), fiches détaillées |
| `vfx/` | 4 | Succès, échec, récompense, incident (6 × 32×32, une fois) |
| `ambient/` | 2 | Foyer (8 × 64×64, 170 ms) et trésor (8 × 32×32, 140 ms), en boucle dans le hall |

Les illustrations sont adressées **uniquement** par `QuestTemplate.Id` (`Art.Thumb/Hd/HdHalf`), jamais par le titre, sans repli narratif. Les 20 modèles du catalogue ont leurs deux images (`QUEST_ART_CHECK`).

## Où est affichée la version HD

- **Fiche de mission** (écran Missions, en cours ou terminée) : illustration HD à 192×128 à droite de la description et du verdict. Le ÷2 exact est calculé une fois (filtre trilinéaire = moyenne de zone, mis en cache), jamais par échantillonnage au plus proche. Ratio 3:2 conservé.
- **Visionneuse** (clic ou Entrée sur n'importe quelle illustration, y compris la miniature du panneau d'affectation) : HD **384×256 à l'échelle 1**, titre, catégorie et compétence écrits, légende « ne préjuge pas du résultat ». Échap, Entrée ou clic la ferme, le focus clavier reste piégé sur « Fermer » puis revient à l'appelant. Aucune mutation.
- **Panneau d'affectation** (tableau, héros) : miniature 96×64 native, emblème de catégorie, icône de compétence, marqueur de niveau, textes inchangés.

### Contrainte documentée

Le panneau d'affectation fait ~190 px de large et la fiche de mission ~396 px de large pour ~290 px de haut : un 384×256 à l'échelle 1 y repousserait le verdict et les boutons hors de l'écran. D'où : demi-échelle inline + échelle 1 à la demande. Les **quatre offres du tableau** ne reçoivent pas de vignette (la carte reste à 66 px, descriptions et boutons intacts). Conséquence : quand une mission réussie a **un incident**, « Voir la réclamation » et « Détails du calcul » passent partiellement sous le pli de la zone défilante (atteignables à la souris et au clavier, le focus fait défiler). Dans le panneau d'affectation, le bloc « quête » est de ~28 px plus bas qu'avant, donc un peu plus loin sous le pli lorsque l'aperçu est long.

## Effets de résultat (B5.2-B)

`GameShell.MissionResolved` est levé **une fois**, après `Session.Resolve` accepté par Core. `QuestResult` y réagit : `CosmeticFx` joue, dans un emplacement 32×32 à droite du bandeau de verdict (sans le masquer), `succès | échec`, puis `récompense` si `RewardPaidCopper > 0`, puis `incident` si `Incident != null`. Les effets lisent l'issue déjà produite : aucun appel à Core, aucun RNG. `RefreshAll`, changement de vue, réouverture de fiche et « Résoudre » répété ne rejouent rien (la pose en fondu du bandeau n'a plus lieu qu'une fois par mission). Un effet est non bloquant (`MouseFilter.Ignore`), s'interrompt à la première touche ou clic (sans consommer l'événement) et disparaît avec sa fiche.

## Ambiance du hall (B5.2-C)

`AmbientFx` : foyer centré sur (321,162) et trésor sur (498,221) en coordonnées du décor, décalage `TopOffset = 24` de `GuildHall` appliqué. Insérés entre le fond et les sprites : sous les douze sprites, les cinq zones cliquables et leurs étiquettes. Pas de filtre souris, pas de focus ; zones, sprites et idle inchangés. Transitions entre vues (optionnelles) : **non réalisées**, pour ne pas toucher à la navigation ni retarder le joueur.

## Vérifications exécutées

- `dotnet build` : 0 avertissement, 0 erreur. `dotnet test` : **160 réussis, 3 ignorés** (ignorés : rapports de benchmark/équilibrage, comme avant).
- Cache Godot supprimé (`.godot/`) puis `tools/shots.sh` : import propre, démarrage Godot .NET 4.7.2 sans erreur de script ; journal : `LOG_ERRORS=0`.
- `tools/run-autoplay.sh` (vraie interface Godot, mêmes commandes que le joueur) : `first-fit` sur 7 jours, `CHECK_FAILURES=0`. Contrôles ajoutés : `ASSETS_CHECK` (59 PNG), `QUEST_ART_CHECK` (20 × 3 tailles), `AMBIENT_CHECK` (2 boucles qui avancent, 5 zones inchangées, sous les zones), `VIEWER_CHECK` (clavier, focus piégé, Échap, état inchangé), `FX_CHECK` (un contrôle par mission : effets joués = effets attendus d'après l'issue de Core), `REPEAT_RESOLVE_CHECK` (aucun nouvel effet ni mutation), plus les contrôles de mise en page à 640×360 et le parcours clavier déjà existants.
- Parties de référence (`greedy-ignore`, `greedy-apologize`, `greedy-repair`) : `REFERENCE_JOURNAL_CHECK OK`, journaux **identiques** à `docs/playtests/` ; `idle` : OK.

## Captures

`docs/b5-2-captures/avant` (main) et `apres` : captures réelles du viewport Godot (agrandies ×2 au plus proche). `01b`/`01c` : hall à deux instants (ambiance) ; `03c` : visionneuse HD ; `08a`/`10a`/`12a` : effets de succès+prime, d'échec et d'incident capturés pendant leur lecture. Les petits carrés grisés visibles sur les panneaux existent déjà dans les captures « avant » (texture de panneau de B5.1) ; non traités ici.

## Limites

- Aucune vérification d'export Linux/Windows ni de rendu hors du poste de développement (Compatibility, Godot 4.7.2 mono).
- Les vignettes 96×64 ne sont pas affichées sur les quatre offres du tableau (voir plus haut).
- Pas de son, pas de transition de vue, pas de B5.3.
