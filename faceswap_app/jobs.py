from dataclasses import dataclass
from queue import Queue
from threading import RLock, Thread


@dataclass
class ResultState:
    result: object = None
    generation: int = 0

    def invalidate(self):
        self.result = None
        self.generation += 1


class JobRunner:
    def __init__(self):
        self.events = Queue()
        self.lock = RLock()
        self._working = False

    @property
    def busy(self):
        with self.lock:
            return self._working or not self.events.empty()

    def submit(self, work):
        with self.lock:
            if self.busy:
                raise RuntimeError('이미 처리 중입니다.')
            self._working = True
        def run():
            try:
                result = ('success', work())
            except Exception as exc:
                result = ('error', str(exc) or type(exc).__name__)
            with self.lock:
                self._working = False
                self.events.put(result)
        Thread(target=run, daemon=True).start()
