from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
source_path = root / "src/Maen.Accounting.App/Resources/AppIcon/appicon.png"
windows_dir = root / "src/Maen.Accounting.App/Platforms/Windows"
windows_dir.mkdir(parents=True, exist_ok=True)

with Image.open(source_path) as source:
    image = source.convert("RGBA")
    image.save(windows_dir / "appicon.png", format="PNG", optimize=True, compress_level=9)
    image.save(
        windows_dir / "appicon.ico",
        format="ICO",
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
    )
    print(f"Created {windows_dir / 'appicon.png'}")
    print(f"Created {windows_dir / 'appicon.ico'}")
