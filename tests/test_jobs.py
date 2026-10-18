import threading

import pytest

from faceswap_app.jobs import JobRunner, ResultState


def event(runner):
    return runner.events.get(timeout=3)


def test_success_and_failure_then_retry():
    runner = JobRunner()
    runner.submit(lambda: 42)
    assert event(runner) == ('success', 42)
    def fail():
        raise ValueError('사진 오류')
    runner.submit(fail)
    status, message = event(runner)
    assert status == 'error' and '사진 오류' in message
    runner.submit(lambda: 7)
    assert event(runner) == ('success', 7)


def test_duplicate_job_rejected():
    runner = JobRunner()
    release = threading.Event()
    runner.submit(lambda: release.wait(timeout=3))
    try:
        with pytest.raises(RuntimeError):
            runner.submit(lambda: None)
    finally:
        release.set()
    assert event(runner)[0] == 'success'


def test_input_change_and_failure_clear_old_result():
    state = ResultState()
    state.result = object()
    state.invalidate()
    assert state.result is None
    assert state.generation == 1


def test_busy_stays_true_until_event_consumed():
    runner = JobRunner()
    runner.submit(lambda: 1)
    with runner.lock:
        assert runner.busy
    assert event(runner) == ('success', 1)
    assert not runner.busy
