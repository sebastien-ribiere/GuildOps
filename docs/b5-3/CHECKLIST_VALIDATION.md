# GuildOps B5.3 — Checklist de validation

## Livrables artistiques

- [ ] Toutes les images du manifest valides (27 fichiers ; tailles, SHA-256, mode RGBA).
- [ ] Fond titre 640×360, logo déjà peint bien cadré, panneau interactif Godot et non image.
- [ ] Intro / victoire / défaite 384×256, aucune donnée de gameplay incrustée.
- [ ] 4 étapes de tutoriel 192×128, textes libres de se mettre à jour.
- [ ] 11 icônes 16×16, 4 assets branding, rubans et séparateur, aucun asset inutilisé non expliqué.
- [ ] Pas de police ni d'audio dans le ZIP ; on réutilise Pixeloid Sans du dépôt.

## Parcours visuels

- [ ] Premier démarrage, écran titre, aucune lecture audio.
- [ ] Entrer dans le hall sans animation forcée ; ordre de focus clavier prévisible.
- [ ] Intro passée/terminée, partie cohérente, graine et compteurs inchangés.
- [ ] Aide 1/4 → 4/4, retour, fermeture Échap, focus restauré.
- [ ] Fenêtré ↔ plein écran ; net à 1280×720 (×2), aucun flou ni fractionnement.
- [ ] Réduire les animations désactive immédiatement `AmbientFx`/`CosmeticFx`/fondu.
- [ ] Partie complète de 7 jours, victoire et défaite déterminées seulement par Core.
- [ ] « Voir le bilan détaillé », retour titre, nouvelle partie, confirmation de reset.
- [ ] Crédits et licences visibles, pas d'invention de titularité.
- [ ] Aucun écran ne masque les contrôles essentiels : roster, four offers, résultats/incident avec scroll.

## Automatisation / qualité

- [ ] `dotnet build src/GuildOps.Game` (Godot .NET conforme).
- [ ] `dotnet test` (indiquer échecs, ignorés et exclusions, ne pas masquer erreurs).
- [ ] Supprimer `.godot` puis `godot --headless --path src/GuildOps.Game --import` ; démarrage à froid sans erreur de police/import.
- [ ] `--autoplay` réussit sans écran titre ni intervention humaine ; journaux seedés identiques aux références.
- [ ] Vérification clavier et interaction de toutes les modales.
- [ ] Exports et lancements **Linux ET Windows** ; si impossible, indiquer précisément le contrôle manquant.
- [ ] Session unique et `GuildOps.Core` / `game-data` sans modifications.
- [ ] Captures réellement produites par Godot (non les images `previews/` du kit).
- [ ] PR ouverte, non fusionnée ; dernière revue humaine avant tag clean.

## Hors périmètre

- [ ] Sauvegarde de partie persistante **uniquement après décision explicite** ; ne pas ajouter de bouton fantôme.
- [ ] Musique et SFX **exclus**, jeu silencieux pour salle de cours multi-postes.
- [ ] Dette volontaire réservée après validation complète du jeu propre.
