#!/usr/bin/env bash
# =============================================================================
# resmoke.sh — repeated critical-path runs to expose flaky tests and leaks
#
# Three layers of repetition:
#   1. --runs      fresh Unity processes (catches process/order-dependent flakes)
#   2. --repeat    UTF -repeat: each test run N extra times in one process
#                  (catches state leaking between runs of the same test)
#   3. --iterations / --reload-cycles  loops inside the Resmoke test fixture
#                  (bus subscribers, object/heap growth, resubscribe after reload)
#
# After all runs every test case is classified:
#   STABLE   passed in every run      FLAKY   passed in some, failed in others
#   FAILING  failed in every run
#
# Outputs: Logs/Resmoke/<timestamp>/{run_N/, cases.tsv, resmoke_summary.txt}
#          Logs/Resmoke_Output.log   all runs' structured logs, concatenated
#
# Exit code: 0 all stable · 1 flaky tests · 2 consistently failing tests · 3 run error
# =============================================================================
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

usage() {
  cat <<'EOF'
Usage: Tools/ci/resmoke.sh [options] [-- extra Unity args]

      --runs <n>            Fresh Unity processes; 0 = keep going until a run fails (default: 3)
      --repeat <n>          UTF -repeat: n extra runs of each test per process        (default: 3)
      --iterations <n>      Soak cycles in ResmokeTests (-resmokeIterations)          (default: 25)
      --reload-cycles <n>   Bus reset + scene load cycles (-resmokeReloadCycles)      (default: 5)
      --max-heap-kb <n>     Allowed heap growth in the soak (-resmokeMaxHeapGrowthKB) (default: 4096)
  -c, --category <names>    Categories to run                               (default: "Critical;Resmoke")
  -p, --platform <mode>     EditMode | PlayMode | All                                  (default: PlayMode)
      --stop-on-fail        Stop after the first failing run
      --unity <path>        Unity executable (default: $UNITY_PATH or auto-detect)
  -h, --help

Examples:
  Tools/ci/resmoke.sh
  Tools/ci/resmoke.sh --runs 10 --repeat 5 --iterations 100
  Tools/ci/resmoke.sh --runs 0                 # loop until something fails (Ctrl+C to stop)
EOF
}

RUNS=3; REPEAT=3; ITERATIONS=25; RELOAD_CYCLES=5; MAX_HEAP_KB=4096
CATEGORY="Critical;Resmoke"; PLATFORM="PlayMode"; STOP_ON_FAIL=0; UNITY_ARG=()
EXTRA=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --runs)          RUNS="${2:?}"; shift 2 ;;
    --repeat)        REPEAT="${2:?}"; shift 2 ;;
    --iterations)    ITERATIONS="${2:?}"; shift 2 ;;
    --reload-cycles) RELOAD_CYCLES="${2:?}"; shift 2 ;;
    --max-heap-kb)   MAX_HEAP_KB="${2:?}"; shift 2 ;;
    -c|--category)   CATEGORY="${2:?}"; shift 2 ;;
    -p|--platform)   PLATFORM="${2:?}"; shift 2 ;;
    --stop-on-fail)  STOP_ON_FAIL=1; shift ;;
    --unity)         UNITY_ARG=(--unity "${2:?}"); shift 2 ;;
    -h|--help)       usage; exit 0 ;;
    --)              shift; EXTRA=("$@"); break ;;
    *)               usage >&2; die "Unknown option: $1" ;;
  esac
done

STAMP="$(date +%Y%m%d_%H%M%S)"
OUT="$PROJECT_ROOT/Logs/Resmoke/$STAMP"
CASES="$OUT/cases.tsv"
SUMMARY_FILE="$OUT/resmoke_summary.txt"
COMBINED_LOG="$PROJECT_ROOT/Logs/Resmoke_Output.log"
mkdir -p "$OUT"
: > "$CASES"
: > "$COMBINED_LOG"

RUNNER="$(dirname "${BASH_SOURCE[0]}")/run_unity_tests.sh"
WORST_RUN=0
RUN_LINES=()
run=0

log_step "Resmoke: runs=$RUNS repeat=$REPEAT iterations=$ITERATIONS reloadCycles=$RELOAD_CYCLES category='$CATEGORY' platform=$PLATFORM"
echo "Output: $OUT"

while :; do
  run=$((run + 1))
  run_dir="$OUT/run_$run"
  run_log="$run_dir/regression.log"
  mkdir -p "$run_dir"

  log_step "Run $run$( [[ $RUNS -gt 0 ]] && printf ' / %s' "$RUNS" )"
  started=$SECONDS
  bash "$RUNNER" ${UNITY_ARG[@]+"${UNITY_ARG[@]}"} \
    --platform "$PLATFORM" --category "$CATEGORY" --repeat "$REPEAT" \
    --results-dir "$run_dir" --log "$run_log" \
    -- -resmokeIterations "$ITERATIONS" -resmokeReloadCycles "$RELOAD_CYCLES" \
       -resmokeMaxHeapGrowthKB "$MAX_HEAP_KB" ${EXTRA[@]+"${EXTRA[@]}"}
  rc=$?
  (( rc > WORST_RUN )) && WORST_RUN=$rc

  for xml in "$run_dir"/*-results.xml; do
    [[ -f "$xml" ]] || continue
    xml_test_cases "$xml" | awk -v run="$run" -F'\t' '{ print run "\t" $1 "\t" $2 }' >> "$CASES"
  done

  leaks=$(grep -c 'LEAK?' "$run_log" 2>/dev/null); leaks=${leaks:-0}
  RUN_LINES+=("$(printf 'run %-3s exit=%s  duration=%ss  subscriberLeakWarnings=%s' "$run" "$rc" "$((SECONDS - started))" "$leaks")")

  { printf '\n########## RESMOKE RUN %s (exit %s) ##########\n' "$run" "$rc"; cat "$run_log" 2>/dev/null; } >> "$COMBINED_LOG"

  if [[ $rc -ne 0 && ( $STOP_ON_FAIL -eq 1 || $RUNS -eq 0 ) ]]; then
    log_warn "Run $run failed — stopping."
    break
  fi
  [[ $RUNS -gt 0 && $run -ge $RUNS ]] && break
done

# ── Classify every test case across runs ────────────────────────────────────
CLASSIFIED="$(awk -F'\t' '
  { key = $2; seen[key]++; if ($3 == "Passed") pass[key]++; else if ($3 == "Failed") fail[key]++ }
  END {
    for (k in seen) {
      p = pass[k] + 0; f = fail[k] + 0
      status = (f == 0) ? "STABLE" : (p == 0 ? "FAILING" : "FLAKY")
      printf "%-8s pass=%-3d fail=%-3d %s\n", status, p, f, k
    }
  }' "$CASES" | sort)"

flaky=$(grep -c '^FLAKY' <<< "$CLASSIFIED")
failing=$(grep -c '^FAILING' <<< "$CLASSIFIED")
stable=$(grep -c '^STABLE' <<< "$CLASSIFIED")

{
  echo "Resmoke $STAMP  runs=$run repeat=$REPEAT iterations=$ITERATIONS reloadCycles=$RELOAD_CYCLES category='$CATEGORY' platform=$PLATFORM"
  echo
  printf '%s\n' "${RUN_LINES[@]}"
  echo
  echo "stable=$stable flaky=$flaky failing=$failing"
  echo
  grep -v '^STABLE' <<< "$CLASSIFIED" || true
  echo
  grep '^STABLE' <<< "$CLASSIFIED" || true
} > "$SUMMARY_FILE"

log_step "Resmoke summary"
cat "$SUMMARY_FILE"
echo
echo "Summary:  $SUMMARY_FILE"
echo "Full log: $COMBINED_LOG"

if   [[ $failing -gt 0 ]]; then exit 2
elif [[ $flaky   -gt 0 ]]; then exit 1
elif [[ $WORST_RUN -ge 3 || ! -s "$CASES" ]]; then exit 3
fi
exit 0
