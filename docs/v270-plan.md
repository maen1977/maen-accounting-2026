# خطة v2.7.0 — رفع المستوى بكل المعايير

## تحليل الفجوات (بعد v2.6.0)

### فجوات تجربة الأعمال
1. لا يوجد بحث/تصفية في جهات الاتصال والفواتير والمدفوعات.
2. لا تعديل أو إلغاء (Void) أو حذف للفواتير/المدفوعات بعد الحفظ.
3. الفاتورة ببنود متعددة (InvoiceLine موجود في النموذج لكن الواجهة سطر واحد فقط) — يجب تفعيل البنود.
4. لا توجد دورة حياة الفاتورة (Draft → ترحيل → إلغاء) في الواجهة.
5. لا تقرير أعمال شهري (مبيعات/مشتريات/تحصيل) — التقارير للأفراد فقط.
6. لا تصدير CSV لبيانات الأعمال.

### فجوات تجربة الأفراد
1. لا بحث/تصفية حسب الفئة أو الفترة في صفحة الحركات.
2. لا حسابات بنكية... (موجود من قبل — تحقق: "الحساب البنكي" طلب سابق).
3. لا تنبيهات/إشعارات للالتزامات القريبة.

### فجوات عامة
1. لا رسوم بيانية حقيقية (CanvasView) — الاتجاهات حالياً أشرطة نصية.
2. لا إحصاءات تفاعلية في لوحة التحكم (أسرع، أغلى فئة، أطول سلسلة...).

## نطاق v2.7.0 (موزون: لا مبالغة بلا قيمة)
1. **Core**: BusinessMonthlyReportCalculator (تقرير أعمال شهري: مبيعات/مشتريات/تحصيل/صافي + جهات أكبر).
2. **Core**: BusinessDocumentLifecycle service (تدوال InvoiceStatus + Payment reconciliation per invoice = مبلغ مدفوع لكل فاتورة).
3. **Core**: ReconciledInvoice (فاتورة مع مدفوع منها ومتبقي) — عرض متبقي لكل فاتورة.
4. **App UI - أعمال**:
   - بحث في الفواتير والجهات (search bar + filter).
   - تعديل/إلغاء/حذف الفاتورة، تعديل/حذف المدفوعات، تعديل/إلغاء جهة الاتصال (عبر أزرار في القائمة).
   - فاتورة متعددة البنود (إضافة بنود).
   - قائمة مدفوع/متبقي لكل فاتورة.
   - صفحة تقارير الأعمال (BusinessReportsPage) ضمن تبويبات الأعمال: تقرير شهري + تصدير CSV.
5. **App UI - أفراد**: بحث وتصفية الفئات والفترة في الحركات (PersonalDashboardPage / search) — ملاحظة: MovementSearchEngine موجود، يُستخدم في شريط بحث عالمي.
6. **UI**: رسم بياني CanvasView (خط أعمدة) لأعمدة التقرير السنوي واتجاه الادخار.
7. **Tests**: اختبارات المحركات الجديدة (Lifecycle + MonthlyReport + Reconciliation) — الهدف 160+.
8. **Keys**: المفاتيح اللغوية T487+ بالعربية/الإنجليزية.
9. Version: 2.7.0 / versionCode 9.

## ملاحظات تقنية (حقائق مثبتة)
- BusinessRepository: UpsertInvoiceAsync/UpsertPaymentAsync موجودة; UpsertContactAsync موجودة; لا توجد Delete methods (الحذف عبر soft field؟ لا يوجد IsDeleted في BusinessDocuments — إلغاء الفاتورة عبر Status=Voided فقط).
- InvoiceLine موجود في Invoice record. Invoice.TotalMinor = Subtotal + TaxMinor.
- BusinessViewModel يرث ObservableObject (App.Infrastructure). DashboardPage يربط MainStateViewModel (proxy لخصائص FinancialPosition).
- BusinessTabbedPage: contacts/invoices/payments فقط — إضافة BusinessReportsPage يحتاج تعديل BusinessTabbedPage (code-only tabbed).
- UiText: Dictionary entries ["T486"] آخر مفتاح. Pattern: (Arabic, English).
- Money.Format(long minor). Palette ألوان في CategoryRegistryCalculator/ReportDetailItemViewModels.
- الاختبارات: tests/Maen.Accounting.Core.Tests/V250EngineTests.cs + V260EngineTests.cs — ProfitEntry ctor، Contact/Invoice/Payment records (Core.Models.BusinessDocuments + PersonalLedger).
- publish: dotnet publish -f net10.0-android -c Release -p:AndroidPackageFormat=apk ثم aab؛ الملفات الموقعة: bin/Release/net10.0-android/com.maen.accounting-Signed.apk/aab.
- release: cd repo && gh release create vX.Y.Z --title ... --notes-file docs/release-notes-X.Y.Z.md <apk> <aab>
- Commit style: "feat: vX.Y.Z — ..." ثم git push. Release URL: https://github.com/maen1977/maen-accounting-2026/releases/tag/vX.Y.Z
