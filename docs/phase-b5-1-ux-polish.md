# GuildOps — B5.1 : ergonomie, lisibilité et mise en scène de l'interface

> **Statut : spécification prête à implémenter par Claude Code, revue utilisateur requise.** Branche `feat/b5-1-ux-polish`. Ne pas fusionner automatiquement. Base : `main` après B5 (PR #7 fusionnée). Ce document ne modifie aucune règle métier et n'ajoute aucune dette intentionnelle.

## 1. Pourquoi cette passe

Les captures du test manuel du 8 novembre 2026 couvrent hall, tableau des contrats, roster 12 héros, mission en cours/terminée et rapport de journée. Le décor et les portraits sont appréciables, mais :
- Silkscreen à 8 px partout : texte trop espacé, lent à lire, descriptions et journaux fatigants.
- Roster : noms longs coupés ("Borborygme Release", "Archibald Platform", "Prudence ProductOwner"), stats difficiles à distinguer, panneau d'affectation vide tant que la sélection n'est pas complète.
- Contrats : quatre grands parchemins quasi identiques, catégorie, compétence, énergie et prime noyées dans le texte ; sélection peu lisible.
- Missions : liste de gauche et panneau de droite avec beaucoup d'espace vide, priorité donnée aux détails techniques plutôt qu'au verdict ; pas de mise en scène de réussite/échec.
- Rapport : journal et trésorerie sans hiérarchie visuelle ; espaces vides.
- Hall : labels flottants et hotspots rectangulaires sans ancrage sémantique, sprites regroupés en bas.

**Objectif B5.1 :** que le joueur repère facilement où cliquer, comprenne les conséquences et lise le texte confortablement, tout en gardant le style 16 bits.

## 2. Garde-fous non négociables

- **Uniquement présentation et ergonomie Godot** : pas de modification des dossiers `src/GuildOps.Core/`, `game-data/` ou tests contractuels métier, sauf accord explicite.
- Une seule `GameSession` dans `GameShell.cs` ; toutes les décisions restent dans le Core. Ne pas recopier de seuils, de probabilités ni de règles d'éligibilité dans une scène. `Preview`, `RegenerationCheck`, `ComplaintCheck`, `Resolve`, `EndDay` sont les sources d'état.
- **Zéro changement de gameplay** : 12 héros disponibles initialement, 4 offres/jour, maximum 3 missions/jour, progression 7 jours, rémunérations/réputation/idempotence inchangées.
- Rendu logique **640 × 360**, mise à l'échelle entière pour 1280 × 720, nearest pixel filtering, Compatibility renderer ; export intégralement hors ligne sur les OS testés.
- Ne pas remplacer les 42 PNG ni changer leurs chemins/noms sans besoin argumenté. Si des assets additionnels sont souhaités, créer une courte demande d'assets dans la PR plutôt que de produire des visuels improvisés.
- Accessibilité clavier, navigation, focus visible, retour au hall, messages d'erreur et conservation des sélections impératifs.
- Les calculs de succès et tirages restent visibles **à la demande** pour la pédagogie, sans cacher ni falsifier la trace.
- Pas de nouveaux défauts, anomalies volontaires, fausses preuves, ni changement de `seed`.

## 3. Spécification écran par écran

### B5.1-A — Typographie et composants partagés (priorité P0)

Fichiers cibles : `src/GuildOps.Game/Ui/UiKit.cs`, `CardButton.cs`, `Cards.cs` (puis usages).

- Garder **Silkscreen** pour titres courts, boutons et petits badges. Employer pour corps de texte / descriptions / journaux une **police pixel lisible** et correctement accentuée, sous licence de redistribution vérifiée (inclure notice de licence) ; sinon proposer d'abord une alternative déjà présente. Prévoir deux styles typographiques centraux (titre, corps), pas des overrides aléatoires.
- Définir des espacements/hauteurs de ligne cohérents en unités logiques. Aucun texte superposé, glyphes accentués corrects, pas de police floue. Rendre les descriptions lisibles sans avoir à ouvrir un tooltip.
- État focus / sélection visible en plus de la couleur, bon contraste sur parchemin clair et panneau sombre.
- Le HUD jour/argent/réputation/missions et les toasts restent visibles et lisibles à 640 × 360 ; aucun clipping sur valeurs longues.

**Acceptance :** "Mme Chausson cherche quelqu'un pour promener Médor. Il mord peu.", "Prudence ProductOwner", descriptions et journal peuvent être lus sans texte coupé ni glyphes cassés.

### B5.1-B — Tableau des contrats (P0)

Fichiers : `Scenes/QuestBoard.cs`, `Ui/Cards.cs`, `Scenes/AssignmentPanel.cs`.

- Quatre offres visibles sans scroll vertical à 640 × 360, distinguer visuellement titre, catégorie/spécialité, énergie, prime et courte description sur **deux lignes maximum** ; les détails complets sont accessibles à la sélection.
- Catégories `Corvée`, `Village`, `Aventure`, `Notable`, `Royale` distinguables par texte + icône/couleur ; ne pas inférer les conditions d'accès à partir des couleurs.
- État sélectionné clairement identifiable, même daltonisme ; mission acceptée visuellement différente.
- Panneau d'affectation : quand héros et/ou mission manquent, montrer **une mini procédure en 2 étapes** et un CTA contextuel (“Choisir un héros” ou “Choisir une quête”), au lieu d'un rectangle vide. Si les deux existent, afficher résumé d'éligibilité, coût, énergie, chance, contrepartie, puis actions. Les contrôles non autorisés restent désactivés d'après Core.
- Le bouton de régénération, avec son coût et sa restriction, reste lisible et explicable.

### B5.1-C — Roster des douze (P0)

Fichiers : `Scenes/HeroRoster.cs`, `Ui/Cards.cs`.

- Conserver la grille **4 colonnes × 3 rangées** des douze, sans scroll à la résolution logique.
- Aucun nom tronqué : nom sur deux lignes si nécessaire, sans renommer les héros du JSON. Afficher spécialité et niveau clairement ; énergie et coût visibles, mais sans masquer le visage.
- Portrait reconnu facilement ; états sélectionné, engagé, incompatible lisibles sans simple dépendance à la couleur. Le héros reste consultable même s'il n'est pas éligible.
- Lorsque l'utilisateur sélectionne une quête puis un héros, la preview vient du Core avec impossibilité expliquée. Les boutons de préparation et d'affectation restent fonctionnels et cohérents avec le tableau des contrats.
- Réserver une hiérarchie visuelle stable (portrait → nom → spécialité/niveau → énergie/coût) ; pas de chevauchement du bord inférieur à 640 × 360.

### B5.1-D — Mission en cours et résultat (P0)

Fichier : `Scenes/QuestResult.cs`.

- Réduire l'espace vide : **liste compacte de missions à gauche**, zone détaillée mieux composée à droite. Pour aucune mission, afficher un état vide utile avec retour possible vers le tableau des contrats.
- En cours : héros + nom de la quête + prime/énergie + action “Résoudre” clairement placée, annonce que le tirage intervient à la résolution. Ne pas afficher un faux pourcentage ou une fausse attente de simulation.
- Terminée : priorité au **verdict SUCCÈS/ÉCHEC**, prime, énergie, delta de réputation et incident; ajout d'une petite transition purement cosmétique possible, mais sans tirage ni mutation.
- Le **tirage (ex. 18 pour 90 %)**, le détail de chance et la cause de réputation restent consultables dans un volet “Détails du calcul” par bouton pliable (visible au clavier). L'événement narratif s'affiche seulement s'il existe dans les résultats actuels ; pas de récit inventé.
- Une réclamation liée propose toujours le lien vers la scène de traitement.
- Aucune modification de résultat si l'utilisateur ouvre/ferme les détails ou navigue entre missions.

### B5.1-E — Journal, trésorerie, plaintes (P1)

Fichiers : `Scenes/DayReport.cs`, `Scenes/ComplaintDialog.cs`.

- Rapport : hiérarchie (état général du jour → métriques → journal), structurer les lignes de journal par jour si cela reste un simple regroupement d'affichage ; scroll et focus fonctionnels.
- Faire ressortir les variations de réputation et la condition de victoire à la fin du jour 7, **sans** changer les métriques.
- Plainte : rendre le coût et les conséquences de chaque réponse lisibles avant validation, respecter `ComplaintCheck`.
- Écrans vides informatifs ; grands espaces vides réduits sans surcharge.

### B5.1-F — Hall de guilde (P1)

Fichier : `Scenes/GuildHall.cs`.

- Les **cinq interactions actuelles** restent cliquables et accessibles au clavier ; ajuster leurs zones aux éléments du décor (contrats, héros, missions, réclamations, trésorerie/bilan), garder tooltips et focus visible.
- Remplacer les “étiquettes flottantes” trop techniques par des libellés courts ancrés visuellement, éventuellement visibles au survol/focus ; pas de texte incrusté dans l'image.
- Répartir les 12 sprites en petits groupes dans la salle, aux positions non conflictuelles avec les hotspots, **sans ajout de déplacement libre ou collision/pathfinding**. Indisponibilité toujours reflétée.
- Les états/compteurs sont dérivés du Core ; pas de valeurs artificielles.

## 4. Parcours de validation obligatoires (DoD)

1. Lancer Godot et vérifier les **six vues** (Hall, Contrats, Héros, Missions, Réclamations, Rapport), sans exceptions UI.
2. Au jour 1 : quatre offres affichées, 12 héros accessibles, aucun nom tronqué, preview et état d'affectation corrects.
3. Parcours assigner → mission en cours → résoudre → succès/échec → détails du calcul → éventuelle plainte → réponse → fin de journée ; états et montants identiques aux appels Core.
4. Vérifier un refus d'affectation et un clic de résolution répété : la présentation n'introduit pas de mutation supplémentaire.
5. Parcours seedé complet sur 7 jours jusqu'au bilan, victoire et échec selon scénarios de référence (ne pas inventer les résultats).
6. Clavier uniquement : tabulations, activation, sélection et retour vers le hall ; focus conservé lors des rafraîchissements.
7. Captures réelles à 1280 × 720 des écrans Hall / Contrats / Roster / Mission en cours / Mission finie / Rapport / Plainte, avec des données représentatives et comparaison avant/après.
8. `dotnet test` et `dotnet build src/GuildOps.Game` exécutés ; Godot lancé, exports testés où l'environnement le permet. Documenter les commandes **réellement exécutées** et les limites.
9. Diff de PR : essentiellement `src/GuildOps.Game/Ui` et `Scenes` et éventuels fichiers de polices libres + licences ; **aucune** modification `GuildOps.Core` ou `game-data`.

## 5. Découpage de réalisation conseillé

- **Commit 1** : système typographique partagé + carte du roster (P0).
- **Commit 2** : tableau des contrats et panneau d'affectation (P0).
- **Commit 3** : écran des missions et résultats (P0).
- **Commit 4** : journal, plaintes et hall (P1).
- **Commit 5** : captures, preuves, tests, correctifs d'ergonomie.

**À ne pas faire** : transformer la scène Godot en écran DPI flottant, réécrire `GameSession`, cacher les métriques nécessaires aux étudiants, ou multiplier les nouveaux systèmes de jeu.

## 6. Prompt prêt pour Claude Code

> Dans le dépôt `sebastien-ribiere/GuildOps`, travaille sur la branche `feat/b5-1-ux-polish`. Lis `docs/phase-b5-1-ux-polish.md`, `docs/phase-b5-visual-implementation.md`, les scènes et `UiKit.cs`, puis propose une très courte table d'impact avant d'implémenter. Livrer **B5.1**, uniquement la présentation Godot, sans changement `GuildOps.Core` ou `game-data`, sans dette intentionnelle. Priorise lisibilité de la police, noms non tronqués sur les 12 cartes, contextualisation de l'affectation et meilleure hiérarchie des missions/résultats. Conserve toutes les commandes et états de la V0 et l'explicabilité du tirage. Fais des commits lisibles sur la même branche, captures et tests réels dans la PR ; ne fusionne pas sans revue.
