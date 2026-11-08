# Phase B5 — rapport d'intégration de l'interface 16 bits

Périmètre : couche Godot uniquement. `src/GuildOps.Core` et `game-data/` sont **inchangés** (`git diff main -- src/GuildOps.Core game-data` vide). Aucune dette intentionnelle injectée.

## Architecture
`Main.tscn` → `GameShell.cs` (composition root) : une **unique** `GameSession`, l'état de sélection, le HUD, le bandeau de messages et la navigation. Les panneaux (`Scenes/*.tscn` + `.cs`) sont masqués/affichés, pas rechargés : la session et les sélections survivent à la navigation. Échap ramène au hall.

| Scène | Rôle | API Core |
|---|---|---|
| `GuildHall` | décor, 5 zones (clic/clavier), sprites idle décoratifs | `CurrentOffers`, `Heroes`, `Quests`, `OpenComplaints` |
| `QuestBoard` | 4 offres, sélection, régénération | `CurrentOffers`, `RegenerationCheck`, `RegenerateOffers` |
| `HeroRoster` | grille 4×3 des 12 héros, grisés selon `Preview` | `Heroes`, `Preview` |
| `AssignmentPanel` | aperçu explicable, Préparer, Affecter | `Preview`, `PreparationCostFor/Check`, `Prepare`, `Assign` |
| `QuestResult` | missions, Résoudre, verdict, incident | `Quests`, `Resolve` |
| `ComplaintDialog` | réclamations et 3 choix | `Complaints`, `ComplaintCheck`, `ComplaintTraitBonus`, `ResolveComplaint` |
| `DayReport` | journal, `EndDay`, bilan du jour 7, reset | `Events`, `EndDay`, `Report`, `Status`, `Reset` |

Correspondance avec l'ancien `Main.cs` : toutes les actions (affecter, préparer, résoudre, réclamation ×3, renouveler, fin de journée, reset, journal, bilan) sont conservées, avec les mêmes messages de refus (`Result.Message`). Écarts d'affichage : le reset demande une confirmation (2 clics) ; après `Assign`, l'offre est désélectionnée (un double-clic ne rejoue donc rien, et Core refuserait de toute façon) ; le coût et l'énergie d'un héros sont des pastilles sur son portrait, le trait et sa contrepartie sont dans `AssignmentPanel`.

## Assets
Kit fourni utilisé tel quel (fond, 12 portraits, 12 spritesheets idle, 9-slices, boutons, icônes, cadre). Le cadre de portrait a un centre opaque : il est dessiné sous le portrait. Seul ajout : police **Silkscreen** (SIL OFL 1.1, licence dans `Assets/fonts/OFL-Silkscreen.txt`) pour un texte pixel avec accents. Filtre `Nearest`, viewport 640×360, mise à l'échelle entière (fenêtre 1280×720).

## Preuves exécutées (Linux x64, Godot 4.7.2 mono, .NET SDK 10.0.114)
- `dotnet test` : 154 réussis, 0 échec, 3 ignorés (tests lents), dont le nouveau `UiParityTests`.
- `dotnet build src/GuildOps.Game` : réussi.
- Scénario seedé (`godot --path src/GuildOps.Game -- --autoplay --shots=<dossier>`), joué via les commandes de l'UI : `day=7 treasury=2077 reputation=87 succeeded=18 events=113 status=Finished success=True`. Le test `UiParityTests` rejoue la même politique sur Core seul et obtient le même état final.
- Contrôle clavier : focus présent dans les 6 vues, Échap → hall (`KEYBOARD_CHECK … escape_to_hall=True`).
- Export : `godot --headless --path src/GuildOps.Game --export-release "Linux x64"` réussi ; le binaire exporté rejoue le scénario complet avec le même résultat. `--export-release "Windows x64"` produit `GuildOps.exe` mais **n'a pas été lancé** (pas de Windows ici).
- Hors ligne : aucun appel réseau dans le code (aucun `HttpClient`/`HTTPRequest`/socket) ; assets embarqués ; pas de dépendance Python/Pillow à l'exécution. Je n'ai pas pu isoler le réseau (`unshare -n` refusé) : le test hors ligne n'a donc pas été fait sous coupure réseau.
- Captures (agrandies ×2) : `docs/b5-screenshots/` — hall, tableau, héros, missions, réclamations, journal jour 1, bilan final.

## Limites connues / à revoir par un humain
- Revue visuelle, lisibilité du texte 8 px et choix de la police restent à valider.
- Non vérifié : écrans autres que 1280×720 (le mode viewport entier est configuré), textes très longs hors données actuelles.
- Les panneaux sont construits en code dans des `.tscn` minimaux (script seul), pas dessinés dans l'éditeur.
- `AutoPlay` (scénario de preuve) reste dans le projet, actif seulement avec `--autoplay`.
