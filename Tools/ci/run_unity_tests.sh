#!/usr/bin/env bash
# =============================================================================
# run_unity_tests.sh — headless Unity Test Framework runner
#
# Runs Edit Mode and/or Play Mode tests through the Unity CLI (-batchmode
# -nographics -runTests), optionally preceded by the content validator
# (-executeMethod). Writes NUnit XML, raw editor logs, and the structured
# regression log that the Tech Lead reads.
#
# Outputs (default locations, all under the project root):
#   Logs/RegressionTest_Output.log        structured run log (all steps, one file)
#   Logs/TestResults/<Platform>-results.xml
#   Logs/TestResults/<Platform>-editor.log
#   Logs/ContentValidation.log            (with --validate)
#
# Exit code: 0 all passed · 1 validation errors · 2 test failures · 3 run error
# =============================================================================
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

usage() {
  cat <<'EOF'
Usage: Tools/ci/run_unity_tests.sh [options] [-- extra Unity args]

  -p, --platform <EditMode|PlayMode|All>   Test mode(s) to run            (default: All)
  -a, --assembly <names>                   Test assemblies, ';'-separated
                                           e.g. "HaoKhiSuViet.Tests.PlayMode"
  -c, --category <names>                   NUnit categories, ';'-separated
                                           (Unit, Critical, Regression, Resmoke)
  -f, --filter <regex>                     Test name filter (-testFilter)
      --repeat <n>                         Run each test n extra times (n+1 total); stops at first failure
      --retry <n>                          Retry failing tests up to n times
      --validate                           Run the content validator (-executeMethod) first
      --results-dir <dir>                  NUnit XML + editor logs  (default: Logs/TestResults)
      --log <file>                         Structured log (default: Logs/RegressionTest_Output.log)
      --append-log                         Append to --log instead of starting fresh
      --unity <path>                       Unity executable (default: $UNITY_PATH or auto-detect)
  -h, --help                               Show this help

Anything after "--" is passed to Unity unchanged, e.g.
  -- -resmokeIterations 50 -resmokeMaxHeapGrowthKB 8192

Examples:
  Tools/ci/run_unity_tests.sh
  Tools/ci/run_unity_tests.sh --validate --platform PlayMode --category "Critical"
  Tools/ci/run_unity_tests.sh -p EditMode -a HaoKhiSuViet.Tests.EditMode
  Tools/ci/run_unity_tests.sh -p PlayMode -f "OngButRegressionTests"
EOF
}

PLATFORM="All"; ASSEMBLIES=""; CATEGORY=""; FILTER=""; REPEAT=""; RETRY=""
VALIDATE=0; APPEND_LOG=0; UNITY=""
RESULTS_DIR="Logs/TestResults"; LOG_FILE="Logs/RegressionTest_Output.log"
EXTRA=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    -p|--platform)   PLATFORM="${2:?}"; shift 2 ;;
    -a|--assembly)   ASSEMBLIES="${2:?}"; shift 2 ;;
    -c|--category)   CATEGORY="${2:?}"; shift 2 ;;
    -f|--filter)     FILTER="${2:?}"; shift 2 ;;
    --repeat)        REPEAT="${2:?}"; shift 2 ;;
    --retry)         RETRY="${2:?}"; shift 2 ;;
    --validate)      VALIDATE=1; shift ;;
    --results-dir)   RESULTS_DIR="${2:?}"; shift 2 ;;
    --log)           LOG_FILE="${2:?}"; shift 2 ;;
    --append-log)    APPEND_LOG=1; shift ;;
    --unity)         UNITY="${2:?}"; shift 2 ;;
    -h|--help)       usage; exit 0 ;;
    --)              shift; EXTRA=("$@"); break ;;
    *)               usage >&2; die "Unknown option: $1" ;;
  esac
done

case "$PLATFORM" in
  EditMode|PlayMode) PLATFORMS=("$PLATFORM") ;;
  All)               PLATFORMS=(EditMode PlayMode) ;;
  *)                 die "--platform must be EditMode, PlayMode or All (got '$PLATFORM')" ;;
esac

if [[ -z "$UNITY" ]]; then
  UNITY="$(detect_unity)" || die "Unity $(project_unity_version) not found. Pass --unity <path> or set UNITY_PATH."
fi
[[ -f "$UNITY" ]] || die "Unity executable not found: $UNITY"

RESULTS_ABS="$(abs_path "$RESULTS_DIR")"
LOG_ABS="$(abs_path "$LOG_FILE")"
mkdir -p "$RESULTS_ABS" "$(dirname "$LOG_ABS")"
[[ $APPEND_LOG -eq 1 ]] || : > "$LOG_ABS"

warn_if_project_open
log_step "Unity:   $UNITY"
echo     "Project: $PROJECT_ROOT"
echo     "Log:     $LOG_ABS"

COMMON_ARGS=(-batchmode -nographics -projectPath "$(native_path "$PROJECT_ROOT")")
OVERALL=0
SUMMARY=()

record() { # exit-code  → keep the most severe
  (( $1 > OVERALL )) && OVERALL=$1
  return 0
}

run_validation() {
  local report="$PROJECT_ROOT/Logs/ContentValidation.log"
  local editor_log="$RESULTS_ABS/validation-editor.log"
  log_step "Content validation (-executeMethod)"

  "$UNITY" "${COMMON_ARGS[@]}" -quit \
    -executeMethod HaoKhiSuViet.EditorCI.ContentValidator.RunFromCommandLine \
    -contentValidationLog "$(native_path "$report")" \
    -logFile "$(native_path "$editor_log")"
  local rc=$?

  if [[ -f "$report" ]]; then
    cat "$report" >> "$LOG_ABS"
    grep -E '\| (ERROR|WARN ) \|' "$report" || true
  else
    echo "No validation report written; tail of editor log:"; tail -n 40 "$editor_log" 2>/dev/null
  fi

  if [[ $rc -eq 0 ]]; then SUMMARY+=("Validation   PASSED"); else SUMMARY+=("Validation   FAILED (exit $rc)"); record 1; fi
}

run_platform() {
  local platform="$1"
  local xml="$RESULTS_ABS/${platform}-results.xml"
  local editor_log="$RESULTS_ABS/${platform}-editor.log"
  rm -f "$xml"

  local args=("${COMMON_ARGS[@]}" -runTests -testPlatform "$platform"
              -testResults "$(native_path "$xml")" -logFile "$(native_path "$editor_log")"
              -regressionLog "$(native_path "$LOG_ABS")" -regressionLogAppend)
  [[ -n "$ASSEMBLIES" ]] && args+=(-assemblyNames "$ASSEMBLIES")
  [[ -n "$CATEGORY"   ]] && args+=(-testCategory "$CATEGORY")
  [[ -n "$FILTER"     ]] && args+=(-testFilter "$FILTER")
  [[ -n "$REPEAT"     ]] && args+=(-repeat "$REPEAT")
  [[ -n "$RETRY"      ]] && args+=(-retry "$RETRY")

  log_step "$platform tests"
  local started=$SECONDS
  "$UNITY" "${args[@]}" ${EXTRA[@]+"${EXTRA[@]}"}
  local rc=$?
  local elapsed=$((SECONDS - started))

  if [[ -f "$xml" ]]; then
    read -r total passed failed skipped _ <<< "$(xml_run_totals "$xml")"
    SUMMARY+=("$(printf '%-12s total=%s passed=%s failed=%s skipped=%s (%ss, exit %s)' "$platform" "$total" "$passed" "$failed" "$skipped" "$elapsed" "$rc")")
    xml_test_cases "$xml" | awk -F'\t' '$2 == "Failed" { print "  FAILED  " $1 }'
  else
    SUMMARY+=("$(printf '%-12s NO RESULTS (exit %s) — compile error or crash, see %s' "$platform" "$rc" "$editor_log")")
    echo "No results file; tail of editor log:"
    tail -n 40 "$editor_log" 2>/dev/null
    [[ $rc -eq 0 ]] && rc=3
  fi

  # Unity: 0 = passed, 2 = test failures, 3 = run error; anything else = run error.
  case "$rc" in 0|2|3) record "$rc" ;; *) record 3 ;; esac
}

[[ $VALIDATE -eq 1 ]] && run_validation
for platform in "${PLATFORMS[@]}"; do run_platform "$platform"; done

log_step "Summary"
printf '  %s\n' "${SUMMARY[@]}"
echo "  Structured log: $LOG_ABS"
echo "  Results:        $RESULTS_ABS"
exit "$OVERALL"
