# Décision de direction artistique — typographie GuildOps (B5.1)

**Statut : DÉCISION VALIDÉE, impérative pour l'implémentation.** 8 novembre 2026. Source officielle : https://ggbot.itch.io/pixeloid-font.

## 1. Choix unique

**Une seule famille visible dans le jeu : Pixeloid Sans 1.0 de GGBotNet**, dans ses deux styles officiels `PixeloidSans.ttf` (Regular) et `PixeloidSans-Bold.ttf` (Bold). Typographie pixel-native, conçue sur une grille de **9 px**, prenant en charge le français, distribuée sous **SIL Open Font License 1.1** (texte OFL et copyright fournis avec les ressources).

**Ne plus utiliser Jersey 15, Silkscreen ni Tiny5 dans le rendu du jeu.** Ces fichiers peuvent être supprimés uniquement si devenus inutilisés et sans casser les licences ; ne pas introduire d'autres familles sans validation explicite du propriétaire.

Cette décision concerne **toutes les vues** (HUD, hall détaillé, contrats, roster, missions, plaintes, journal, bilan, résultats, toasts, boutons). Pas de dérogation locale cachée dans une scène.

## 2. Tableau de styles verrouillé

| Rôle | Police | Taille logique Godot | Casse |
|---|---|---:|---|
| Corps, descriptions, journaux, dialogues, toasts | Regular | **9 px** | Casse naturelle / phrase |
| HUD, boutons, noms de héros, stats | Bold | **9 px** | Casse naturelle |
| Titres de panneaux, étiquettes du hall | Bold | **9 px** | Casse naturelle (capitales courtes ponctuellement) |
| Grand verdict SUCCÈS / ÉCHEC | Bold | **18 px** | Capitales possibles pour ces deux mots |

**Pas de tailles intermédiaires improvisées (11, 12, 13, 15, etc.).** 9 et son multiple entier 18 sont les seules tailles d'interface initialement autorisées ; une exception doit être motivée et validée avant de coder.

Le titre du jeu ou un logo graphique peut être une image, pas une troisième police.

## 3. Qualité d'affichage

- Résolution logique : **640×360** ; fenêtre 1280×720 avec multiplication entière ×2, pas de scaling fractionnaire.
- Aucune interpolation floue. Godot : anti-aliasing des polices **None**, subpixel positioning **Disabled**, import correct des deux TTF ; pas de police système utilisée silencieusement comme fallback.
- Centraliser les styles dans `UiKit` / thème Godot ; modifier tous les appels et overrides qui peuvent sélectionner Jersey, Silkscreen ou Tiny5.
- Conserver les caractères et diacritiques FR (é è ê ë à â ù û ç î ï ô œ æ É À Ç, apostrophe, ponctuation). Tester notamment « Réclamations », « Trésorerie », « Énergie », « Mme Chausson cherche quelqu’un pour promener Médor », « Prudence ProductOwner ».
- Remplacer les symboles de navigation non couverts par des icônes du kit, ou du texte simple lorsque nécessaire ; ne pas utiliser un fallback système qui change la police.
- Les longs textes sont en casse naturelle, ni FULL UPPERCASE ni graisse Bold pour les descriptions. En particulier le journal, les détails de calcul, les messages du toast et l'aide.

## 4. Ajustement de la mise en page, pas de la police

Recomposer les marges, hauteurs de ligne, composants et panneaux autour des tailles **9/18**. Ne pas augmenter arbitrairement la taille pour remplir les espaces ni étirer les textes. Préserver les 12 cartes du roster (4×3) sans nom tronqué, les quatre offres, et les boutons d'affectation.

Sur le hall détaillé **V4**, tous les libellés doivent être courts et attachés au décor, les compteurs intégrés au libellé (ex. `Missions (0)`, jamais un `0` flottant), aucun chevauchement avec les sprites ou zones de clic. Ne pas valider uniquement sur l'ancien hall simplifié.

## 5. Done / preuves obligatoires

1. Réintégrer les deux TTF officiels + licence OFL ; vérifier que le jeu démarre dans un clone propre après `godot --headless --path src/GuildOps.Game --import`.
2. Contrôle statique : rechercher **toutes** les références à `Jersey`, `Silkscreen`, `Tiny5`, `FontFile`, `AddThemeFontOverride`, `AddThemeFontSizeOverride` dans le code du jeu ; seules Pixeloid Regular/Bold et les tailles convenues sont effectivement utilisées.
3. Screenshots **réels**, sous Godot .NET, du hall V4 détaillé, tableau, roster, affectation possible/impossible, missions en cours et résultats, plaintes, journal/bilan, à 1280×720.
4. Test d'accents/glyphes, noms complets, pas de débordement, focus et boutons. Vérifier les textes réellement affichés, pas seulement la présence du code.
5. `dotnet build`/`dotnet test` + import Godot + démarrage de jeu depuis cache propre, parcours de sept jours existants ; publication des résultats authentiques.
6. Aucune modification de `GuildOps.Core`, `game-data` ni de la logique de gameplay.
7. Conserver la branche B5.1 et **ne pas fusionner** avant validation visuelle du propriétaire.

**Critère de sortie :** ce que l'on voit dans GuildOps doit ressembler à l'interface d'un jeu 16 bits lisible, pas à une police console modernisée. En cas de doute, ne pas essayer de troisième typographie : revenir à cette décision et documenter le blocage.
