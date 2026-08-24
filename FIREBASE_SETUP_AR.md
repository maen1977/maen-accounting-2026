# إعداد Firebase للنسخة الجديدة

## المصادقة

التطبيق يستخدم Firebase Authentication REST. إعدادات المشروع الحالية موجودة في `FirebaseOptions.cs`:

- Project ID: `maen-accountings`
- Web API Key: من مشروع Firebase الأصلي

تأكد من تفعيل Email/Password من Firebase Console. التطبيق الحالي يستعمل Firebase Authentication REST، لذلك يجب أن يكون مفتاح Web API مسموحًا لواجهات Authentication. إذا كانت قيود مفتاح API أو إعداد Android تعتمد على اسم الحزمة القديم، أنشئ/أضف تطبيق Android باسم `com.maen.accounting` ثم نزّل `google-services.json` الجديد بدل تعديل الملف القديم يدويًا.

## Firestore

التصميم الجديد لا يستخدم مستندًا واحدًا باسم البريد للبيانات الجديدة. المسارات الجديدة هي:

```
users/{firebaseUid}/entries/{entryId}
users/{firebaseUid}/personalEntries/{entryId}
users/{firebaseUid}/businessContacts/{recordId}
users/{firebaseUid}/businessInvoices/{recordId}
users/{firebaseUid}/businessPayments/{recordId}
users/{firebaseUid}/personalPlans/{recordId}
users/{firebaseUid}/personalObligations/{recordId}
users/{firebaseUid}/personalDeposits/{recordId}
```

قواعد `firestore.rules` تسمح للمستخدم المصادق بقراءة وكتابة مستنداته فقط، وتتحقق من `userId` و`accountScope` ونوع السجل. ولا تسمح بالحذف المباشر؛ الحذف يتم عبر `isDeleted = true` في السجلات التي تدعم الحذف المنطقي للحفاظ على التزامن بين الأجهزة. كما تسمح القواعد بقراءة مستند النسخة القديمة المطابق للبريد فقط لغرض الترحيل، ولا تسمح بتعديله أو تعداده.

### نشر القواعد على المشروع الصحيح

ملف `.firebaserc` يحدد المشروع `maen-accountings` لتجنب النشر إلى مشروع آخر. من جهاز يملك صلاحية إدارة مشروع Firebase نفّذ:

```bash
firebase login
firebase use maen-accountings
firebase deploy --only firestore:rules
```

لا تستبدل القواعد بقاعدة عامة مثل `allow read, write: if true;`؛ ذلك يعرّض بيانات جميع المستخدمين للخطر. إذا ظهر في التطبيق `HTTP 403 — Missing or insufficient permissions`، فهذا يعني غالباً أن القواعد الموجودة في Console لم تُنشر بعد أو أنها مختلفة عن ملف `firestore.rules` في المستودع. بعد نشرها، سجّل الخروج والدخول مرة واحدة ثم اضغط «مزامنة الآن». عند تسجيل الدخول بحساب موجود، وإذا كانت قاعدة الجهاز فارغة، يحاول التطبيق قراءة النسخة القديمة `profit_tracker_backups/{email}` ثم تحويلها إلى السجلات الجديدة ورفعها تحت UID؛ لذلك يجب نشر قاعدة القراءة القديمة مرة واحدة أثناء الترحيل.

## بيانات النسخة القديمة

المستندات القديمة في:

```
profit_tracker_backups/{email}
```

تحاول النسخة الجديدة قراءتها تلقائيًا عند أول تسجيل دخول بالحساب نفسه إذا كانت قاعدة الجهاز فارغة، ثم تحول السجلات وترفعها إلى البنية الجديدة تحت UID. إذا لم تكن النسخة القديمة متاحة للقراءة أو فشل الترحيل، صدّر JSON من تطبيق Flutter ثم استخدم زر الاستيراد داخل نسخة MAUI، وبعد الاستيراد نفّذ المزامنة لرفع السجلات إلى البنية الجديدة.
