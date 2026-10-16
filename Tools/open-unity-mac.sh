#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
if [[ "$(uname -s)" != Darwin ]]; then
  echo "Run this launcher on the Mac that has Unity installed. This shell is not macOS." >&2
  exit 1
fi
editor_path="${UNITY_EDITOR:-}"
if [[ -z "$editor_path" ]]; then
  editor_list="$(mktemp /tmp/cellblock-editors.XXXXXX)"
  trap 'rm -f "$editor_list"' EXIT
  for candidate in /Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity; do
    if [[ -x "$candidate" ]]; then
      version_dir="$(dirname "$(dirname "$(dirname "$(dirname "$candidate")")")")"
      printf '%s\t%s\n' "$(basename "$version_dir")" "$candidate" >> "$editor_list"
    fi
  done
  if [[ -s "$editor_list" ]]; then
    editor_path="$(sort -t . -k1,1n -k2,2n -k3,3n "$editor_list" | tail -n 1 | cut -f2-)"
  fi
fi
if [[ ! -x "$editor_path" ]]; then
  echo "Unity 6 was not found in Unity Hub's default location." >&2
  echo 'Set UNITY_EDITOR to the full installed editor executable path, then rerun.' >&2
  exit 1
fi
bundle_path="$(dirname "$(dirname "$(dirname "$editor_path")")")"
editor_version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$bundle_path/Contents/Info.plist" 2>/dev/null || true)"
if [[ "$editor_version" != 6000.* ]]; then
  editor_version="$(basename "$(dirname "$bundle_path")")"
fi
if [[ "$editor_version" != 6000.* ]]; then
  echo "This project requires Unity 6 (6000.x). Select that editor using UNITY_EDITOR." >&2
  exit 1
fi
if [[ "${1:-}" == --print-editor ]]; then
  printf '%s\n' "$editor_path"
  exit 0
fi
mkdir -p "$project_root/ProjectSettings"
if [[ ! -f "$project_root/ProjectSettings/ProjectVersion.txt" ]]; then
  printf 'm_EditorVersion: %s\n' "$editor_version" > "$project_root/ProjectSettings/ProjectVersion.txt"
fi
if [[ "${1:-}" == --batch-check ]]; then
  exec "$editor_path" -batchmode -nographics -quit -projectPath "$project_root" -executeMethod Cellblock.Editor.ProjectSetup.Validate -logFile -
fi
exec "$editor_path" -projectPath "$project_root"
