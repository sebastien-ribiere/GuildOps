#!/bin/bash
# Lance GuildOps sous Godot .NET avec le scénario AutoPlay (7 jours, mêmes commandes que l'UI) et enregistre les captures.
# usage : tools/run-autoplay.sh <dossier-captures> [chemin-godot-mono]
set -euo pipefail
SHOTS="$(realpath -m "$1")"
GODOT="${2:-${GODOT_MONO:-godot-mono}}"
cd "$(dirname "$0")/../src/GuildOps.Game"
mkdir -p "$SHOTS"
"$GODOT" --path . -- --autoplay --shots="$SHOTS"
