#!/usr/bin/env bash
# Shared helpers for the Unity CI scripts. Sourced, not executed.

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

# Git Bash / MSYS rewrites anything that looks like a POSIX path when calling a
# native .exe. We convert paths ourselves (native_path), so turn that off.
export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL="*"

is_windows() { [[ "${OSTYPE:-}" == msys* || "${OSTYPE:-}" == cygwin* || "${OSTYPE:-}" == win32* ]]; }

# Path as the Unity executable expects it (C:\... on Windows, unchanged elsewhere).
native_path() {
  if is_windows && command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s\n' "$1"; fi
}

# Path usable by bash builtins (/d/... on Windows, unchanged elsewhere).
posix_path() {
  if is_windows && command -v cygpath >/dev/null 2>&1; then cygpath -u "$1"; else printf '%s\n' "$1"; fi
}

# Resolve a path relative to the project root unless it is already absolute.
abs_path() {
  case "$1" in
    /*|[A-Za-z]:*) posix_path "$1" ;;
    *) printf '%s\n' "$PROJECT_ROOT/$1" ;;
  esac
}

log_step() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
log_warn() { printf '\033[1;33mWARN:\033[0m %s\n' "$*" >&2; }
die()      { printf '\033[1;31mERROR:\033[0m %s\n' "$*" >&2; exit 64; }

project_unity_version() {
  sed -n 's/^m_EditorVersion: *//p' "$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt" | tr -d '\r'
}

# Finds the editor matching ProjectVersion.txt. Order: $UNITY_PATH, Unity Hub's
# custom install folder (secondaryInstallPath.json), then default Hub locations.
detect_unity() {
  if [[ -n "${UNITY_PATH:-}" ]]; then posix_path "$UNITY_PATH"; return 0; fi

  local version candidates=()
  version="$(project_unity_version)"
  [[ -n "$version" ]] || return 1

  local hub_setting="${APPDATA:-}/UnityHub/secondaryInstallPath.json"
  if [[ -n "${APPDATA:-}" && -f "$hub_setting" ]]; then
    local hub_dir
    hub_dir="$(tr -d '"\r\n' < "$hub_setting" | sed 's#\\\\#/#g; s#\\#/#g')"
    [[ -n "$hub_dir" ]] && candidates+=("$(posix_path "$hub_dir")/$version/Editor/Unity.exe")
  fi

  candidates+=(
    "/c/Program Files/Unity/Hub/Editor/$version/Editor/Unity.exe"
    "/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity"
    "$HOME/Unity/Hub/Editor/$version/Editor/Unity"
  )

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -f "$candidate" ]]; then printf '%s\n' "$candidate"; return 0; fi
  done
  return 1
}

# Batch mode cannot open a project the editor already has open.
warn_if_project_open() {
  if [[ -f "$PROJECT_ROOT/Temp/UnityLockfile" ]]; then
    log_warn "Temp/UnityLockfile exists. If the Unity Editor has this project open, close it first — batch mode will refuse to open a locked project."
  fi
}

# Prints "total passed failed skipped duration" from an NUnit3 results file.
xml_run_totals() {
  local line
  line="$(grep -o '<test-run [^>]*>' "$1" | head -n 1)"
  local attr
  for attr in total passed failed skipped duration; do
    printf '%s ' "$(printf '%s' "$line" | sed -n "s/.* $attr=\"\([^\"]*\)\".*/\1/p")"
  done
  printf '\n'
}

# Prints "fullname<TAB>result" for every test case in an NUnit3 results file.
xml_test_cases() {
  grep -o '<test-case [^>]*>' "$1" \
    | sed -n 's/.* fullname="\([^"]*\)".* result="\([^"]*\)".*/\1\t\2/p'
}
