#!/bin/bash
# Headless GUT test run. Set GODOT_BIN to override the engine binary.
set -e
cd "$(dirname "$0")"

GODOT_BIN="${GODOT_BIN:-/Users/trading/ClaudeWorkspace/tools/Godot.app/Contents/MacOS/Godot}"

if [ ! -x "$GODOT_BIN" ]; then
  echo "Godot binary not found at $GODOT_BIN — set GODOT_BIN" >&2
  exit 1
fi

# First run on a fresh checkout needs an import pass so scripts/resources
# are registered before GUT loads them.
if [ ! -d ".godot" ]; then
  "$GODOT_BIN" --headless --path . --import >/dev/null 2>&1 || true
fi

exec "$GODOT_BIN" --headless --path . -s addons/gut/gut_cmdln.gd -gexit -gdir=res://tests
