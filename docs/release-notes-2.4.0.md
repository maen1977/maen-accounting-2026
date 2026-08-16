# Maen Accounting 2.4.0

## العربية

يضيف هذا الإصدار طبقة مزامنة مستقلة للحسابات التجارية، بحيث تتم مزامنة جهات الاتصال والفواتير والدفعات إلى مجموعات Firestore منفصلة تحت حساب المستخدم، مع دمج محلي/سحابي يعتمد على آخر تعديل ثم الإصدار ثم معرّف الجهاز كقاعدة حسم حتمية.

تم تقسيم تجربة الشركات إلى مسارات واضحة لجهات الاتصال والفواتير والدفعات، مع الإبقاء على دفتر الحسابات والهوية البصرية الموحدة وفصل نطاق الحسابات الفردية عن الشركات. بعد المزامنة، يعيد التطبيق قراءة البيانات من السحابة للتحقق من قبول كل سجل مرفوع، ثم يعرض إجمالي السجلات والانتصارات المحلية والسحابية لكل نطاق.

أضيف كتالوج مركزي لترحيلات SQLite مع ترحيل رابع لفهارس التحديث الخاصة بكيانات الشركات. كما أضيفت اختبارات لمحرك دمج وثائق الشركات وكتالوج الترحيلات، إلى جانب الاختبارات السابقة للحسابات الفردية والمزامنة والتقارير.

## English

This release adds an independent synchronization layer for business accounts. Contacts, invoices, and payments are synchronized into separate Firestore collections under the user account, using deterministic conflict resolution based on update time, version, and device identifier.

The business experience is now organized into focused contacts, invoices, and payments workspaces while preserving the unified brand system and the strict separation between personal and business scopes. After uploads, the app re-reads Firestore to verify that every pushed record is available, then reports totals and local/cloud win counters for the synchronized scopes.

SQLite migrations are now described by a central catalog, with a fourth migration adding update-time indexes for business entities. New tests cover the generic business-document merge engine and migration catalog, alongside the existing personal ledger, synchronization, and reporting tests.

## Verification

- Core tests: **44 passed, 0 failed** at the time of the 2.4.0 preparation.
- Android Debug build: **successful**.
- Android Release publish: executed as the final packaging gate before release publication.
