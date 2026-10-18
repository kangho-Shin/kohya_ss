from pathlib import Path
from queue import Empty
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

from PIL import Image, ImageDraw, ImageTk

from .engine import FaceEngine
from .images import load_image, save_png
from .jobs import JobRunner, ResultState
from .models import DEFAULT_MODELS, prepare


class Application:
    def __init__(self, root):
        self.root = root
        root.title('내 사진 얼굴 교체')
        root.geometry('1120x790')
        root.minsize(900, 650)
        self.runner = JobRunner()
        self.state = ResultState()
        self.engine = None
        self.engine_device = None
        self.paths = {}
        self.images = {}
        self.faces = {}
        self.previews = {}
        self.raw_result = None
        self.photo_refs = {}
        self.buttons = []
        self.combos = {}
        self.status = tk.StringVar(value='1. 모델 준비 → 2. 사진 선택 → 3. 얼굴 탐지 및 선택 → 4. 교체')
        self.device = tk.StringVar(value='cuda')
        toolbar = ttk.Frame(root, padding=12)
        toolbar.pack(fill='x')
        self.button(toolbar, '처음 한 번: 모델 준비', self.prepare_models).pack(side='left')
        ttk.Label(toolbar, text='  실행 장치: ').pack(side='left')
        self.device_combo = ttk.Combobox(toolbar, textvariable=self.device,
                                         values=('cuda', 'cpu'), state='readonly', width=8)
        self.device_combo.pack(side='left')
        self.device_combo.bind('<<ComboboxSelected>>', self.device_changed)
        ttk.Label(toolbar, text='cuda = RTX 3080 / cpu = 느린 호환 모드').pack(side='left', padx=8)
        panels = ttk.Frame(root, padding=(12, 0))
        panels.pack(fill='both', expand=True)
        for col, (key, title) in enumerate((('source', '1. 가져올 얼굴 사진'), ('target', '2. 얼굴을 바꿀 대상 사진'))):
            frame = ttk.LabelFrame(panels, text=title, padding=8)
            frame.grid(row=0, column=col, sticky='nsew', padx=4)
            panels.columnconfigure(col, weight=1)
            label = ttk.Label(frame, text='사진을 선택하세요', anchor='center')
            label.pack(fill='both', expand=True)
            self.previews[key] = label
            self.button(frame, '사진 선택', lambda k=key: self.choose(k)).pack(fill='x', pady=5)
            combo = ttk.Combobox(frame, state='disabled')
            combo.pack(fill='x')
            combo.bind('<<ComboboxSelected>>', lambda e: self.clear_result())
            self.combos[key] = combo
        panels.rowconfigure(0, weight=1)
        actions = ttk.Frame(root, padding=10)
        actions.pack(fill='x')
        self.swap_button = self.button(actions, '3. 선택한 얼굴 교체', self.swap)
        self.swap_button.pack(side='left', padx=4)
        self.refine_button = self.button(actions, '선택: SD 경계 보정', self.refine)
        self.refine_button.pack(side='left', padx=4)
        self.save_button = self.button(actions, '현재 결과 저장 (PNG)', self.save)
        self.save_button.pack(side='left', padx=4)
        self.raw_button = self.button(actions, '보정 전 교체 결과 저장', lambda: self.save(raw=True))
        self.raw_button.pack(side='left', padx=4)
        self.previews['result'] = ttk.Label(root, text='결과 미리보기', anchor='center')
        self.previews['result'].pack(fill='both', expand=True, padx=12)
        self.progress = ttk.Progressbar(root, mode='indeterminate')
        self.progress.pack(fill='x', padx=12, pady=(6, 0))
        ttk.Label(root, textvariable=self.status, wraplength=1050, padding=12).pack(fill='x')
        root.protocol('WM_DELETE_WINDOW', self.close)
        root.after(100, self.poll)
        self.update_controls()

    def button(self, frame, text, command):
        button = ttk.Button(frame, text=text, command=command)
        self.buttons.append(button)
        return button

    def clear_result(self):
        self.state.invalidate()
        self.raw_result = None
        self.photo_refs.pop('result', None)
        self.previews['result'].configure(image='', text='결과 미리보기')
        self.update_controls()

    def device_changed(self, event=None):
        self.clear_result()
        self.status.set('실행 장치를 변경했습니다. 다음 처리부터 적용됩니다.')

    def get_engine(self, device):
        if self.engine is None or self.engine_device != device:
            if self.engine is not None:
                self.engine.close()
            self.engine = None
            self.engine = FaceEngine(DEFAULT_MODELS, device)
            self.engine_device = device
        return self.engine

    def submit(self, work, message):
        self.runner.submit(work)
        self.status.set(message)
        self.progress.start(12)
        self.update_controls()

    def prepare_models(self):
        if not messagebox.askyesno('공식 모델 다운로드',
            'InsightFace 공식 모델 약 850 MB를 내려받습니다.\n'
            '모델 이용 조건은 비상업 연구용입니다.\n'
            'https://github.com/deepinsight/insightface\n\n이용 조건을 확인했으며 다운로드할까요?'):
            return
        def work():
            prepare(DEFAULT_MODELS)
            return ('models',)
        self.submit(work, '모델 다운로드 및 SHA-256 확인 중… 처음 한 번은 시간이 걸립니다.')

    def choose(self, side):
        filename = filedialog.askopenfilename(filetypes=[('사진', '*.png *.jpg *.jpeg *.webp *.bmp'), ('모든 파일', '*.*')])
        if not filename:
            return
        self.clear_result()
        self.paths.pop(side, None)
        self.images.pop(side, None)
        self.faces.pop(side, None)
        self.combos[side].set('')
        self.previews[side].configure(image='', text='읽는 중…')
        device = self.device.get()
        def work():
            image = load_image(Path(filename))
            engine = self.get_engine(device)
            faces = engine.detect(image)
            if not faces:
                raise ValueError('얼굴을 찾지 못했습니다. 선명한 정면 사진을 선택하세요.')
            return ('detected', side, Path(filename), image, faces, engine.provider)
        self.submit(work, '사진 읽기와 얼굴 탐지 중…')

    def show_image(self, key, data, faces=()):
        image = Image.fromarray(data[:, :, ::-1])
        original_width = image.width
        image.thumbnail((490, 245) if key != 'result' else (1000, 240))
        if faces:
            draw = ImageDraw.Draw(image)
            scale = image.width / original_width
            for index, face in enumerate(faces):
                box = [int(x * scale) for x in face.bbox]
                draw.rectangle(box, outline='#ff4b4b', width=3)
                draw.text((max(0, box[0]), max(0, box[1]-13)), str(index+1), fill='red', stroke_width=1)
        photo = ImageTk.PhotoImage(image)
        self.photo_refs[key] = photo
        self.previews[key].configure(image=photo, text='')

    def swap(self):
        if not all(side in self.images for side in ('source', 'target')):
            return
        indices = (self.combos['source'].current(), self.combos['target'].current())
        device = self.device.get()
        self.clear_result()
        def work():
            result = self.get_engine(device).swap(self.images['source'], self.images['target'], *indices)
            return ('swapped', result)
        self.submit(work, '얼굴 교체 중…')

    def refine(self):
        if self.raw_result is None:
            return
        # Preserve the unrefined result even if optional refinement fails.
        self.state.result = self.raw_result.image
        self.show_image('result', self.state.result)
        raw = self.raw_result
        def work():
            from .refine import refine
            if self.engine is not None:
                self.engine.close()
                self.engine = None
            image = refine(raw.image, raw.bbox, DEFAULT_MODELS / 'sd-inpainting')
            return ('refined', image)
        self.submit(work, 'SD 경계 보정 중… 보정 전 결과는 유지됩니다.')

    def save(self, raw=False):
        image = self.raw_result.image if raw and self.raw_result else self.state.result
        if image is None:
            return
        filename = filedialog.asksaveasfilename(defaultextension='.png', filetypes=[('PNG', '*.png')], initialfile='faceswap-result.png')
        if filename:
            try:
                path = save_png(image, Path(filename), tuple(self.paths.values()))
                self.status.set(f'저장 완료: {path}')
            except Exception as exc:
                messagebox.showerror('저장 실패', str(exc))

    def update_controls(self):
        busy = self.runner.busy
        for button in self.buttons:
            button.configure(state='disabled' if busy else 'normal')
        self.device_combo.configure(state='disabled' if busy else 'readonly')
        for side, combo in self.combos.items():
            combo.configure(state='readonly' if not busy and side in self.faces else 'disabled')
        self.swap_button.configure(state='normal' if not busy and len(self.images) == 2 else 'disabled')
        self.refine_button.configure(state='normal' if not busy and self.raw_result is not None else 'disabled')
        self.raw_button.configure(state='normal' if not busy and self.raw_result is not None else 'disabled')
        self.save_button.configure(state='normal' if not busy and self.state.result is not None else 'disabled')

    def poll(self):
        try:
            status, payload = self.runner.events.get_nowait()
        except Empty:
            self.root.after(100, self.poll)
            return
        self.progress.stop()
        if status == 'error':
            self.status.set('처리 실패: ' + payload)
            messagebox.showerror('처리 실패', payload)
        elif payload[0] == 'models':
            self.status.set('모델 준비 완료. 사진 두 장을 선택하세요.')
        elif payload[0] == 'detected':
            _, side, path, image, faces, provider = payload
            self.paths[side], self.images[side], self.faces[side] = path, image, faces
            self.combos[side].configure(values=[f'얼굴 {i+1} (왼쪽부터)' for i in range(len(faces))])
            self.combos[side].current(0)
            self.show_image(side, image, faces)
            self.status.set(f'얼굴 {len(faces)}개 탐지. 실제 실행 장치: {provider}')
        elif payload[0] == 'swapped':
            self.raw_result = payload[1]
            self.state.result = self.raw_result.image
            self.show_image('result', self.state.result)
            self.status.set(f'교체 완료 · {self.raw_result.provider} · 결과를 확인하고 저장하세요.')
        elif payload[0] == 'refined':
            self.state.result = payload[1]
            self.show_image('result', self.state.result)
            self.status.set('SD 보정 완료. 보정 전 교체 결과도 따로 저장할 수 있습니다.')
        self.update_controls()
        self.root.after(100, self.poll)

    def close(self):
        if self.runner.busy:
            messagebox.showinfo('처리 중', '작업이 끝난 뒤 창을 닫아 주세요.')
            return
        if self.engine is not None:
            self.engine.close()
        self.root.destroy()


def main():
    root = tk.Tk()
    Application(root)
    root.mainloop()
