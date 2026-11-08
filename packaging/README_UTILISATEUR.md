# GuildOps — édition B5.3

Jeu de gestion de guilde fantasy en pixel art. Vous reprenez une guilde en difficulté : vous avez **7 jours** pour choisir des contrats,
affecter vos aventuriers, résoudre les missions et rendre des comptes au Conseil royal.

- 100 % hors ligne : aucun compte, aucune connexion réseau.
- **Totalement silencieux** : ni musique ni effet sonore (pensé pour une salle de classe).
- Aucune installation : le moteur et l'environnement .NET sont embarqués.

## Lancer le jeu

**Linux (x86_64)** — archive `GuildOps-B5.3-linux-x86_64.tar.gz`

```bash
tar xzf GuildOps-B5.3-linux-x86_64.tar.gz
cd GuildOps-B5.3-linux-x86_64
./GuildOps.x86_64          # si besoin : chmod +x GuildOps.x86_64
```

**Windows 10 ou 11 (x64)** — archive `GuildOps-B5.3-windows-x86_64.zip`

1. Faites un clic droit sur le zip, « Extraire tout… », dans un dossier de votre choix (pas depuis l'aperçu de l'archive).
2. Double-cliquez sur `GuildOps.exe`.
3. Le programme n'est pas signé : Windows peut afficher « Windows a protégé votre ordinateur » ; choisissez « Informations complémentaires », puis « Exécuter quand même ».

Gardez ensemble `GuildOps.x86_64` / `GuildOps.exe` et le dossier `data_GuildOps.Game_…` qui l'accompagne : l'un ne fonctionne pas sans l'autre.

Configuration : processeur x86_64, carte graphique compatible OpenGL 3.3 (moteur en mode « Compatibilité »), écran d'au moins 1280×720 recommandé.

## Jouer

1. À l'écran titre : **Nouvelle partie**, puis l'introduction (vous pouvez la passer).
2. Dans le hall, cliquez sur les zones : **Contrats**, **Aventuriers**, **Missions**, **Réclamations**, **Trésorerie**.
3. Choisissez une offre, un héros, puis affectez ; résolvez la mission dans **Missions** ; traitez les réclamations ; terminez la journée.
4. Au jour 7, le Conseil royal rend son bilan : victoire ou défaite, puis retour au titre ou nouvelle partie.

L'**aide en 4 pages** est toujours disponible : bouton « Aide » en haut de l'écran, touche **F1**, ou « Aide et règles » au titre.

| Commande | Effet |
|---|---|
| Souris | cliquer sur les zones, cartes et boutons |
| Tab / Maj+Tab ou flèches | passer d'un contrôle à l'autre |
| Entrée ou Espace | valider le contrôle sélectionné |
| Échap | fermer l'écran ouvert, sinon retourner au hall |
| F1 | ouvrir l'aide |

**Paramètres** (depuis le titre ; en jeu, bouton « Menu ») : plein écran, taille de fenêtre (échelles entières uniquement, pour des pixels nets),
« Réduire les animations » (coupe l'ambiance et les effets visuels ; sans aucune conséquence sur les règles ni sur les résultats).

## À savoir

- **Il n'y a pas de sauvegarde.** « Menu » ramène au titre, où « Reprendre » retrouve la partie **en mémoire** ; fermer le jeu l'efface.
- La partie démarre toujours de la même graine : deux joueurs qui font les mêmes choix obtiennent les mêmes résultats.
- Les préférences d'affichage sont enregistrées dans `display.cfg`, dans le dossier de données utilisateur de Godot
  (Linux : `~/.local/share/godot/app_userdata/GuildOps/` ; Windows : `%APPDATA%\Godot\app_userdata\GuildOps\`). Supprimez ce fichier pour revenir aux réglages par défaut.

## Modes de test (formateurs)

Les arguments se placent après `--` :

```bash
./GuildOps.x86_64 -- --autoplay                       # joue la partie de référence sans interface bloquante, puis quitte (0 = tous les contrôles OK)
./GuildOps.x86_64 -- --autoplay --policy=idle         # autres politiques : first-fit, greedy-ignore, greedy-apologize, greedy-repair, idle
./GuildOps.x86_64 -- --flowcheck                      # parcours normal automatisé : titre, introduction, aide, paramètres, crédits, 7 jours, victoire et défaite
```

Ajoutez `--shots=<dossier>` pour enregistrer des captures. Ces modes ne modifient ni les règles ni les préférences.

## Licences

Voir le dossier `LICENSES/` et le fichier `INVENTAIRE_RESSOURCES_ET_LICENCES.md`. Les crédits sont aussi dans le jeu (« Crédits »).
