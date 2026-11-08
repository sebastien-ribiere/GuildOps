#!/bin/bash
# Teste l'archive LINUX dans un environnement propre : répertoire vide, HOME vierge, PATH sans dotnet (le runtime embarqué doit suffire),
# aucun cache .godot. Exécute le binaire exporté : scénarios AutoPlay de référence, parcours normal (FlowCheck) et démarrage simple.
# Chaque code de retour est affiché ; le script sort en erreur au moindre échec.
#
# usage : tools/test-dist.sh <GuildOps-…-linux-x86_64.tar.gz> [dossier-de-travail]
set -uo pipefail
ARCHIVE="$(realpath "$1")"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="$(realpath -m "${2:-$(mktemp -d)}")"
rm -rf "$WORK"; mkdir -p "$WORK/x" "$WORK/home" "$WORK/emptybin"
tar xzf "$ARCHIVE" -C "$WORK/x" || { echo "extraction impossible"; exit 2; }
APP="$(find "$WORK/x" -maxdepth 2 -name GuildOps.x86_64 | head -1)"
[ -x "$APP" ] || { echo "GuildOps.x86_64 introuvable ou non exécutable"; exit 2; }
cd "$(dirname "$APP")" || exit 2
FAIL=0
run() { env -i HOME="$WORK/home" PATH="$WORK/emptybin" DISPLAY="${DISPLAY:-:0}" XAUTHORITY="${XAUTHORITY:-}" XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-}" "$@"; }
note() { echo "[$1] $2"; [ "$1" = OK ] || FAIL=1; }

echo "== Environnement propre =="
echo "environnement : HOME=$WORK/home, PATH=$WORK/emptybin (dotnet absent : $(command -v dotnet >/dev/null && PATH="$WORK/emptybin" command -v dotnet >/dev/null && echo non || echo oui))"
# Le contenu du PCK embarqué (27 PNG B5.3, aucun audio, ni sources/ ni previews/) est vérifié PAR LE JEU lui-même (FLOW_ASSETS_CHECK ci-dessous).

echo "== AutoPlay (binaire exporté) =="
for policy in first-fit greedy-repair idle; do
  run ./GuildOps.x86_64 -- --autoplay --policy=$policy --shots="$WORK/shots-$policy" >"$WORK/autoplay-$policy.log" 2>&1
  rc=$?
  result="$(grep AUTOPLAY_RESULT "$WORK/autoplay-$policy.log")"
  errors=$(grep -cE "SCRIPT ERROR|^ERROR:|Unhandled exception|System\.[A-Za-z]+Exception|_CHECK FAIL" "$WORK/autoplay-$policy.log")
  expected=""
  case $policy in
    first-fit) expected="day=7 treasury=2077 reputation=87 succeeded=18 events=113 status=Finished success=True" ;;
    greedy-repair) expected="day=7 treasury=1707 reputation=64 succeeded=15 events=107 status=Finished success=True" ;;
    idle) expected="day=7 treasury=1200 reputation=35 succeeded=0 events=22 status=Finished success=False" ;;
  esac
  if [ $rc -eq 0 ] && [ "$errors" -eq 0 ] && [[ "$result" == *"$expected"* ]]; then note OK "autoplay $policy : code $rc, 0 erreur, résultat de référence ($expected)"
  else note KO "autoplay $policy : code $rc, $errors erreur(s), résultat « $result »"; fi
done
if diff <(grep -E '^- #' "$ROOT/docs/playtests/seed-1142026-RepairIfPossible.md") "$WORK/shots-greedy-repair/journal.txt" >/dev/null; then note OK "journal greedy-repair identique à docs/playtests/seed-1142026-RepairIfPossible.md"; else note KO "journal greedy-repair différent de la référence"; fi

echo "== Parcours normal (FlowCheck, binaire exporté) =="
run ./GuildOps.x86_64 -- --flowcheck --shots="$WORK/shots-flow" >"$WORK/flow.log" 2>&1
rc=$?
fails=$(grep -c "_CHECK FAIL" "$WORK/flow.log"); oks=$(grep -c "_CHECK OK" "$WORK/flow.log")
errors=$(grep -cE "SCRIPT ERROR|^ERROR:|Unhandled exception|System\.[A-Za-z]+Exception" "$WORK/flow.log")
if [ $rc -eq 0 ] && [ "$fails" -eq 0 ] && [ "$errors" -eq 0 ] && grep -q "FLOW_RESULT failures=0" "$WORK/flow.log"; then note OK "flowcheck : code $rc, $oks contrôles OK, 0 échec, 0 erreur de journal"
else note KO "flowcheck : code $rc, $fails échec(s), $errors erreur(s) (voir $WORK/flow.log)"; fi
grep "FLOW_INFO" "$WORK/flow.log"

echo "== Démarrage simple (240 images puis arrêt propre) =="
timeout -s KILL 60 env -i HOME="$WORK/home" PATH="$WORK/emptybin" DISPLAY="${DISPLAY:-:0}" XAUTHORITY="${XAUTHORITY:-}" XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-}" ./GuildOps.x86_64 --quit-after 240 >"$WORK/start.log" 2>&1
rc=$?
errors=$(grep -cE "SCRIPT ERROR|^ERROR:|Unhandled exception|System\.[A-Za-z]+Exception" "$WORK/start.log")
if [ $rc -eq 0 ] && [ "$errors" -eq 0 ] && grep -q "GuildOps prêt" "$WORK/start.log"; then note OK "démarrage normal sans option : écran titre affiché 240 images, arrêt propre (code $rc), 0 erreur de journal"
else note KO "démarrage : code $rc, $errors erreur(s) (voir $WORK/start.log)"; fi

echo "journaux et captures : $WORK"
[ $FAIL -eq 0 ] && echo "TEST_DIST_RESULT OK" || echo "TEST_DIST_RESULT ÉCHEC"
exit $FAIL
