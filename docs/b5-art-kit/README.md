# GuildOps B5 — kit graphique provisoire 16 bits

Ce paquet contient **42 PNG originaux**, générés à dimensions fixes et utilisables dans Godot sans accès réseau.
Le script Python `tools/generate_art.py` est **uniquement un outil de conception** (dépendance Pillow) ; le jeu livré doit charger les PNG déjà générés, sans Python et sans Pillow.

## Contenu

- `assets/backgrounds/guildhall_640x360.png` : décor de quartier général, sans texte ou règles codées dans l'image.
- `assets/portraits/*.png` : **12 portraits RGBA, 64 × 64**, nommés selon les IDs du roster.
- `assets/sprites/*_idle.png` : **12 spritesheets RGBA, 128 × 48**, soit **4 frames 32 × 48** horizontalement. Une animation idle simple suffit pour B5.
- `assets/ui/panel_*_9slice.png` : 3 cadres **48 × 48**, coins de 8 px (Godot `NinePatchRect`, set margins to 8 all sides).
- `assets/ui/button_*.png` : quatre états de bouton, **128 × 32**.
- `assets/ui/portrait_frame_72x72.png` : cadre de portrait.
- `assets/icons/*_16.png` : 9 pictogrammes RGBA, **16 × 16**.
- `assets/manifest.json` : liste de héros, conventions, positions indicatives des hotspots.
- `previews/` : prévisualisations pour la revue, non nécessaires au runtime.
- `references/guildops_fortress_concept.png` : illustration originale de référence (ambiance générale), **pas** un décor cliquable, ni une spritesheet.

## Intégration Godot

1. Copier `assets/` dans `src/GuildOps.Game/Assets/` (si cette PR n'inclut pas encore les binaires).
2. Dans les paramètres d'import, utiliser `Nearest` / pas de filtrage flou et désactiver les mipmaps sur ces petits sprites.
3. Créer des scènes `Control` indépendantes du moteur et les connecter à `GameSession` via un coordinateur de présentation.
4. **Ne jamais dériver le prix, la probabilité, la réputation ou l'éligibilité à partir des PNG ou de scripts de scènes** : afficher les résultats de `GuildOps.Core`.
5. Faire tourner `dotnet test` pour la compatibilité métier et des tests manuels pour les interactions UI.

## Statut artistique

Ces assets sont **provisoires, reproductibles et prêts à intégrer**, mais ne prétendent pas avoir la richesse du concept-art de la forteresse GuildOps généré précédemment. La structure et les couleurs servent de référence ; sprites et décor peuvent être améliorés sans modifier le contrat visuel ni le code métier.

## Licence / provenance

Éléments dessinés pour ce lot GuildOps par génération procédurale originale. Pas de téléchargement ni d'assets tiers. Aucun texte sur les images de production, pas de données pédagogiques confidentielles.
