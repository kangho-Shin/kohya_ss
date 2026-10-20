"""Install the pinned Linux SDK from Microsoft, verifying its published SHA-512."""
import hashlib
import json
from pathlib import Path
import tarfile
import urllib.request

project = Path(__file__).resolve().parent.parent
version = json.loads((project / 'global.json').read_text())['sdk']['version']
root = Path('/workspace/.dotnet')
if (root / 'sdk' / version / 'dotnet.dll').is_file():
    print(f'.NET SDK {version} already installed')
    raise SystemExit(0)
metadata = 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
with urllib.request.urlopen(metadata, timeout=60) as response:
    releases = json.load(response)['releases']
sdk = next(s for release in releases for s in release.get('sdks', [release['sdk']]) if s['version'] == version)
asset = next(f for f in sdk['files'] if f['rid'] == 'linux-x64' and f['name'].endswith('.tar.gz'))
archive = Path('/tmp/office-power-dotnet.tar.gz')
with urllib.request.urlopen(asset['url'], timeout=90) as response, archive.open('wb') as file:
    while chunk := response.read(1024 * 1024):
        file.write(chunk)
with archive.open('rb') as file:
    if hashlib.file_digest(file, 'sha512').hexdigest().lower() != asset['hash'].lower():
        raise RuntimeError('Official SDK SHA-512 mismatch; refusing installation')
root.mkdir(parents=True, exist_ok=True)
with tarfile.open(archive) as file:
    file.extractall(root, filter='data')
print(f'.NET SDK {version}: SHA-512 verified and installed')
