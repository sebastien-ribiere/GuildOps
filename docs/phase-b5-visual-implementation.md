# GuildOps — Phase B5 : contrat d'intégration de l'interface 16 bits

> **Statut : proposition pour revue / pas une implémentation terminée.** Périmètre pour Claude Code et contrat de remise du kit graphique. B5 précède la version pédagogique endettée ; ne créer **aucune dette intentionnelle**. Ne pas fusionner cette PR automatiquement.

## 1. Objectif

Remplacer l'UI technique et monolithique de `src/GuildOps.Game/Main.cs` par une interface médiévale-fantasy 16 bits, interactive, stable et lisible. Conserver les comportements existants sur la boucle **7 jours**, les **12 héros**, les **4 offres par jour**, les missions, la préparation, la régénération du tableau, les plaintes, les évènements, le bilan et le reset. L'interface reste 100 % hors ligne (Windows x64 et Linux x64 visés).

Le moteur **`GuildOps.Core` est la source de vérité**. Les probabilités, l'argent, les coûts, l'énergie, la réputation et les conditions d'éligibilité ne doivent pas être reprogrammés dans les scènes.

## 2. Responsabilités

- **Assets graphiques** : fournis séparément par le responsable artistique (42 PNG provisoires + manifest et générateur). Claude **n'a pas à dessiner** de héros, de tiles ou d'icônes.
- **Claude** : architecture Godot/C#, scènes UI, navigation, intégration/import du kit fourni, états visuels, tests et preuves de fonctionnement.
- **Revue humaine** : aspect, cohérence, lisibilité, captures et acceptance. Aucun merge autonome.

## 3. Scènes / composants demandés

- `Main.tscn` / `GameShell.cs` : composition root, une **unique** instance `GameSession` pour toute la partie, HUD (jour, trésorerie, réputation, missions engagées), notifications, reset, navigation.
- `Scenes/GuildHall.tscn` : fond `guildhall_640x360.png`, zones d'interaction accessibles au clic et au clavier ; accès aux contrats, héros, réclamations/conseil, trésorerie/bilan. **Pas de pathfinding ni de déplacements libres**.
- `Scenes/QuestBoard.tscn` : quatre offres, description, niveau/compétence, coût d'énergie, prime, sélection, offres acceptées, action de régénération (une fois) si autorisée.
- `Scenes/HeroRoster.tscn` : grille 4x3 affichant **les douze** héros dès l'origine, portrait, compétence, niveau, énergie, coût, trait, disponibilité, sélection et détails/contrepartie.
- `Scenes/AssignmentPanel.tscn` : aperçu explicable d'affectation via `GameSession.Preview`, préparation éventuelle via `Prepare`, bouton `Assign` avec état activé/désactivé fondé sur Core.
- `Scenes/QuestResult.tscn` : liste de missions assignées/terminées, bouton `Resolve`, succès/échec, prime, énergie, delta de réputation, incident et cause ; courte animation **cosmétique** sans tirage supplémentaire.
- `Scenes/ComplaintDialog.tscn` : plaintes ouvertes et expirées, texte, choix et coûts, validation via `ComplaintCheck` et `ResolveComplaint`.
- `Scenes/DayReport.tscn` : journal des événements, fin de journée `EndDay`, offres renouvelées, bilan final du jour 7, possibilité de recommencer.

Ces composants peuvent être des panneaux cachés/affichés par un `GameShell` plutôt que des changements de scène : privilégier la **continuité de l'instance GameSession**, la simplicité et une navigation sans perte de sélection.

## 4. APIs existantes à réutiliser

La V0 contient déjà `GameSession.Start`, `Reset`, `Heroes`, `Offers`/`CurrentOffers`, `Quests`, `Complaints`, `Events`, `Guild`, `Preview`, `Assign`, `Prepare`, `Resolve`, `ComplaintCheck`, `ResolveComplaint`, `RegenerationCheck`, `RegenerateOffers`, `EndDay`, `Report`, `Status`, `Money`, `PreparationCostFor`, `ComplaintTraitBonus`. Adapter le presenter aux signatures **réelles** ; ne pas changer le Core pour simplifier le rendu.

Préférer des `Control`, `TextureRect`, `TextureButton`, `NinePatchRect`, `GridContainer`, `ScrollContainer`, `AnimationPlayer`, `CanvasLayer`. Les zones cliquables du hall sont des contrôles Godot **séparés des pixels du décor** ; la scène est accessible sans cliquer au pixel près.

## 5. Kit graphique et conventions d'import

L'intégrateur reçoit `guildops_b5_art_16bits.zip`, comprenant `assets/manifest.json` :
- `backgrounds/guildhall_640x360.png` (un décor complet sans UI incorporée).
- `portraits/<hero-id>.png` : **12 × 64×64 RGBA**, noms calqués sur `game-data/roster.v0.1.json`.
- `sprites/<hero-id>_idle.png` : **12 spritesheets 128×48 RGBA**, quatre frames horizontales 32×48 ; mouvement décoratif, non synchrone de la simulation.
- `ui/panel_*_9slice.png` : **48×48**, marges 8 px ; `button_{normal,hover,pressed,disabled}_128x32.png` ; `portrait_frame_72x72.png`.
- `icons/*_16.png` : neuf icônes (cuivre, réputation, énergie, vie, parchemin, alerte, succès, échec, jour).

**Chemin cible** des binaires PNG : `src/GuildOps.Game/Assets/` en conservant les sous-dossiers ci-dessus. Le générateur Pillow n'est **pas** une dépendance d'exécution : images PNG pré-générées et embarquées dans l'export.

Import `Nearest`, aucun lissage/mipmap sur pixels ; résolution de base **640×360**, adaptée par mise à l'échelle entière pour 1280×720. Vérifier la taille minimale, les écrans courants et le texte en français (accents, lignes longues). Ne pas découper les images de concept-art avec texte comme des sprites.

## 6. Séquencement souhaité des commits / PR Claude

1. **Squelette** : scènes Godot, shell et navigation avec contenu textuel provisoire, une session unique. Aucun changement de métier.
2. **Flux principal** : quatre offres → douze héros → preview → affecter/préparer → résoudre → conséquences.
3. **Flux secondaire** : réclamations, nouveau jour, régénération, journal, victoire/défaite et reset seed fixe.
4. **Intégration des PNG** : arrière-plan, avatars, animation idle, thème 9-slice, états visuels et icônes.
5. **Hardening** : écran 640×360, navigation clavier, longues chaînes, export, captures, tests, scénario seedé complet.

Le premier sous-lot peut avoir des PNG temporaires, mais la livraison B5 doit intégrer **le kit fourni**, sans remplacer les données métier par de la logique visuelle.

## 7. Hors périmètre

Aucun changement de `game-data`, équilibrage, catalogue, probabilité, histoire, règles de plainte ni tests métier pour rendre la nouvelle UI plus simple. Pas de RPG de déplacement, combat, réseau, serveur, base de données, inventaire, localisation, son/musique final, refonte du moteur. **Ne pas injecter de défauts volontairement**.

## 8. Critères d'acceptation (DoD)

- Partie entière de 7 jours jouable via la nouvelle UI ; tous les états existants atteignables, y compris échec, refus d'affectation, plaintes et bilan.
- Les 12 portraits sont présents et correspondent aux 12 IDs ; la sélection d'un héros reste compréhensible même pour un héros indisponible.
- Une seule source métier : `GuildOps.Core` inchangé sur cette PR, aucune copie de seuil métier dans l'UI.
- Interaction non bloquante : double-clic / état non éligible n'introduit pas de nouvelle mutation ou résolution.
- `dotnet test` vert ; `dotnet build src/GuildOps.Game` ; lancement Godot .NET et export hors ligne réellement essayés (OS et commandes cités, pas de succès supposé).
- Captures des 5 vues principales et mini-parcours seedé joints à la PR ; comparer aux observables de `Main.cs` actuel.
- Les assets chargent depuis le dépôt exporté **sans Internet** et aucune dépendance runtime à Python/Pillow.
- Ne pas retirer la baseline saine : revue et tag `v0-clean` **après** B5 validée, puis seulement envisager la phase suivante.

## 9. Prompt pour Claude Code

> Travaille sur GuildOps, branche dérivée du socle B5, sans merge. Lis ce document, `README.md`, le `Main.cs` actuel et les APIs publiques de `GuildOps.Core`. Propose d'abord un plan de scènes et une table de correspondance des actions présentes. Refonds **uniquement** la couche Godot en UI 16 bits, avec les PNG du kit livré dans `src/GuildOps.Game/Assets/`. Conserve chaque opération métier et chaque condition d'acceptation existante. Fournis des preuves exécutées des tests/builds/export et des captures. N'ajoute pas de nouveaux systèmes de gameplay, ne modifie pas les règles d'économie, et n'injecte aucun défaut pédagogique intentionnel. Présente la livraison en PR pour revue humaine.
