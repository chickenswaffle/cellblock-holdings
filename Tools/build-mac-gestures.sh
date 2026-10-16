#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
[[ "$(uname -s)" == Darwin ]] || { echo 'Build the AppKit gesture plugin on macOS.' >&2; exit 1; }
bundle="$project_root/Assets/Plugins/macOS/CellblockGestures.bundle"
mkdir -p "$bundle/Contents/MacOS"
xcrun clang++ -dynamiclib -fobjc-arc -fblocks -O2 -arch x86_64 -arch arm64 -mmacosx-version-min=11.0 \
  -framework AppKit "$project_root/Native/macOS/Trackpad.mm" -o "$bundle/Contents/MacOS/CellblockGestures"
cat > "$bundle/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>CellblockGestures</string>
<key>CFBundleIdentifier</key><string>com.cellblockholdings.gestures</string>
<key>CFBundlePackageType</key><string>BNDL</string>
<key>CFBundleVersion</key><string>1</string>
</dict></plist>
PLIST
xattr -cr "$bundle"
codesign --force --sign - "$bundle"
file "$bundle/Contents/MacOS/CellblockGestures"
