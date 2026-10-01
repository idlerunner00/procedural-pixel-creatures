#!/usr/bin/env bash
# Full verification: C# build, engine independent core tests (console harness, if the .NET SDK is
# installed) and the Godot self test (headless). Exit code != 0 if anything fails.
#   scripts/check.sh [--quick]
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
GODOT_BIN="$(find_godot)"
QUICK=""
[[ "${1:-}" == "--quick" ]] && QUICK="--quick"
REPORTS="${REPO_ROOT}/reports"
mkdir -p "${REPORTS}"
status=0

echo "== 1/3 C# build =="
build_csharp || status=1

if command -v dotnet >/dev/null 2>&1; then
  echo "== 2/3 Core tests (console harness, without Godot) =="
  dotnet run -c Release --project "${REPO_ROOT}/tools/core_harness/CoreHarness.csproj" -- test "${REPORTS}" all "$([[ -n "$QUICK" ]] && echo 10 || echo 100)" || status=1
else
  echo "== 2/3 Core tests skipped (no .NET SDK; the Godot self-test contains the same tests) =="
fi

echo "== 3/3 Godot self-test (headless) =="
"${GODOT_BIN}" --headless --audio-driver Dummy --path "${REPO_ROOT}" -- --self-test ${QUICK} --report="${REPORTS}/self_test_report.json" || status=1

if [[ $status -eq 0 ]]; then echo "ALL CHECKS PASSED"; else echo "CHECKS FAILED (see above and ${REPORTS})"; fi
exit $status
