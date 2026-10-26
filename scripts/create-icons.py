"""Воспроизводимое построение собственных ICO; Pillow нужен только для изменения иконок."""
from pathlib import Path
from PIL import Image, ImageDraw
import struct
import io

ROOT = Path(__file__).resolve().parents[1] / 'assets'
SIZES = (16, 24, 32, 48, 64, 128, 256)

def draw_icon(size, document):
    scale = 4
    image = Image.new('RGBA', (size * scale, size * scale))
    d = ImageDraw.Draw(image)
    unit = size * scale / 64
    def box(coords): return tuple(round(c * unit) for c in coords)
    navy, blue, pale = '#274A7A', '#3979B5', '#E8EFF7'
    if document:
        d.rounded_rectangle(box((12, 3, 51, 61)), radius=round(5*unit), fill=navy)
        d.rounded_rectangle(box((15, 6, 48, 58)), radius=round(3*unit), fill=pale)
        d.polygon([tuple(box(p)) for p in ((37, 6), (48, 17), (37, 17))], fill=blue)
        for y in (22, 28): d.rounded_rectangle(box((21, y, 40, y+2)), radius=max(1, round(unit)), fill='#95AAC1')
        x, y, width = 34, 44, 25
    else:
        d.rounded_rectangle(box((2, 2, 62, 62)), radius=round(14*unit), fill=navy)
        x, y, width = 32, 35, 32
    left, right = x-width/2, x+width/2
    d.arc(box((left+5, y-20, right-5, y+3)), 180, 360, fill=pale if not document else navy, width=max(2, round(4*unit)))
    d.rounded_rectangle(box((left, y-7, right, y+16)), radius=round(4*unit), fill=pale if not document else blue)
    d.ellipse(box((x-2.5, y, x+2.5, y+5)), fill=navy if not document else 'white')
    d.rectangle(box((x-1, y+3, x+1, y+9)), fill=navy if not document else 'white')
    return image.resize((size, size), Image.Resampling.LANCZOS)

def write_ico(path, document):
    images = []
    for size in SIZES:
        stream = io.BytesIO()
        draw_icon(size, document).save(stream, format='PNG')
        images.append(stream.getvalue())
    offset = 6 + 16*len(SIZES)
    header = bytearray(struct.pack('<HHH', 0, 1, len(SIZES)))
    for size, data in zip(SIZES, images):
        header.extend(struct.pack('<BBBBHHII', size % 256, size % 256, 0, 0, 1, 32, len(data), offset))
        offset += len(data)
    path.write_bytes(header + b''.join(images))

ROOT.mkdir(exist_ok=True)
write_ico(ROOT / 'pocketvault.ico', False)
write_ico(ROOT / 'pocketvault-file.ico', True)
