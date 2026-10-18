#!/usr/bin/env bash
# Builds Cellblock for iPhone/iPad and uploads it to TestFlight, using the Virobit LLC team.
# Run only from Claude's own clone (never a checkout another editor has open):
#   bash Tools/testflight.sh                 build, archive and upload
#   bash Tools/testflight.sh --no-upload     build and archive, export an .ipa locally
#   bash Tools/testflight.sh --upload-only   upload the existing archive again (e.g. once the App Store record exists)
# The bundle ID is com.virobit.cellblock (user decision 2026-10-17); its App Store Connect record must exist before
# an upload. The build number defaults to the commit count, so each new commit uploads as a new build.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
team="${CELLBLOCK_TEAM:-FX4R73M22M}"
export CELLBLOCK_BUNDLE_ID="${CELLBLOCK_BUNDLE_ID:-com.virobit.cellblock}"
export CELLBLOCK_BUILD_NUMBER="${CELLBLOCK_BUILD_NUMBER:-$(git -C "$root" rev-list --count HEAD)}"
unity="$(bash "$root/Tools/open-unity-mac.sh" --print-editor)"
out="$root/Builds/iOS"; mkdir -p "$out"
mode="${1:-}"
if [[ "$mode" != --upload-only ]]; then
echo "Unity: exporting the Xcode project (build $CELLBLOCK_BUILD_NUMBER, $CELLBLOCK_BUNDLE_ID)"
"$unity" -batchmode -nographics -quit -projectPath "$root" -buildTarget iOS \
  -executeMethod Cellblock.Editor.IosBuild.Device -logFile "$out/unity-device.log"
grep -q CELLBLOCK_IOS_BUILD_PASSED "$out/unity-device.log" || { echo "Unity build failed; see $out/unity-device.log" >&2; exit 1; }
# No custom encryption: answer export compliance in the build so every upload skips the question.
/usr/libexec/PlistBuddy -c "Delete :ITSAppUsesNonExemptEncryption" "$out/Device/Info.plist" 2>/dev/null || true
/usr/libexec/PlistBuddy -c "Add :ITSAppUsesNonExemptEncryption bool false" "$out/Device/Info.plist"
# The app icon: one 1024 px opaque image (the guard tower from the Home artwork); Xcode derives every size from it.
icons="$out/Device/Unity-iPhone/Images.xcassets/AppIcon.appiconset"
rm -rf "$icons"; mkdir -p "$icons"; cp "$root/Tools/ios/AppIcon.png" "$icons/AppIcon.png"
cat > "$icons/Contents.json" <<'JSON'
{ "images" : [ { "filename" : "AppIcon.png", "idiom" : "universal", "platform" : "ios", "size" : "1024x1024" } ],
  "info" : { "author" : "xcode", "version" : 1 } }
JSON
rm -rf "$out/Cellblock.xcarchive"
echo "Xcode: archiving"
xcodebuild archive -project "$out/Device/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
  -destination 'generic/platform=iOS' -archivePath "$out/Cellblock.xcarchive" -allowProvisioningUpdates \
  DEVELOPMENT_TEAM="$team" CODE_SIGN_STYLE=Automatic > "$out/archive.log" 2>&1 || { tail -30 "$out/archive.log" >&2; exit 1; }
# Unity rewrites shared settings while importing; keep this clone's tracked files as committed.
git -C "$root" checkout -- ProjectSettings Assets/DefaultVolumeProfile.asset Assets/UniversalRenderPipelineGlobalSettings.asset 2>/dev/null || true
fi
[[ -d "$out/Cellblock.xcarchive" ]] || { echo "No archive at $out/Cellblock.xcarchive; run without --upload-only first." >&2; exit 1; }
rm -rf "$out/export"
destination=upload; [[ "$mode" == --no-upload ]] && destination=export
plist="$out/ExportOptions.plist"
cat > "$plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>method</key><string>app-store-connect</string>
  <key>destination</key><string>$destination</string>
  <key>teamID</key><string>$team</string>
  <key>signingStyle</key><string>automatic</string>
  <key>manageAppVersionAndBuildNumber</key><false/>
</dict></plist>
PLIST
echo "Xcode: exporting ($destination)"
xcodebuild -exportArchive -archivePath "$out/Cellblock.xcarchive" -exportOptionsPlist "$plist" -exportPath "$out/export" \
  -allowProvisioningUpdates > "$out/export.log" 2>&1 || { tail -30 "$out/export.log" >&2; exit 1; }
tail -3 "$out/export.log"
echo "Done: $CELLBLOCK_BUNDLE_ID build $CELLBLOCK_BUILD_NUMBER ($destination). TestFlight processing usually takes 5-15 minutes."
