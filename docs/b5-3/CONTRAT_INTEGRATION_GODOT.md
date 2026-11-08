# B5.3 — Contrat d'intégration Godot (Claude Code)

## Dépendance et périmètre

Démarrer **après validation/fusion par le propriétaire de la PR B5.2**. Branche neuve `feat/b5-3-finalisation` créée depuis `main`. Ne pas toucher `GuildOps.Core` / `game-data` sans décision explicite ; pour ce lot, tous les changements sont dans `src/GuildOps.Game/` et docs/exports.

Jeu observé au 8 novembre 2026 : Godot **4.7.2 .NET**, C# **.NET 10**, écran logique **640×360**, fenêtre **1280×720**, Compatibility, thème **Pixeloid Sans 9px**. `GameShell` instancie une session unique dans `_Ready()` et affiche immédiatement `View.Hall`. `DayReport` affiche déjà un bilan royal de Core. Aucun système de sauvegarde/reprise persistante n'a été confirmé.

## Assets à importer

```
assets/b5_3/
  backgrounds/title_640x360.png
  scenes/intro_384x256.png
  scenes/victory_384x256.png
  scenes/defeat_384x256.png
  branding/guild_crest_64.png
  branding/guild_crest_128.png
  branding/guild_crest_256.png
  branding/app_icon_256.png
  tutorial/step_1_192x128.png
  tutorial/step_2_192x128.png
  tutorial/step_3_192x128.png
  tutorial/step_4_192x128.png
  ui/ribbon_title_192x32.png
  ui/ribbon_victory_192x32.png
  ui/ribbon_defeat_192x32.png
  ui/divider_160x8.png
  icons/{play,help,settings,credits,exit,back,fullscreen,reduce_motion,trophy,keyboard,info}_16.png
```

**27 PNG runtime**. Depuis l'extraction du ZIP, copier `assets/b5_3/*` dans `src/GuildOps.Game/Assets/b5_3/` puis import Godot avec cache propre. Tous les chemins restent relatifs à `res://Assets/b5_3/` ; ne pas réutiliser l'importeur B5 des 42 PNG. Le titre comporte un logo **peint dans l'image** : ne pas en rajouter un second. Les `sources/` et `previews/` ne doivent pas être copiés au runtime.

## Découpage d'implémentation souhaité

### B5.3-1 — Système d'écrans et démarrage

- Ajouter un **TitleScreen** superposé à l'application actuelle, visible au démarrage normal ; `--autoplay` / captures automatisées restent bypassés.
- Entrées : Nouvelle partie, Aide et règles, Paramètres, Crédits, Quitter. Le bouton « Reprendre » n'existe que pour une session courante réellement en mémoire, et ne doit jamais prétendre recharger un fichier. **Pas de sauvegarde fictive**.
- Conserver une seule session métier. Différencier `Retour au titre` (navigation) de `Recommencer` (reset avec confirmation). Ne pas modifier la graine de référence.
- Ne pas réimplémenter les six vues B5.2 ; garder leurs raccourcis et les zones cliquables.

### B5.3-2 — Introduction et aide

- Introduction courte avant l'entrée dans le hall lors d'une nouvelle partie normale ; ignorable immédiatement ; textes dans labels Godot, pas dans les images.
- Tutoriel en 4 pages avec les PNG `tutorial/` ; garde de focus, Échap, Suivant/Précédent, indicateur de progression, pas de mutation Core ni de délai forcé.
- Aide à tout moment depuis titre, hall et jeu ; afficher aussi les commandes clavier et les objectifs réels du `rules.Victory` lorsque nécessaire.

### B5.3-3 — Paramètres et accessibilité

- Fenêtré / plein écran, nearest + échelle entière, réduire les animations (état lisible à l'écran, sans dépendance couleur seule).
- Affecter l'option animation à `AmbientFx`, `CosmeticFx` et transition optionnelle. Désactivation immédiate, sans casser la logique des événements.
- Mémorisation facultative des préférences système via `ConfigFile`, **hors Core**. Aucun autre type de sauvegarde dans ce lot.
- **Audio : aucune musique, aucun SFX, aucun menu audio, aucun son au démarrage.** Le mode classe silencieux est le comportement standard et unique.

### B5.3-4 — Fin de partie

- Afficher une scène finale après `Session.EndDay()` **seulement si Core retourne un rapport final**.
- Choisir victoire ou défaite à partir de `FinalReport.Success`, sans recomposer la condition à partir de compteurs UI.
- Afficher les vraies valeurs en contrôles Godot ; « Voir le bilan » mène au journal détaillé ; `Retour au titre` ne reset pas la session ; `Nouvelle partie` confirme le reset.
- Conserver `DayReport` comme source de chiffres détaillés, ne pas dupliquer sa logique ni détourner un échec de mission en écran de défaite globale.

### B5.3-5 — Crédits et distribution

- Fenêtre de crédits lisible, inclure la liste des licences et contributions vérifiées ; pas de nom inventé.
- Créer icônes d'application Linux / Windows à partir de `branding/` sans changer le style du jeu.
- Exports Linux et Windows de **Godot .NET**, lancements sur répertoire propre, documentation dépendances/reproductibilité. Si système distant indisponible, distinguer compilation de validation réelle sur la cible.

## Politiques de rendu et états

- Palette bordeaux / prune / or / parchemin ; réutiliser boutons, panneaux et types de font de `UiKit` ; pas de nouvelles familles, ni de lissage de glyphes.
- 640×360 natif, échelle entière, alignement des coordonnées et focus visibles ; ne pas rogner le logo sur les écrans 16:9.
- Toute illustration est **décorative** et `MouseFilter.Ignore`, sauf boutons Godot explicitement interactifs.
- Les menus modaux doivent restaurer le focus initial après fermeture. La touche Échap ferme d'abord la modale, puis revient dans la vue précédente, jamais reset.
- `--autoplay` et tests déjà présents fonctionnent sans clic dans le titre ; produire un test du démarrage normal **et** un test de bypass.
- Aucun effet ne mute Core ni RNG ; tous les timers/animations ont une option réduit les animations.

## DoD / acceptation

1. Les 27 PNG passent `tools/validate_b5_3.py`, images chargées depuis une copie propre du projet.
2. Démarrage normal silencieux → titre → intro → hall → gameplay des sept jours → bilan → fin de partie, avec une seule session et les résultats de Core.
3. Le démarrage `--autoplay` continue de fonctionner sans écran titre bloquant, journaux identiques aux scénarios seedés existants.
4. Aide, crédits et paramètres utilisables depuis titre et jeu, à la souris et au clavier ; focus et Échap cohérents.
5. Aucun bouton mort ni « Continuer » de sauvegarde inventé ; retour titre/reprise/restart ont une sémantique explicite.
6. Paramètre « Réduire les animations » désactive effectivement l'ambiance/les effets visuels.
7. Aucun son n'est émis par l'application, même au démarrage et à la victoire.
8. Les vues B5.2 et tous leurs assets restent intacts ; `dotnet build`, `dotnet test`, import Godot et parcours seedé sont exécutés.
9. Captures réelles à 1280×720 : titre, intro, les 4 pages aide, réglages, crédits, victoire, défaite, hall B5.2 et résultat de quête.
10. Archives de distribution Linux/Windows + instructions de lancement, licences, et tests sur machines cibles quand disponibles.
11. PR non fusionnée, description claire des tests effectués et non effectués, **attente de validation humaine**.
