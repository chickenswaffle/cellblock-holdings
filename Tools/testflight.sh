#!/usr/bin/env bash
# Builds Cellblock for iPhone/iPad and uploads it to TestFlight, using the Virobit LLC team.
# Run only from Claude's own clone (never a checkout another editor has open):
#   CELLBLOCK_BUNDLE_ID=<id> CELLBLOCK_BUILD_NUMBER=<n> bash Tools/testflight.sh [--no-upload]
# The App Store Connect record for the bundle ID must exist first. Each upload needs a new build number.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
team="${CELLBLOCK_TEAM:-FX4R73M22M}"
: "${CELLBLOCK_BUNDLE_ID:?Set CELLBLOCK_BUNDLE_ID to the App Store Connect bundle identifier.}"
: "${CELLBLOCK_BUILD_NUMBER:?Set CELLBLOCK_BUILD_NUMBER to a number higher than the last upload.}"
export CELLBLOCK_BUNDLE_ID CELLBLOCK_BUILD_NUMBER
unity="$(bash "$root/Tools/open-unity-mac.sh" --print-editor)"
out="$root/Builds/iOS"; mkdir -p "$out"
echo "Unity: exporting the Xcode project (build $CELLBLOCK_BUILD_NUMBER, $CELLBLOCK_BUNDLE_ID)"
"$unity" -batchmode -nographics -quit -projectPath "$root" -buildTarget iOS \
  -executeMethod Cellblock.Editor.IosBuild.Device -logFile "$out/unity-device.log"
grep -q CELLBLOCK_IOS_BUILD_PASSED "$out/unity-device.log" || { echo "Unity build failed; see $out/unity-device.log" >&2; exit 1; }
# No custom encryption: answer export compliance in the build so every upload skips the question.
/usr/libexec/PlistBuddy -c "Delete :ITSAppUsesNonExemptEncryption" "$out/Device/Info.plist" 2>/dev/null || true
/usr/libexec/PlistBuddy -c "Add :ITSAppUsesNonExemptEncryption bool false" "$out/Device/Info.plist"
rm -rf "$out/Cellblock.xcarchive" "$out/export"
echo "Xcode: archiving"
xcodebuild archive -project "$out/Device/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
  -destination 'generic/platform=iOS' -archivePath "$out/Cellblock.xcarchive" -allowProvisioningUpdates \
  DEVELOPMENT_TEAM="$team" CODE_SIGN_STYLE=Automatic > "$out/archive.log" 2>&1 || { tail -30 "$out/archive.log" >&2; exit 1; }
destination=upload; [[ "${1:-}" == --no-upload ]] && destination=export
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
# Unity rewrites shared settings while importing; keep this clone's tracked files as committed.
git -C "$root" checkout -- ProjectSettings Assets/DefaultVolumeProfile.asset Assets/UniversalRenderPipelineGlobalSettings.asset 2>/dev/null || true
echo "Done. TestFlight processing usually takes 5-15 minutes before the build appears."
