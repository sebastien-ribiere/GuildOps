#!/bin/bash
# Lance GuildOps sous Godot .NET en DÉMARRAGE NORMAL (écran titre, sans contournement) avec le parcours de contrôle FlowCheck :
# un joueur virtuel utilise les vrais contrôles (clavier, boutons) du titre jusqu'à la fin de partie. Codes de retour propagés.
# 0 seulement si Godot sort en 0, que tous les contrôles FLOW_*_CHECK sont OK et que le journal Godot ne contient aucune erreur.
#
# usage : tools/run-flow.sh <dossier-captures> [chemin-godot-mono]
set -uo pipefail
SHOTS="$(realpath -m "$1")"
GODOT="${2:-${GODOT_MONO:-godot-mono}}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/src/GuildOps.Game" || exit 2
dotnet build -v q --nologo || { echo "ÉCHEC de la compilation" >&2; exit 2; }
mkdir -p "$SHOTS"
LOG="$SHOTS/godot.log"
"$GODOT" --path . -- --flowcheck --shots="$SHOTS" >"$LOG" 2>&1
GODOT_RC=$?
grep -E "FLOW_" "$LOG"
ERRORS=$(grep -cE "SCRIPT ERROR|^ERROR:|Unhandled exception|System\.[A-Za-z]+Exception" "$LOG")
FAILS=$(grep -cE "_CHECK FAIL" "$LOG")
echo "GODOT_EXIT=$GODOT_RC LOG_ERRORS=$ERRORS CHECK_FAILURES=$FAILS (journal : $LOG)"
if [ "$GODOT_RC" -ne 0 ] || [ "$ERRORS" -ne 0 ] || [ "$FAILS" -ne 0 ] || ! grep -q "FLOW_RESULT" "$LOG"; then echo "ÉCHEC du parcours normal" >&2; exit 1; fi
