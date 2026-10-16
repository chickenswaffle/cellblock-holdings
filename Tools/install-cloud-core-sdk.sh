#!/usr/bin/env bash
set -euo pipefail
if [[ "$(uname -s)" != Linux ]]; then
  echo "This helper prepares the Linux cloud SDK. Use a local .NET 8 SDK for optional Mac core checks." >&2
  exit 1
fi
tools_root="${CELLBLOCK_TOOLS_ROOT:-/workspace/.cellblock-tools}"
if [[ ! -x "$tools_root/dotnet/dotnet" ]]; then
  python3 - "$tools_root" <<'PY'
import hashlib, json, platform, subprocess, sys, tarfile, tempfile
from pathlib import Path
version = '8.0.426'
root = Path(sys.argv[1])
root.mkdir(parents=True, exist_ok=True)
rid = {'x86_64': 'linux-x64', 'aarch64': 'linux-arm64'}.get(platform.machine())
if not rid:
    raise SystemExit('Unsupported cloud architecture')
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
        package.extractall(destination, filter='data')
    print('Official SDK SHA-512 verified.')
PY
fi
export DOTNET_CLI_HOME="$tools_root/dotnet-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
"$tools_root/dotnet/dotnet" --version
