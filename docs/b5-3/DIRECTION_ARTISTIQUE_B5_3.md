# GuildOps — Direction artistique complète B5.3

## Intention

**Passer d'un jeu jouable à un jeu accueillant et distribuable**, sans transformer GuildOps en nouveau projet ni toucher aux règles de la guilde. La progression doit raconter : *on prend la tête d'une guilde en difficulté, on forme une équipe, on choisit des missions, et on rend compte au Conseil royal au bout de sept jours*.

La direction artistique est **validée comme orientation** ; les maquettes sont des références et les PNG runtime sont des propositions d'assets prêts à intégrer, dont le rendu final doit être vérifié dans Godot. B5.3 n'est pas une refonte des scènes B5.1/5.2.

## Grammaire visuelle

| Composante | Décision |
|---|---|
| Ton | Fantasy chaleureuse, guilde légèrement absurde, proximité avec les illustrations de contrats HD |
| Couleurs de base | Encre #1B1527, prune #3C1B38, bordeaux #7E1F3D, or #EFC150, crème #F7E4B6 |
| États | Vert feutré pour le succès, rouge carmin pour l'échec, **toujours texte et icône** |
| Art | Images narratives riches ; contrôler le contraste par des panneaux Godot, pas en gravant du texte dans le PNG |
| Éléments interactifs | Boutons/panneaux B5 déjà présents ; aucun troisième habillage de bouton |
| Typographie | **Pixeloid Sans**, 9 px logique Regular/Bold ; grand verdict 18 px ; logo peint autorisé comme image |
| Échelle | 640×360 natif / 1280×720 entier ×2, nearest ; préserver le cadrage des images 3:2 |
| Animation | Souple et courte, désactivable par « Réduire les animations », aucune animation qui impose d'attendre |
| Audio | **Silence intégral** : pas de musique ni d'effets sonores en B5.3, ni de contrôle audio inutile |

## A. Écran titre — `backgrounds/title_640x360.png`

- Source : peinture déjà approuvée de la forteresse GuildOps au crépuscule (présentée auparavant dans le travail graphique), avec le logo « GuildOps » **intégré comme art**. Ce n'est pas une nouvelle police d'interface.
- **Menu en vrais contrôles Godot**, non dessiné dans l'asset. Cadre compact en bas à droite (proposition logique : x=440 à 628, y=208 à 346), fond prune semi-opaque afin que le texte reste lisible. Ne pas inventer un second logo superposé.
- Boutons : **Nouvelle partie**, **Aide et règles**, **Paramètres**, **Crédits**, **Quitter** ; option « Reprendre » seulement si une session courante existe lorsque l'on revient au titre **ou** si un vrai mécanisme de sauvegarde est approuvé. Pas de bouton « Continuer » inerte.
- Le démarrage n'émet aucun son. L'entrée dans le hall est instantanée ; ne pas laisser tourner une animation de logo obligatoire.
- La commande « Nouvelle partie » s'appuie sur la session métier et son seed existants, sans introduire un second `GameSession` ni changer la graine du test.
- **Mode automatisé** : `--autoplay` et scripts de captures démarrent directement dans le jeu, sans dépendre d'un clic sur l'accueil.

## B. Introduction — `scenes/intro_384x256.png`

- Afficher la salle du Conseil royal et le hall détaillé comme ancrage narratif, avec un **parchemin Godot superposé** (pas de texte dans le PNG).
- Texte de référence suggéré, qui peut être reformulé sans altérer les règles : « Le Conseil royal vous confie une guilde à remettre sur pied. Vous avez sept jours pour organiser les missions et faire vos preuves. »
- Actions : « Commencer », « Passer l'introduction ». Pas de vidéo, pas de cinématique imposée, pas de progression dans le Core avant que le joueur agisse.
- Montrer au maximum une fois par nouvelle partie, sauf ouverture volontaire depuis l'aide. Le mode réduit les animations n'affecte pas la lisibilité de l'écran.

## C. Tutoriel / aide — `tutorial/step_{1..4}_192x128.png`

**Quatre étapes courtes**, affichées en modale ou panneau plein écran, accessibles depuis le titre ET depuis le jeu :

1. **Contrats** : choisir une offre, consulter catégorie, énergie et prime.
2. **Héros** : sélectionner l'un des douze, lire classe/niveau/énergie/coût et l'aperçu de réussite.
3. **Missions** : affecter, éventuellement préparer, résoudre et lire les conséquences ; l'exemple visuel ne prédit pas une issue.
4. **Bilan** : traiter les réclamations, clore le jour et vérifier les trois objectifs de victoire au jour 7.

La copie affichée doit venir d'un fichier/contrôle facilement éditable, **pas** de l'image. Éviter d'écrire un tutoriel qui manipule de faux `Quest` ou des résultats simulés dans la session réelle.

Le tutoriel ne bloque jamais les commandes. Prévoir « Précédent », « Suivant », « Fermer / Passer » et progression `1/4`; focus clavier perceptible ; aide toujours consultable ; pas d'injection de probabilités ni de détails métiers non confirmés.

## D. Paramètres / confort

Utiliser le **panneau existant**, sans inventer une nouvelle grammaire de UI :

- Fenêtré ↔ plein écran (mode fenêtré 1280×720 par défaut si l'écran le permet).
- **Échelle entière** : conserver le rendu net ; pas de slider ×1,5 et aucun flou.
- **Réduire les animations** : coupe/simplifie `AmbientFx`, `CosmeticFx`, transitions, tout en gardant les états et les résultats visibles.
- Contrôles clavier visibles dans l'aide. `Échap` ferme un overlay, puis revient au jeu ; le focus revient au contrôle d'origine.
- **Aucune section audio** : la musique est volontairement écartée pour la salle de cours. Pas d'icônes de son ni de réglage de volume inopérant.
- Les préférences d'affichage sont extérieures au Core ; leur persistance éventuelle via `ConfigFile` ne doit pas enregistrer de données métier.

## E. Fin de partie — `scenes/victory_384x256.png` et `scenes/defeat_384x256.png`

Une fin de partie est déterminée **uniquement** à partir du rapport de Core à la fin du jour 7 :

- **Victoire** : hall lumineux et fête discrète, ambiance or/vert ; « Guilde redressée ! » et valeurs finales réelles. Ne pas attribuer de score supplémentaire.
- **Défaite** : hall assombri, palette prune/carmin, bannière abîmée ; « Bilan inachevé », invitation à revoir les choix, sans faire croire à une perte punitive nouvelle.
- La scène n'est pas affichée après une simple mission échouée. Pas de son ni fanfare.
- Boutons : « Voir le bilan détaillé », « Nouvelle partie », « Retour au titre » ; retour titre ne doit pas perdre la session silencieusement. Les actions nécessitant de recommencer passent par une confirmation.
- Les panneaux affichent objectifs et résultats réels via Core (`FinalReport`). Pas de données fictives utilisées dans le jeu, même si des valeurs d'exemple figurent sur les **maquettes**.

## F. Crédits et identité distribuable

- Badge `branding/guild_crest_{64,128,256}.png`, icône application `branding/app_icon_256.png` (SVG/ICNS/ICO peuvent être fabriqués par l'intégrateur si l'export l'exige).
- Crédits écrits comme données UI : noms des contributions vérifiées et licences ; ne pas inventer de titulaires ; mentionner les générateurs/assistants seulement selon les préférences de l'éditeur.
- Font **Pixeloid Sans déjà installée dans B5.1** (respecter OFL existante) ; ne pas copier de fonts depuis le ZIP ; se référer à la licence du dépôt.
- Une note de commandes de lancement et versions Godot/.NET doit accompagner les archives Linux et Windows.

## G. Micro-composants

- 3 rubans text-free `ui/ribbon_{title,victory,defeat}_192x32.png`, le texte est placé par Godot.
- `ui/divider_160x8.png` pour aides et crédits.
- 11 pictogrammes 16×16 : jouer, aide, réglages, crédits, quitter, retour, plein écran, animation réduite, trophée, clavier, information.
- Les boutons existants de B5 sont **réutilisés**, pas redessinés.
- Pas de PNG supplémentaires de transition : un fondu **120–180 ms max** en Godot est suffisant, facultatif et désactivé en mode animations réduites.

## H. Priorisation et frontières

### B5.3 indispensable

Titre/intro accessibles, aide utile, paramètres simples, fin de partie claire, crédits, icône, exports, tests depuis clone propre, conservation de tous les comportements B5.2. Tous les écrans silencieux.

### Hors B5.3 par défaut

- Sauvegarde/reprise persistante (décision fonctionnelle séparée) ; pas de bouton vide dans la UI.
- Musique / SFX / synthèse sonore ; pas de monitoring d'enceintes, pas de boucle audible.
- Nouveaux scénarios, effets métiers, IA d'aventuriers, progression hors des sept jours.
- Toute dette pédagogique intentionnelle, réservée à la phase C **après** acceptation de la version propre.

## Décision de sortie

B5.3 sera validée sur des **captures Godot réelles**, des parcours utilisateur, la navigation clavier, les logs de démarrage et une distribution Linux + Windows. Les maquettes et ce pack graphique **ne valent pas validation de l'intégration**.
