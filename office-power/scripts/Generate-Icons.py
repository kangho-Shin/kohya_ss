"""Generate original Office Power icons. Requires Pillow; only maintainers run this."""
from pathlib import Path
from PIL import Image, ImageDraw

OUTPUT = Path(__file__).resolve().parent.parent / 'assets'
OUTPUT.mkdir(exist_ok=True)
SIZES = [(n, n) for n in (16, 20, 24, 32, 40, 48, 64, 128, 256)]


def draw_icon(name, background, accent):
    scale = 4
    image = Image.new('RGBA', (256 * scale, 256 * scale))
    draw = ImageDraw.Draw(image)

    def box(coords):
        return tuple(round(v * scale) for v in coords)

    draw.rounded_rectangle(box((8, 8, 248, 248)), radius=52 * scale, fill=background)
    draw.rounded_rectangle(box((37, 49, 219, 179)), radius=16 * scale, fill='white')
    draw.rounded_rectangle(box((48, 60, 208, 163)), radius=8 * scale, fill=accent)
    draw.rounded_rectangle(box((116, 176, 140, 201)), radius=3 * scale, fill='white')
    draw.rounded_rectangle(box((87, 198, 169, 210)), radius=6 * scale, fill='white')
    # Open power ring and vertical stroke remain legible at taskbar sizes.
    draw.arc(box((94, 79, 162, 147)), start=310, end=590, fill='white', width=10 * scale)
    draw.line(box((128, 75, 128, 111)), fill='white', width=10 * scale)
    draw.ellipse(box((123, 70, 133, 80)), fill='white')
    draw.ellipse(box((123, 106, 133, 116)), fill='white')
    image = image.resize((256, 256), Image.Resampling.LANCZOS)
    image.save(OUTPUT / f'{name}.ico', format='ICO', sizes=SIZES)
    return image


powermaster = draw_icon('powermaster', '#1547A0', '#2176E8')
agent = draw_icon('agent', '#086653', '#0C9B78')
preview = Image.new('RGB', (640, 320), '#F0F3F8')
preview.paste(powermaster, (40, 32), powermaster)
preview.paste(agent, (344, 32), agent)
preview.save(OUTPUT / 'icons-preview.png')
