#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet_bin="${CELLBLOCK_DOTNET:-$(command -v dotnet || true)}"
if [[ ! -x "$dotnet_bin" ]]; then
  echo "Install the .NET 8 SDK or set CELLBLOCK_DOTNET to its dotnet executable." >&2
  exit 1
fi
exec "$dotnet_bin" run --project "$project_root/Tools/CoreChecks/CoreChecks.csproj" --configuration Release
