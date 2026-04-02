## تم دمج google-services.json
- هذا الملف تم وضعه داخل `firebase/google-services.json` وسيتم نسخه تلقائيًا إلى `android/app/google-services.json` أثناء البناء في GitHub Actions.
- يبقى عليك فقط تفعيل Authentication و Firestore من Firebase Console.

# إعداد Firebase للمزامنة التلقائية

هذه النسخة تستخدم:
- Firebase Authentication (تسجيل الدخول بالإيميل وكلمة المرور)
- Cloud Firestore (نسخة احتياطية سحابية واسترجاع تلقائي بين الأجهزة)

## الخطوات
1. ثبت Firebase CLI و FlutterFire CLI.
2. من داخل المشروع نفّذ:
   - flutter pub get
   - flutterfire configure
3. فعّل Email/Password من Firebase Authentication.
4. أنشئ Cloud Firestore في وضع الإنتاج أو الاختبار ثم عدّل القواعد.
5. أعد بناء التطبيق.

## سلوك البرنامج
- بعد تسجيل الدخول يتم حفظ البيانات داخل SQLite محليًا.
- بعد كل إضافة أو تعديل أو حذف يحاول البرنامج تحديث النسخة المحلية والنسخة السحابية تلقائيًا.
- إذا تم تثبيت التطبيق على جهاز آخر وتسجيل الدخول بنفس الحساب، يحاول البرنامج استعادة البيانات تلقائيًا من السحابة إذا كانت قاعدة البيانات المحلية فارغة.

## ملاحظات
- هذه النسخة لا تتضمن firebase_options.dart لأن هذا الملف يجب توليده حسب مشروع Firebase الخاص بك عبر flutterfire configure.
- يمكن إبقاء زر مشاركة النسخة إلى البريد كطبقة أمان إضافية فوق المزامنة السحابية.
