# Maen Accounting 2.3.0

## العربية

هذا الإصدار يمثل تحديثاً شاملاً لتجربة Maen Accounting وبنيته الداخلية.

### أبرز التحديثات

- إعادة بناء الهوية البصرية بألوان navy/gold وتوحيد شريط الهوية في المسارات الرئيسية.
- الحفاظ على الفصل الكامل بين الحسابات الفردية وحسابات الشركات.
- إضافة ملخصات الحسابات الفردية، الرصيد البنكي، رصيد الشهر والسنة، وتحليل المصروفات حسب الفئة.
- إضافة تصدير تقارير الحسابات إلى CSV مع دعم العربية وهروب الفواصل والاقتباسات بصورة صحيحة.
- إضافة نظام ترحيلات SQLite مرقم وآمن للتحديث التدريجي لقواعد البيانات الموجودة.
- تحسين نتائج المزامنة لعرض السجلات المرفوعة وانتصارات المحلي وانتصارات السحابة والإجمالي.
- إضافة محرك دمج مركزي للتحقق من ملكية السجلات، اختيار النسخة الأحدث، ومنع الرفع غير الضروري.
- رفع إصدار التطبيق إلى 2.3.0 ورقم البناء إلى 5.

### التحقق

- اختبارات النواة: **38 ناجحة، 0 فشل**.
- Android Debug: **نجاح**.
- Android Release: **نجاح**، مع إنشاء APK وAAB موقّعين ضمن مخرجات النشر.

## English

This release delivers a comprehensive modernization of Maen Accounting and its internal architecture.

### Highlights

- Refreshed navy/gold visual identity and a shared brand header across the main flows.
- Preserved the complete separation between personal and business accounting.
- Added personal-ledger summaries, bank balance, monthly/yearly totals, and category spending analysis.
- Added CSV report export with correct escaping for Arabic text, commas, and quotation marks.
- Added numbered, idempotent SQLite migrations for safe upgrades of existing databases.
- Improved sync results to expose uploaded records, local wins, cloud wins, and total records.
- Added a central merge engine that validates ownership, selects the newest record, and avoids unnecessary uploads.
- Bumped the application to version 2.3.0 and Android build number 5.

### Verification

- Core tests: **38 passed, 0 failed**.
- Android Debug: **passed**.
- Android Release: **passed**, producing signed APK and AAB artifacts.
