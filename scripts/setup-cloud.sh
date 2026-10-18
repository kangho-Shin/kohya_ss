#!/usr/bin/env bash
# Debian 13 cloud development setup; Windows users run install.bat instead.
set -euo pipefail
cd "$(dirname "$0")/.."
cloud_python=/workspace/.venvs/faceswap/bin/python
if [ ! -x "$cloud_python" ]; then
  python -m venv /workspace/.venvs/faceswap
fi
"$cloud_python" -m pip install --disable-pip-version-check -r requirements-dev.txt
"$cloud_python" -m faceswap_app.models --accept-model-license

portable_xvfb=/workspace/.tools/faceswap-xvfb
if ! command -v Xvfb >/dev/null && [ ! -x "$portable_xvfb/usr/bin/Xvfb" ]; then
  apt_dir=$(mktemp -d /tmp/faceswap-apt.XXXXXX)
  mkdir -p "$apt_dir/lists/partial" "$apt_dir/cache/archives/partial" "$apt_dir/packages"
  cat > "$apt_dir/sources.list" <<'SOURCES'
deb [signed-by=/usr/share/keyrings/debian-archive-keyring.gpg] https://deb.debian.org/debian trixie main
SOURCES
  cat > "$apt_dir/apt.conf" <<CONFIG
Dir::Etc::main "/dev/null";
Dir::Etc::parts "-";
Dir::Etc::sourcelist "$apt_dir/sources.list";
Dir::Etc::sourceparts "-";
Dir::State::lists "$apt_dir/lists";
Dir::Cache "$apt_dir/cache";
Debug::NoLocking "true";
CONFIG
  APT_CONFIG="$apt_dir/apt.conf" /usr/bin/apt-get update
  (
    cd "$apt_dir/packages"
    APT_CONFIG="$apt_dir/apt.conf" /usr/bin/apt-get download xvfb
    mkdir -p "$portable_xvfb"
    dpkg-deb -x ./xvfb_*.deb "$portable_xvfb"
  )
fi
"$cloud_python" -m scripts.doctor --load
"$cloud_python" -m pytest -q
