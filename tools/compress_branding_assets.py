from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
paths = [
    root / "src/Maen.Accounting.App/Resources/AppIcon/appicon.png",
    root / "src/Maen.Accounting.App/Resources/Images/maen_logo.png",
    root / "src/Maen.Accounting.App/Resources/Splash/splash.png",
]

for path in paths:
    with Image.open(path) as source:
        image = source.convert("RGB")
        if image.size != (1024, 1024):
            image = image.resize((1024, 1024), Image.Resampling.LANCZOS)
        image.save(path, format="PNG", optimize=True, compress_level=9)
        print(f"{path}: {image.size[0]}x{image.size[1]}")
