#!/bin/bash
# Headless GUT test run. Set GODOT_BIN to override the engine binary.
#
#   ./run-tests.sh          everything, including tests/slow/ (~6 min)
#   ./run-tests.sh --fast   skips tests/slow/ (~45 s)
#
# tests/slow/ holds the whole-simulation tests: the M4 riot DoD (20 seeds x
# 5 sim-days x 2 scenarios) and the 200-agent perf test. They are slow
# because they simulate ~100 prison-days, not because they're wasteful, and
# they are the two tests most likely to catch a real regression in the
# balance of the sim — so the default run includes them. Use --fast while
# iterating, then run the full suite before committing.
set -e
cd "$(dirname "$0")"

GODOT_BIN="${GODOT_BIN:-/Users/trading/ClaudeWorkspace/tools/Godot.app/Contents/MacOS/Godot}"

if [ ! -x "$GODOT_BIN" ]; then
  echo "Godot binary not found at $GODOT_BIN — set GODOT_BIN" >&2
  exit 1
fi

DIRS="res://tests,res://tests/slow"
if [ "$1" = "--fast" ]; then
  DIRS="res://tests"
  echo "(--fast: skipping tests/slow/)"
fi

# New/renamed class_name scripts need an import pass to land in the global
# class cache before GUT loads them — cheap enough to always run.
"$GODOT_BIN" --headless --path . --import >/dev/null 2>&1 || true

exec "$GODOT_BIN" --headless --path . -s addons/gut/gut_cmdln.gd -gexit -gdir="$DIRS"
