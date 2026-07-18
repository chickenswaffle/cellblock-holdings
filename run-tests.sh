#!/bin/bash
# Headless GUT test run. Set GODOT_BIN to override the engine binary.
set -e
cd "$(dirname "$0")"

GODOT_BIN="${GODOT_BIN:-/Users/trading/ClaudeWorkspace/tools/Godot.app/Contents/MacOS/Godot}"

if [ ! -x "$GODOT_BIN" ]; then
  echo "Godot binary not found at $GODOT_BIN — set GODOT_BIN" >&2
  exit 1
fi

# New/renamed class_name scripts need an import pass to land in the global
# class cache before GUT loads them — cheap enough to always run.
"$GODOT_BIN" --headless --path . --import >/dev/null 2>&1 || true

exec "$GODOT_BIN" --headless --path . -s addons/gut/gut_cmdln.gd -gexit -gdir=res://tests
