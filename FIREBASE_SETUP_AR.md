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

قواعد `firestore.rules` تسمح للمستخدم المصادق بقراءة وكتابة المستندات التي يكون `userId` فيها مساوياً لمعرف Firebase الخاص به فقط. ولا تسمح بالحذف المباشر؛ الحذف يتم عبر `isDeleted = true` للحفاظ على التزامن بين الأجهزة.

### نشر القواعد على المشروع الصحيح

ملف `.firebaserc` يحدد المشروع `maen-accountings` لتجنب النشر إلى مشروع آخر. من جهاز يملك صلاحية إدارة مشروع Firebase نفّذ:

```bash
firebase login
firebase use maen-accountings
firebase deploy --only firestore:rules
```

لا تستبدل القواعد بقاعدة عامة مثل `allow read, write: if true;`؛ ذلك يعرّض بيانات جميع المستخدمين للخطر. إذا ظهر في التطبيق `HTTP 403 — Missing or insufficient permissions`، فهذا يعني غالباً أن القواعد الموجودة في Console لم تُنشر بعد أو أنها مختلفة عن ملف `firestore.rules` في المستودع. بعد نشرها، سجّل الخروج والدخول مرة واحدة ثم اضغط «مزامنة الآن».

## بيانات النسخة القديمة

المستندات القديمة في:

```
profit_tracker_backups/{email}
```

لا تنتقل تلقائيًا. صدّر JSON من تطبيق Flutter ثم استخدم زر الاستيراد داخل نسخة MAUI. بعد الاستيراد نفّذ المزامنة لرفع السجلات إلى البنية الجديدة.
