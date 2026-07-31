from pathlib import Path
import json
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
errors: list[str] = []

required = [
    root / 'src/Maen.Accounting.App/Maen.Accounting.App.csproj',
    root / 'src/Maen.Accounting.Core/Services/SyncMergeEngine.cs',
    root / 'src/Maen.Accounting.App/Services/FirestoreSyncService.cs',
    root / 'tests/Maen.Accounting.Core.Tests/SyncMergeEngineTests.cs',
    root / 'firestore.rules',
    root / '.github/workflows/build.yml',
]
for path in required:
    if not path.exists():
        errors.append(f'missing: {path.relative_to(root)}')

text_files = [
    path for path in root.rglob('*')
    if path.is_file() and path.suffix in {'.cs', '.csproj', '.xaml', '.rules', '.md', '.yml'}
]
all_text = '\n'.join(path.read_text(encoding='utf-8') for path in text_files)

checks = {
    'Flutter source must not remain in the converted project': not list(root.rglob('*.dart')),
    'Flutter package manifest must not remain': not list(root.rglob('pubspec.yaml')),
    'Money must be stored as integer minor units': 'SalesMinor' in all_text and 'CostMinor' in all_text,
    'Per-user database filename must exist': 'DatabaseFileName(userId)' in all_text,
    'Cloud rules must use Firebase UID': 'request.auth.uid == userId' in all_text,
    'Sync must merge individual records': 'SyncMergeEngine.BuildPlan' in all_text,
    'Legacy Flutter backup import must exist': 'LegacyBackupParser.Parse' in all_text,
    '.NET 10 popup APIs must be used': 'DisplayAlert(' not in all_text,
    'No font files may be bundled': not any(root.rglob('*.ttf')) and not any(root.rglob('*.otf')),
}
for message, passed in checks.items():
    if not passed:
        errors.append(message)

for path in list(root.rglob('*.xaml')) + list(root.rglob('*.csproj')) + list(root.rglob('*.xml')) + list(root.rglob('*.appxmanifest')):
    try:
        ET.parse(path)
    except Exception as exception:
        errors.append(f'invalid XML {path.relative_to(root)}: {exception}')

for path in root.rglob('*.json'):
    try:
        json.loads(path.read_text(encoding='utf-8'))
    except Exception as exception:
        errors.append(f'invalid JSON {path.relative_to(root)}: {exception}')

if errors:
    print('CONVERSION_VALIDATION_FAILED')
    for error in errors:
        print(f'- {error}')
    sys.exit(1)

print('CONVERSION_VALIDATION_OK')
