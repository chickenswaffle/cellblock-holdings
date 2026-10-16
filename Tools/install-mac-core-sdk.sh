#!/usr/bin/env bash
set -euo pipefail
if [[ "$(uname -s)" != Darwin ]]; then
  echo "This helper installs the verified macOS .NET 8 SDK for local core checks." >&2
  exit 1
fi
project_root="$(cd "$(dirname "$0")/.." && pwd)"
tools_root="${CELLBLOCK_TOOLS_ROOT:-$project_root/.local-tools}"
sdk_path="$tools_root/dotnet/dotnet"
if [[ ! -x "$sdk_path" ]]; then
  python3 - "$tools_root" "$(uname -m)" <<'PY'
import hashlib, json, os, subprocess, sys, tarfile, tempfile
from pathlib import Path, PurePosixPath

version = '8.0.426'
root = Path(sys.argv[1])
architecture = sys.argv[2]
rid = {'x86_64': 'osx-x64', 'arm64': 'osx-arm64'}.get(architecture)
if not rid:
    raise SystemExit('Unsupported Mac architecture: ' + architecture)
root.mkdir(parents=True, exist_ok=True)
with tempfile.TemporaryDirectory(prefix='cellblock-sdk-') as temporary:
    directory = Path(temporary)
    metadata = directory / 'releases.json'
    subprocess.run(['curl', '--fail', '--location', '--retry', '2', '--max-time', '120',
                    'https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json',
                    '--output', str(metadata)], check=True)
    sdk = next(sdk for release in json.loads(metadata.read_text())['releases']
               for sdk in release.get('sdks', []) if sdk['version'] == version)
    artifact = next(file for file in sdk['files'] if file['rid'] == rid and file['name'].endswith('.tar.gz'))
    expected_prefix = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/' + version + '/'
    if not artifact['url'].startswith(expected_prefix):
        raise SystemExit('Unexpected SDK distribution URL')
    archive = directory / 'sdk.tar.gz'
    subprocess.run(['curl', '--fail', '--location', '--retry', '2', '--max-time', '240',
                    artifact['url'], '--output', str(archive)], check=True)
    digest = hashlib.sha512()
    with archive.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    if digest.hexdigest().lower() != artifact['hash'].lower():
        raise SystemExit('SDK SHA-512 verification failed')
    destination = root / 'dotnet'
    destination.mkdir(parents=True, exist_ok=True)
    with tarfile.open(archive) as package:
        members = package.getmembers()
        for member in members:
            relative = PurePosixPath(member.name)
            if relative.is_absolute() or '..' in relative.parts:
                raise SystemExit('Unsafe SDK archive path')
            if member.issym() or member.islnk():
                target = member.linkname if member.islnk() else os.path.join(str(relative.parent), member.linkname)
                normalized = os.path.normpath(target)
                if os.path.isabs(target) or normalized == '..' or normalized.startswith('../'):
                    raise SystemExit('Unsafe SDK archive link')
            elif not (member.isfile() or member.isdir()):
                raise SystemExit('Unexpected special file in SDK archive')
        package.extractall(destination, members=members)
    print('Official .NET SDK SHA-512 verified: ' + artifact['hash'])
PY
fi
export DOTNET_CLI_HOME="$tools_root/dotnet-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
"$sdk_path" --version
