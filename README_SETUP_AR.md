# خطوات التشغيل السريع

## الطريقة الأسرع محليًا
من داخل المشروع نفّذ:

```bash
python3 tools/prepare_android_local.py
flutter analyze
flutter run
```

هذا السكربت يقوم تلقائيًا بـ:
- إنشاء مجلد `android` إذا كان غير موجود
- نسخ `firebase/google-services.json` إلى `android/app/google-services.json`
- تفعيل Google Services داخل Gradle
- ضبط اسم التطبيق إلى `Maen Accountings`
- إضافة صلاحية الإنترنت إلى `AndroidManifest.xml`
- توليد الأيقونة من `assets/icon/maen_accountings_icon.png`

## إذا أردت تنفيذ الخطوات يدويًا
### 1) توليد ملفات أندرويد إن لم تكن موجودة
```bash
flutter create . --platforms=android
```

### 2) جلب الحزم
```bash
flutter pub get
```

### 3) تجهيز أندرويد وFirebase محليًا
```bash
python3 tools/prepare_android_local.py --skip-pub-get
```

### 4) فحص الكود
```bash
flutter analyze
```

### 5) تشغيل التطبيق
```bash
flutter run
```

### 6) بناء ملف APK
```bash
flutter build apk --release
```

## ملاحظات مهمة
- اسم التطبيق بعد التثبيت: `Maen Accountings`
- السكربت المحلي يجعل التشغيل اليدوي قريبًا من نفس خطوات GitHub Actions
- إذا أردت دعم منصات أخرى غير Android، نفّذ `flutterfire configure` لتوليد إعدادات FlutterFire المناسبة لها
