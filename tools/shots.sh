#!/bin/bash
# Importe les assets (nouveau worktree), reconstruit, puis joue le scénario AutoPlay et enregistre les captures.
# usage : tools/shots.sh <dossier-captures> <chemin-godot-mono>
set -euo pipefail
OUT="$1"; GODOT="$2"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
dotnet build "$ROOT/src/GuildOps.Game" -v q --nologo | tail -3
"$GODOT" --headless --path "$ROOT/src/GuildOps.Game" --import >/dev/null 2>&1 || true
"$ROOT/tools/run-autoplay.sh" "$OUT" "$GODOT" 2>&1 | grep -E "KEYBOARD_CHECK|AUTOPLAY_RESULT|ERROR|SCRIPT ERROR|Exception" || true
