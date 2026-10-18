import hashlib
import io
import zipfile

import pytest

from faceswap_app.models import download_file, extract_buffalo, require_models


def test_missing_models_report_paths(tmp_path):
    with pytest.raises(FileNotFoundError, match='inswapper_128.onnx'):
        require_models(tmp_path)


def test_download_checks_hash_and_preserves_existing_on_failure(tmp_path):
    path = tmp_path / 'model.onnx'
    path.write_bytes(b'old')
    def opener(*args, **kwargs):
        return io.BytesIO(b'new')
    with pytest.raises(ValueError, match='SHA'):
        download_file('https://example.com/model', path, '0' * 64, opener=opener)
    assert path.read_bytes() == b'old'
    assert not list(tmp_path.glob('*.part'))


def test_successful_download(tmp_path):
    path = tmp_path / 'model.onnx'
    download_file('https://example.com/model', path, hashlib.sha256(b'new').hexdigest(),
                  opener=lambda *a, **k: io.BytesIO(b'new'))
    assert path.read_bytes() == b'new'


def test_interrupted_download_leaves_no_partial(tmp_path):
    class Broken(io.BytesIO):
        def read(self, size=-1):
            raise OSError('interrupted')
    with pytest.raises(OSError):
        download_file('https://example.com/model', tmp_path / 'm', '0' * 64,
                      opener=lambda *a, **k: Broken())
    assert list(tmp_path.iterdir()) == []


def test_archive_traversal_is_rejected(tmp_path):
    archive = tmp_path / 'bad.zip'
    with zipfile.ZipFile(archive, 'w') as z:
        z.writestr('../outside.onnx', b'evil')
    with pytest.raises(ValueError):
        extract_buffalo(archive, tmp_path / 'buffalo_l')
    assert not (tmp_path / 'outside.onnx').exists()
