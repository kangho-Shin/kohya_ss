"""Install the pinned Linux SDK from Microsoft, verifying its published SHA-512."""
import hashlib
import json
from pathlib import Path
import tarfile
import shutil
import tempfile
import uuid
import urllib.request

project = Path(__file__).resolve().parent.parent
version = json.loads((project / 'global.json').read_text())['sdk']['version']
root = Path('/workspace/.dotnet')
if (root / 'sdk' / version / 'dotnet.dll').is_file():
    print(f'.NET SDK {version} already installed')
    raise SystemExit(0)
channel = '.'.join(version.split('.')[:2])
metadata = f'https://builds.dotnet.microsoft.com/dotnet/release-metadata/{channel}/releases.json'
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
with tempfile.TemporaryDirectory(dir='/workspace', prefix='.office-sdk-stage-') as stage_name:
    stage = Path(stage_name)
    with tarfile.open(archive) as file:
        file.extractall(stage, filter='data')
    for source in stage.rglob('*'):
        destination = root / source.relative_to(stage)
        if source.is_dir():
            destination.mkdir(parents=True, exist_ok=True)
        elif source.is_file():
            destination.parent.mkdir(parents=True, exist_ok=True)
            temporary = destination.with_name(destination.name + '.' + uuid.uuid4().hex + '.tmp')
            shutil.copy2(source, temporary)
            temporary.replace(destination)
print(f'.NET SDK {version}: SHA-512 verified and installed')
