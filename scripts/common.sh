#!/usr/bin/env bash
# Shared helpers for the Linux/macOS scripts. Finds the Godot .NET 4.5.2 executable:
#   1. $GODOT (full path to the editor/binary)
#   2. "godot" / "godot4" / "Godot_v4.5.2-stable_mono_linux.x86_64" in PATH
set -euo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

find_godot() {
  if [[ -n "${GODOT:-}" && -x "${GODOT}" ]]; then echo "${GODOT}"; return 0; fi
  for c in godot godot4 Godot_v4.5.2-stable_mono_linux.x86_64 Godot_v4.5.2-stable_mono_linux.arm64; do
    if command -v "$c" >/dev/null 2>&1; then command -v "$c"; return 0; fi
  done
  echo "Godot .NET 4.5.2 not found. Set GODOT=/path/to/Godot_v4.5.2-stable_mono_linux.x86_64" >&2
  return 1
}

build_csharp() {
  # dotnet build is the fastest path; without the .NET SDK (or if it fails, e.g. offline before the Godot
  # editor registered its bundled NuGet packages) Godot builds the solution itself
  if command -v dotnet >/dev/null 2>&1; then
    if dotnet build "${REPO_ROOT}/ProceduralPixelCreatures.sln" -nologo -v q; then return 0; fi
    echo "dotnet build failed, trying the build via Godot ..." >&2
  fi
  "$(find_godot)" --headless --path "${REPO_ROOT}" --build-solutions --quit
}
