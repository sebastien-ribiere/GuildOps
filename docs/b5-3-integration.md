# B5.3 — Finalisation : écran titre, aide, paramètres, fin de partie, crédits, distribution

Contrat de référence : `docs/b5-3/` (direction artistique, contrat d'intégration Godot, checklist, sources et licences, manifest).
Périmètre : `src/GuildOps.Game/` uniquement, plus `tools/`, `packaging/`, `tests/` et `docs/`. **`GuildOps.Core` et `game-data` ne sont pas modifiés**
(preuve dans `docs/b5-3-validation.md`).

## Parcours

```
Lancement → Titre → Introduction → Hall → Sept jours de gestion → Bilan royal → Victoire ou défaite → Retour au titre
```

`GameShell` possède l'**unique** `GameSession`, créée dans `_Ready()` depuis la graine de référence (`rules.TestSeed`) ; le titre et l'introduction
s'affichent par-dessus un hall déjà prêt. Aucune seconde instance n'existe (un test statique le vérifie).

### Sémantique des commandes de navigation

| Commande | Où | Effet sur la partie | Confirmation |
|---|---|---|---|
| **Nouvelle partie** | titre, écran de fin | `Session.Reset()` (même graine), puis introduction, puis hall | oui, si la partie n'est plus à son état initial (`HasProgress`) |
| **Reprendre** / **Revoir le bilan** | titre | aucune : retour à la partie **en mémoire**, dans la vue où on l'avait laissée (ou au bilan si elle est terminée) | non |
| **Menu** (HUD) / **Retour au titre** (fin) | jeu, écran de fin | aucune : la session est conservée | non |
| **Recommencer** (HUD, bilan) | jeu | `Session.Reset()` (même graine), retour au hall, sans introduction (comportement B5.1 conservé) | oui (double appui, 4 s) |
| **Quitter** | titre | ferme l'application | oui, si une partie est en cours |

« Reprendre » n'apparaît que si une partie existe réellement en mémoire et indique « Partie en mémoire, non sauvegardée ». **Il n'y a ni « Continuer » ni sauvegarde sur disque**
(décision explicitement hors périmètre) ; seul fichier écrit : les préférences d'affichage.

## Composants (`src/GuildOps.Game/Ui/`)

- `Overlay` : écran superposé commun. La souris est interceptée ; le focus clavier est enfermé dans une boucle de contrôles (`SetFocusRing`) ;
  Échap ferme **l'écran du dessus seulement** ; le focus est rendu au contrôle d'origine ; le dernier ouvert est dessiné au-dessus ; fondu d'ouverture de 150 ms
  (supprimé par « Réduire les animations », sans jamais retarder une entrée).
- `TitleScreen`, `IntroScreen`, `ConfirmDialog` (focus initial sur le choix sûr), `HelpScreen` + `HelpContent` (texte éditable en un seul endroit, nombres lus dans `rules`),
  `SettingsScreen` + `DisplaySettings`, `EndScreen`, `CreditsScreen`, `B53` (fabrique de textures du kit).
- `GameShell` : pile d'écrans, `HasProgress`, commandes ci-dessus, `F1`, boutons HUD « Aide » et « Menu ».
- `Probe`, `FlowCheck` : contrôles automatisés (voir plus bas).

### Fin de partie
L'écran n'est ouvert que par `GameShell.EndDay()` lorsque Core renvoie un `FinalReport`, et la distinction victoire/défaite est `FinalReport.Success`, rien d'autre
(`EndScreen` ne compare aucun seuil). Une mission ratée n'y mène jamais. Esc = « Voir le bilan détaillé » (jamais une réinitialisation). Les chiffres affichés sont ceux du rapport ;
les objectifs sont lus dans `rules.Victory` pour être affichés.

### Paramètres
Plein écran, échelle **entière** de la fenêtre (de ×1 au maximum que l'écran permet), « Réduire les animations », rappel des commandes clavier. Résolution logique 640×360, filtrage nearest
et mise à l'échelle entière sont ceux de `project.godot` (inchangés). Préférences : `user://display.cfg` (`ConfigFile`, section `display` : `fullscreen`, `scale`, `reduce_motion`) ;
fichier absent, illisible ou aux valeurs absurdes : réglages par défaut, sans exception. En lancement automatisé, aucune préférence n'est lue ni écrite.

« Réduire les animations » : `AmbientFx` est masqué et arrêté immédiatement, `CosmeticFx` n'est plus lancé (et un effet en cours s'interrompt), le fondu d'écran disparaît.
Ces effets ne lisent ni n'écrivent Core ni le RNG : le réglage ne peut pas changer un résultat (prouvé par le test de déterminisme du parcours normal).

### Silence
Aucune musique, aucun effet, aucun son au lancement ni aux résultats, aucun réglage audio, aucune bibliothèque audio ; aucun fichier audio dans le projet ni dans les exports.
Vérifié statiquement (`B53ContractTests`) et à l'exécution (`FLOW_SILENCE_CHECK` : aucun lecteur audio dans l'arbre de scène, crête du mixage à −200 dB sur tout le parcours).

## Mode automatisé (`--autoplay`) et parcours normal (`--flowcheck`)

- `--autoplay` : le titre, l'introduction et l'écran de fin ne sont jamais ouverts ; les boutons « Aide » / « Menu » et la touche F1 sont désactivés ; aucune préférence n'est lue.
  Le HUD et le hall sont donc **identiques à B5.2**, et aucune modale ne peut bloquer un scénario, même par un focus clavier mal placé ou un clic perdu.
  Les résultats seedés sont inchangés (voir validation).
- `--flowcheck` (`tools/run-flow.sh`) : démarrage **normal** (titre) piloté par de vraies actions clavier et de vrais boutons : titre, introduction, aide (F1, HUD, 4 pages), paramètres
  (plein écran et échelle réellement appliqués à la fenêtre, animations réduites, persistance), crédits, partie complète de 7 jours (victoire) comparée événement par événement à la même partie
  jouée dans Core seul, partie sans mission (défaite), confirmations, navigation depuis l'écran de fin, silence, session unique.

## Corrections ciblées hors fonctionnalités (sans incidence sur le gameplay)

1. **Grain de panneau** (`UiKit.Panel`) : le centre des PNG `panel_*_9slice` contient 4 pixels isolés qui, étirés, devenaient de gros carrés gris sur les grands panneaux
   (défaut signalé dans la doc B5.2 : « non traité »). L'axe central est maintenant en mosaïque ; le PNG n'est pas modifié. Effet mesuré : 0,1 à 0,4 % des pixels des vues B5.2 (hors HUD).
2. **`ASSETS_CHECK` d'AutoPlay** : il n'énumérait aucun PNG dans un export (seuls `*.png.import` y subsistent). Il normalise désormais les deux cas. Contrôle de test uniquement.
3. Aucun contrôle sous la zone visible n'a été relevé : tous les `LAYOUT_CHECK` d'AutoPlay sont OK ; aucun correctif nécessaire.

## Écarts et décisions à connaître

- **Ruban** : le texte est en Bold 9 px (et non 18 px) car « Guilde redressée ! » à 18 px dépasse la zone utile (≈ 144 px) du ruban 192×32 fourni.
- **Menu du titre** : placé en bas à droite (marge de 12 px) et dimensionné par son contenu plutôt que par le rectangle proposé (440–628 × 208–346) : avec « Reprendre » et sa note, il faut plus de hauteur.
- **Accès aux paramètres et aux crédits en jeu** : via « Menu » (retour au titre sans perte de partie), pas par un bouton HUD supplémentaire (place comptée).
- **Illustrations du kit** : les trois scènes 384×256 (introduction, victoire, défaite) contiennent un rectangle translucide résiduel (zone de maquette) en bas à droite de l'image. Les PNG sont figés par le manifest (SHA-256) :
  je ne les ai pas modifiés ; à corriger côté graphiste si on veut le retirer.
- **Icône Windows** : l'icône de fenêtre (blason) est définie dans le projet ; l'incrustation dans le `.exe` demande `rcedit` (et Wine sous Linux), absents ici : l'explorateur Windows affichera l'icône Godot par défaut.
