#!/usr/bin/env bash
# Performance benchmark (populations 1/25/100) or soak test. Reports go to reports/.
#   scripts/benchmark.sh                 -> benchmark in a window (real GPU, V-Sync off)
#   scripts/benchmark.sh --headless      -> CPU side only (server/CI)
#   scripts/benchmark.sh --soak [min]    -> soak test
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
GODOT_BIN="$(find_godot)"
build_csharp
mkdir -p "${REPO_ROOT}/reports"
HEADLESS=()
if [[ "${1:-}" == "--headless" ]]; then HEADLESS=(--headless --audio-driver Dummy); shift; fi
if [[ "${1:-}" == "--soak" ]]; then
  exec "${GODOT_BIN}" "${HEADLESS[@]}" --path "${REPO_ROOT}" -- --soak --minutes="${2:-10}" --report="${REPO_ROOT}/reports/soak.json"
fi
exec "${GODOT_BIN}" "${HEADLESS[@]}" --path "${REPO_ROOT}" -- --benchmark --report="${REPO_ROOT}/reports/benchmark.json" "$@"
