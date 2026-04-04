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


إعداد الاسم والأيقونة على أندرويد:
- اسم التطبيق بعد التثبيت: Maen Accountings
- أيقونة التطبيق يتم توليدها تلقائيًا من الملف assets/icon/maen_accountings_icon.png عبر flutter_launcher_icons داخل GitHub Actions.
- عند البناء محليًا شغّل: dart run flutter_launcher_icons ثم flutter build apk --release
