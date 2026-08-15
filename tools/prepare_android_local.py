#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
import shutil
import subprocess
import sys
from pathlib import Path

APP_LABEL = 'Maen Accounting'
GOOGLE_SERVICES_PLUGIN_VERSION = '4.4.2'
PACKAGE_NAME = 'com.example.profit_tracker'


def run(cmd: list[str], cwd: Path) -> None:
    print('$', ' '.join(cmd))
    subprocess.run(cmd, cwd=str(cwd), check=True)


def ensure_android_platform(root: Path) -> None:
    if (root / 'android').exists():
        return
    if shutil.which('flutter') is None:
        raise SystemExit('Flutter غير موجود في PATH، ولا يمكن إنشاء مجلد android تلقائيًا.')
    run(['flutter', 'create', '.', '--platforms=android'], cwd=root)


def copy_google_services(root: Path) -> None:
    src = root / 'firebase' / 'google-services.json'
    if not src.exists():
        raise SystemExit('الملف firebase/google-services.json غير موجود داخل المشروع.')
    app_dir = root / 'android' / 'app'
    app_dir.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(src, app_dir / 'google-services.json')


def patch_settings_kts(path: Path) -> None:
    text = path.read_text(encoding='utf-8')
    needle = 'id("com.google.gms.google-services") version "{}" apply false'.format(
        GOOGLE_SERVICES_PLUGIN_VERSION
    )
    if 'com.google.gms.google-services' not in text:
        text = re.sub(r'(plugins\s*\{)', r'\1\n    ' + needle, text, count=1)
        path.write_text(text, encoding='utf-8')


def patch_settings_groovy(path: Path) -> None:
    text = path.read_text(encoding='utf-8')
    plugin_line = "id 'com.google.gms.google-services' version '{}' apply false".format(
        GOOGLE_SERVICES_PLUGIN_VERSION
    )
    if 'plugins {' in text:
        if 'com.google.gms.google-services' not in text:
            text = re.sub(r'(plugins\s*\{)', r"\1\n    {}".format(plugin_line), text, count=1)
            path.write_text(text, encoding='utf-8')
        return

    build_gradle = path.parent / 'build.gradle'
    if build_gradle.exists():
        patch_root_build_groovy(build_gradle)


def patch_root_build_groovy(path: Path) -> None:
    text = path.read_text(encoding='utf-8')
    classpath_line = "classpath 'com.google.gms:google-services:{}'".format(
        GOOGLE_SERVICES_PLUGIN_VERSION
    )
    if 'com.google.gms:google-services' in text:
        return
    if 'buildscript' in text and 'dependencies' in text:
        text = re.sub(r'(dependencies\s*\{)', r"\1\n        {}".format(classpath_line), text, count=1)
    else:
        text += (
            '\n\nbuildscript {\n'
            '    dependencies {\n'
            '        ' + classpath_line + '\n'
            '    }\n'
            '}\n'
        )
    path.write_text(text, encoding='utf-8')


def patch_app_kts(path: Path) -> None:
    text = path.read_text(encoding='utf-8')
    if 'com.google.gms.google-services' not in text:
        text = re.sub(
            r'(plugins\s*\{)',
            r'\1\n    id("com.google.gms.google-services")',
            text,
            count=1,
        )
    if 'applicationId = "{}"'.format(PACKAGE_NAME) not in text:
        text = re.sub(
            r'applicationId\s*=\s*"[^"]+"',
            'applicationId = "{}"'.format(PACKAGE_NAME),
            text,
            count=1,
        )
    if 'namespace = "{}"'.format(PACKAGE_NAME) not in text:
        text = re.sub(
            r'namespace\s*=\s*"[^"]+"',
            'namespace = "{}"'.format(PACKAGE_NAME),
            text,
            count=1,
        )
    path.write_text(text, encoding='utf-8')


def patch_app_groovy(path: Path) -> None:
    text = path.read_text(encoding='utf-8')
    if 'com.google.gms.google-services' not in text:
        text = re.sub(
            r'(plugins\s*\{)',
            "\\1\n    id 'com.google.gms.google-services'",
            text,
            count=1,
        )
    text = re.sub(
        r'applicationId\s+"[^"]+"',
        'applicationId "{}"'.format(PACKAGE_NAME),
        text,
        count=1,
    )
    if 'namespace ' in text:
        text = re.sub(
            r'namespace\s+"[^"]+"',
            'namespace "{}"'.format(PACKAGE_NAME),
            text,
            count=1,
        )
    path.write_text(text, encoding='utf-8')


def patch_manifest(path: Path) -> None:
    text = path.read_text(encoding='utf-8')
    permission = '<uses-permission android:name="android.permission.INTERNET" />'
    if permission not in text:
        text, count = re.subn(
            r'(<manifest\b[^>]*>)',
            lambda m: m.group(1) + '\n    ' + permission,
            text,
            count=1,
        )
        if count == 0:
            raise SystemExit('تعذر إضافة صلاحية الإنترنت إلى AndroidManifest.xml')
    text = re.sub(r'android:label="[^"]*"', 'android:label="{}"'.format(APP_LABEL), text)
    path.write_text(text, encoding='utf-8')


def ensure_main_activity(root: Path) -> None:
    package_dir = root / 'android' / 'app' / 'src' / 'main' / 'kotlin' / 'com' / 'example' / 'profit_tracker'
    package_dir.mkdir(parents=True, exist_ok=True)
    activity = package_dir / 'MainActivity.kt'
    if activity.exists():
        return
    activity.write_text(
        'package com.example.profit_tracker\n\n'
        'import io.flutter.embedding.android.FlutterActivity\n\n'
        'class MainActivity : FlutterActivity()\n',
        encoding='utf-8',
    )


def run_launcher_icons(root: Path) -> None:
    dart_path = shutil.which('dart')
    flutter_path = shutil.which('flutter')
    if dart_path:
        run([dart_path, 'run', 'flutter_launcher_icons'], cwd=root)
        return
    if flutter_path:
        run([flutter_path, 'pub', 'run', 'flutter_launcher_icons'], cwd=root)
        return
    print('! تم تخطي توليد الأيقونة لأن dart/flutter غير موجود في PATH')


def main() -> None:
    parser = argparse.ArgumentParser(description='Prepare local Android files for Maen Accounting.')
    parser.add_argument('--skip-pub-get', action='store_true')
    parser.add_argument('--skip-icons', action='store_true')
    args = parser.parse_args()

    root = Path(__file__).resolve().parent.parent
    ensure_android_platform(root)

    if not args.skip_pub_get and shutil.which('flutter'):
        run(['flutter', 'pub', 'get'], cwd=root)

    copy_google_services(root)

    settings_kts = root / 'android' / 'settings.gradle.kts'
    settings_groovy = root / 'android' / 'settings.gradle'
    app_kts = root / 'android' / 'app' / 'build.gradle.kts'
    app_groovy = root / 'android' / 'app' / 'build.gradle'
    root_build_groovy = root / 'android' / 'build.gradle'
    manifest = root / 'android' / 'app' / 'src' / 'main' / 'AndroidManifest.xml'

    if settings_kts.exists():
        patch_settings_kts(settings_kts)
    elif settings_groovy.exists():
        patch_settings_groovy(settings_groovy)
    elif root_build_groovy.exists():
        patch_root_build_groovy(root_build_groovy)

    if app_kts.exists():
        patch_app_kts(app_kts)
    elif app_groovy.exists():
        patch_app_groovy(app_groovy)
    else:
        raise SystemExit('ملف android/app/build.gradle(.kts) غير موجود.')

    if not manifest.exists():
        raise SystemExit('ملف android/app/src/main/AndroidManifest.xml غير موجود.')

    patch_manifest(manifest)
    ensure_main_activity(root)

    if not args.skip_icons:
        run_launcher_icons(root)

    print('\n[OK] Android platform is prepared for local builds.')
    print('Next: flutter analyze && flutter run')


if __name__ == '__main__':
    try:
        main()
    except subprocess.CalledProcessError as exc:
        raise SystemExit(exc.returncode)
