#!/bin/bash
# Export the web build and force-push it to gh-pages (served by GitHub Pages).
# Requires the 4.4.1 web export templates installed (see CLAUDE.md).
set -e
cd "$(dirname "$0")"

GODOT_BIN="${GODOT_BIN:-/Users/trading/ClaudeWorkspace/tools/Godot.app/Contents/MacOS/Godot}"
REPO_URL="https://github.com/chickenswaffle/cellblock-holdings.git"

rm -rf build/web
mkdir -p build/web
"$GODOT_BIN" --headless --path . --export-release "Web" build/web/index.html

cd build/web
touch .nojekyll
git init -q
git checkout -q -b gh-pages
git add -A
git commit -q -m "Web build $(git -C ../.. rev-parse --short HEAD)"
git push -q -f "$REPO_URL" gh-pages
echo "Deployed: https://chickenswaffle.github.io/cellblock-holdings/"
