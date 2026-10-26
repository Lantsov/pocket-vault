"""Generate documentation artwork from the existing project icon; requires Pillow."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "docs" / "images"


def generate():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    icon = Image.open(ROOT / "assets" / "pocketvault.ico").convert("RGBA")
    # Preserve the original mark's geometry in a neutral palette.
    gray = icon.convert("L")
    logo = Image.merge("RGBA", (gray, gray, gray, icon.getchannel("A")))
    logo.save(OUTPUT / "pocketvault-logo.png", optimize=True)

    canvas = Image.new("RGB", (1280, 640), "#141414")
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle((32, 32, 1247, 607), radius=22, outline="#333333", width=2)
    canvas.paste(logo.resize((104, 104), Image.Resampling.LANCZOS), (100, 132), logo.resize((104, 104), Image.Resampling.LANCZOS))
    fonts = Path("C:/Windows/Fonts")
    title = ImageFont.truetype(str(fonts / "seguisb.ttf"), 84)
    subtitle = ImageFont.truetype(str(fonts / "segoeui.ttf"), 32)
    small = ImageFont.truetype(str(fonts / "segoeui.ttf"), 24)
    draw.text((96, 255), "PocketVault", font=title, fill="#F5F5F5")
    draw.text((100, 375), "Your files. Your password. Your control.", font=subtitle, fill="#B3B3B3")
    draw.line((100, 480, 1180, 480), fill="#333333", width=2)
    draw.text((100, 515), "OFFLINE FILE ENCRYPTION  /  WINDOWS", font=small, fill="#888888")
    canvas.save(OUTPUT / "social-preview.png", optimize=True)


if __name__ == "__main__":
    generate()
