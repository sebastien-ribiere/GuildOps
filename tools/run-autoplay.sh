#!/bin/bash
# Lance GuildOps sous Godot .NET avec le scénario AutoPlay (mêmes commandes que l'UI), enregistre les captures et propage le code
# de retour : 0 seulement si Godot sort en 0, que tous les contrôles *_CHECK sont OK, que le journal Godot ne contient aucune erreur
# de script et, pour les parties de référence, que le journal de partie est identique à docs/playtests/.
# Le journal Godot complet est écrit dans <dossier-captures>/godot.log, le journal de partie dans <dossier-captures>/journal.txt.
#
# usage : tools/run-autoplay.sh <dossier-captures> [chemin-godot-mono] [politique]
#   politique : first-fit (défaut, parité UiParityTests) | greedy-ignore | greedy-apologize | greedy-repair | idle
set -uo pipefail
SHOTS="$(realpath -m "$1")"
GODOT="${2:-${GODOT_MONO:-godot-mono}}"
POLICY="${3:-first-fit}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/src/GuildOps.Game" || exit 2
# Jamais de binaire périmé : un échec de compilation arrête tout (sinon Godot rejouerait l'ancienne DLL).
dotnet build -v q --nologo || { echo "ÉCHEC de la compilation" >&2; exit 2; }
mkdir -p "$SHOTS"
LOG="$SHOTS/godot.log"
"$GODOT" --path . -- --autoplay --shots="$SHOTS" --policy="$POLICY" >"$LOG" 2>&1
GODOT_RC=$?
grep -E "_CHECK |AUTOPLAY_" "$LOG"
ERRORS=$(grep -cE "SCRIPT ERROR|^ERROR:|Unhandled exception|System\.[A-Za-z]+Exception" "$LOG")
FAILS=$(grep -cE "_CHECK FAIL" "$LOG")
echo "GODOT_EXIT=$GODOT_RC LOG_ERRORS=$ERRORS CHECK_FAILURES=$FAILS (journal : $LOG)"
STATUS=0
if [ "$GODOT_RC" -ne 0 ] || [ "$ERRORS" -ne 0 ] || [ "$FAILS" -ne 0 ] || ! grep -q "AUTOPLAY_RESULT" "$LOG"; then STATUS=1; fi

case "$POLICY" in
  greedy-ignore)    REF="$ROOT/docs/playtests/seed-1142026-Ignore.md" ;;
  greedy-apologize) REF="$ROOT/docs/playtests/seed-1142026-Apologize.md" ;;
  greedy-repair)    REF="$ROOT/docs/playtests/seed-1142026-RepairIfPossible.md" ;;
  *)                REF="" ;;
esac
if [ -n "$REF" ]; then
  if diff <(grep -E '^- #' "$REF") "$SHOTS/journal.txt" >"$SHOTS/journal.diff"; then
    echo "REFERENCE_JOURNAL_CHECK OK — journal identique à $(basename "$REF") ($(wc -l <"$SHOTS/journal.txt") lignes)"
  else
    echo "REFERENCE_JOURNAL_CHECK FAIL — écarts avec $(basename "$REF") : voir $SHOTS/journal.diff" >&2
    STATUS=1
  fi
fi
[ "$STATUS" -ne 0 ] && echo "ÉCHEC du scénario AutoPlay ($POLICY)" >&2
exit "$STATUS"
