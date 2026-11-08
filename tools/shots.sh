#!/bin/bash
# Reconstruit, importe les assets (nouveau worktree), puis joue le scénario AutoPlay et enregistre les captures.
# Chaque étape propage son code de retour : aucune erreur n'est masquée.
# usage : tools/shots.sh <dossier-captures> <chemin-godot-mono>
set -euo pipefail
OUT="$1"; GODOT="$2"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
dotnet build "$ROOT/src/GuildOps.Game" -v q --nologo
"$GODOT" --headless --path "$ROOT/src/GuildOps.Game" --import
"$ROOT/tools/run-autoplay.sh" "$OUT" "$GODOT"
