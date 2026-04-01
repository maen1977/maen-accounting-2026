# خطوات التشغيل السريع

## 1) توليد ملفات أندرويد إن لم تكن موجودة
```bash
flutter create . --platforms=android
```

## 2) جلب الحزم
```bash
flutter pub get
```

## 3) التأكد من السماح بالإنترنت على أندرويد
بعد توليد مجلد `android` تأكد من وجود السطر التالي داخل:
`android/app/src/main/AndroidManifest.xml`

```xml
<uses-permission android:name="android.permission.INTERNET" />
```

## 4) فحص الكود
```bash
flutter analyze
```

## 5) تشغيل التطبيق
```bash
flutter run
```

## 6) بناء ملف APK
```bash
flutter build apk --release
```
