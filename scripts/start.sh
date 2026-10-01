#!/usr/bin/env bash
# Starts the Procedural Pixel Creature Workshop (builds the C# project first).
#   scripts/start.sh            -> workshop
#   scripts/start.sh --testarea -> test area directly
#   scripts/start.sh --overview -> workshop in the overview (up to 100 creatures)
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
GODOT_BIN="$(find_godot)"
build_csharp
if [[ "${1:-}" == "--testarea" ]]; then
  exec "${GODOT_BIN}" --path "${REPO_ROOT}" -- --testarea
fi
if [[ "${1:-}" == "--overview" ]]; then
  exec "${GODOT_BIN}" --path "${REPO_ROOT}" -- --overview
fi
exec "${GODOT_BIN}" --path "${REPO_ROOT}"
