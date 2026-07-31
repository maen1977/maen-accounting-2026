# معن للمحاسبة — نسخة .NET MAUI

هذه إعادة بناء منظمة للمشروع الأصلي المكتوب بـ Flutter، وتستهدف Android وWindows من قاعدة C# واحدة.

## ما تم تحويله

- تسجيل الدخول وإنشاء الحساب عبر Firebase Authentication REST.
- وضع محلي مستقل دون سحابة.
- إضافة وتعديل وحذف سجل يومي.
- لوحة ملخصات شهرية وسنوية.
- تقارير شهرية مع البحث.
- SQLite محلية.
- نسخ JSON واستيراد نسخ Flutter القديمة (`version: 2`).
- مزامنة Firestore سجلًا بسجل مع حل تعارضات يعتمد على وقت التعديل والإصدار.
- اختبارات لمنطق المال والحساب والمزامنة وعزل المستخدمين والاستيراد القديم.
- GitHub Actions لبناء Android وWindows وتشغيل الاختبارات.

## تحسينات الأمان والدقة

1. كل Firebase UID يحصل على ملف SQLite مستقل، كما أن كل صف يحمل `UserId` ويُتحقق منه.
2. المبالغ مخزنة بوحدة نقدية صغرى (`long`) بدل `double`.
3. الحذف عبارة عن tombstone تتم مزامنته، فلا يعود السجل من جهاز آخر.
4. لا تُرفع مصفوفة قاعدة كاملة ولا تُستبدل نسخة سحابية أحدث.
5. Firestore أصبح في المسار `users/{uid}/entries/{entryId}`.
6. أزيل تحديد الموقع بالـ IP والطقس والسوق لأنهما ليسا جزءًا محاسبيًا وكانا يضيفان مخاطرة خصوصية وتعقيدًا.

## البناء على Windows

المتطلبات:

- Visual Studio 2026 مع حمل عمل .NET MAUI، أو .NET 10 SDK مع MAUI workload.
- Android SDK لبناء Android.

```powershell
dotnet workload install maui
dotnet restore Maen.Accounting.sln
dotnet test tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj
dotnet build src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-android -c Debug
dotnet build src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-windows10.0.19041.0 -c Debug
```

لإنشاء APK وAAB:

```powershell
dotnet publish src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-android -c Release
```

## إعداد Firestore

انشر القواعد الجديدة الموجودة في `firestore.rules`. القواعد القديمة التي كانت تعتمد البريد ومسار `profit_tracker_backups/{email}` لا تناسب التصميم الجديد.

```bash
firebase deploy --only firestore:rules
```

## نقل بيانات Flutter

من التطبيق القديم صدّر ملف JSON. في نسخة MAUI افتح:

`الإعدادات ← استيراد نسخة Flutter أو MAUI`

يجب أن يطابق `backupEmail` بريد الحساب الحالي. يتم تحويل القيم العشرية إلى minor units ودمج السجلات دون حذف الأحدث.

## ملاحظات النشر

- `ApplicationId` أصبح `com.maen.accounting`. إذا نُشر التطبيق القديم بالمعرّف `com.example.profit_tracker` فلن يعتبره المتجر تحديثًا للتطبيق نفسه. غيّر المعرف فقط بعد اتخاذ قرار الترحيل.
- مفتاح Firebase API ليس كلمة مرور، لكن يجب تقييده من Google Cloud Console بحسب تطبيق Android وواجهات API المطلوبة.
- أضف توقيع Android Release عبر أسرار GitHub أو إعدادات Visual Studio قبل رفع AAB للمتجر.
