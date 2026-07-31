# إعداد Firebase للنسخة الجديدة

## المصادقة

التطبيق يستخدم Firebase Authentication REST. إعدادات المشروع الحالية موجودة في `FirebaseOptions.cs`:

- Project ID: `maen-accountings`
- Web API Key: من مشروع Firebase الأصلي

تأكد من تفعيل Email/Password من Firebase Console. إذا كان مفتاح API مقيدًا باسم حزمة Android القديم، أضف الحزمة الجديدة `com.maen.accounting` أو استخدم مفتاح Web API مخصصًا لواجهات Firebase Authentication.

## Firestore

التصميم الجديد لا يستخدم مستندًا واحدًا باسم البريد. المسار الجديد:

```
users/{firebaseUid}/entries/{entryId}
```

انشر القواعد:

```bash
firebase deploy --only firestore:rules
```

القواعد تمنع حذف المستندات مباشرة؛ الحذف يتم عبر `isDeleted = true` للحفاظ على التزامن بين الأجهزة.

## بيانات النسخة القديمة

المستندات القديمة في:

```
profit_tracker_backups/{email}
```

لا تنتقل تلقائيًا. صدّر JSON من تطبيق Flutter ثم استخدم زر الاستيراد داخل نسخة MAUI. بعد الاستيراد نفّذ المزامنة لرفع السجلات إلى البنية الجديدة.
