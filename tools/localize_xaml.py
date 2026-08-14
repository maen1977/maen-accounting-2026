from pathlib import Path
import re

translations = {
    "أحدث السجلات": "Recent entries",
    "إجمالي الدائن": "Total credit",
    "إجمالي الفواتير المرحّلة": "Posted invoices total",
    "إجمالي المبيعات": "Total sales",
    "إجمالي المدفوعات": "Total payments",
    "إجمالي المدين": "Total debit",
    "إجمالي المصروفات": "Total expenses",
    "إضافة سجل جديد": "Add new entry",
    "إنشاء حساب جديد": "Create account",
    "ابدأ بدون تسجيل": "Continue without registration",
    "استخدم البريد وكلمة المرور للمزامنة والنسخ الاحتياطي السحابي.": "Use your email and password for cloud sync and backup.",
    "استيراد نسخة Flutter أو MAUI": "Import a Flutter or MAUI backup",
    "الإعدادات والنسخ": "Settings & backups",
    "البريد الإلكتروني": "Email",
    "التاريخ": "Date",
    "التقارير": "Reports",
    "الحساب": "Account",
    "الحسابات": "Accounts",
    "الخصوصية": "Privacy",
    "الدخول السحابي": "Cloud sign in",
    "الدخول محليًا بدون تسجيل": "Local access without registration",
    "الربح الإجمالي للشهر": "Monthly gross profit",
    "الربح الإجمالي": "Gross profit",
    "السجل اليومي": "Daily entry",
    "العملاء والموردون والفواتير والمدفوعات": "Customers, suppliers, invoices and payments",
    "العمليات التجارية": "Business operations",
    "القيود المرحّلة": "Posted journal entries",
    "المبيعات": "Sales",
    "المزامنة تقارن وقت وإصدار كل سجل، ولا تستبدل قاعدة كاملة بقاعدة أقدم.": "Sync compares the time and version of every entry; it never replaces a database with an older one.",
    "المزامنة": "Sync",
    "المشتريات والمصروفات اليومية": "Purchases & daily expenses",
    "المصروفات": "Expenses",
    "النسخة الاحتياطية": "Backup",
    "تأكيد كلمة المرور": "Confirm password",
    "تحديث": "Update",
    "ترحيل السجلات القديمة": "Migrate legacy entries",
    "ترحيل القيد": "Post entry",
    "تسجيل الخروج": "Sign out",
    "تسجيل الدخول": "Sign in",
    "تصدير نسخة JSON": "Export JSON backup",
    "تعديل": "Edit",
    "تكلفة القطع": "Cost of goods",
    "حالة الميزان": "Trial balance status",
    "حذف": "Delete",
    "حفظ وترحيل العملية": "Save & post transaction",
    "حفظ وترحيل الفاتورة": "Save & post invoice",
    "حفظ": "Save",
    "دفعة أو قبض جديد": "New payment or receipt",
    "دليل الحسابات وميزان المراجعة والقيود المرحّلة": "Chart of accounts, trial balance and posted entries",
    "دليل الحسابات": "Chart of accounts",
    "سجلات مالية يومية دقيقة مع فصل كامل بين الحسابات": "Accurate daily financial records with complete account isolation",
    "صافي الربح المتوقع": "Expected net profit",
    "صافي السنة": "Year net profit",
    "صافي الشهر": "Month net profit",
    "عدد السجلات": "Entry count",
    "عميل أو مورد جديد": "New customer or supplier",
    "فاتورة جديدة": "New invoice",
    "قيد يومية جديد": "New journal entry",
    "قيد يومية": "Journal entry",
    "كلمة المرور": "Password",
    "لا يرسل التطبيق عنوان IP لتحديد الموقع، ولا يطلب GPS، ولا يجلب الطقس أو أسعار السوق. الاتصال الخارجي مقتصر على تسجيل الدخول والمزامنة التي يختارها المستخدم.": "The app does not send your IP for location, request GPS, or fetch weather or market prices. External connections are limited to sign-in and user-initiated sync.",
    "مبيعات الشهر": "Month sales",
    "متوسط صافي السجل": "Average entry net",
    "مركز المحاسبة": "Accounting center",
    "مزامنة الآن": "Sync now",
    "معن للمحاسبة": "Maen Accounting",
    "ملاحظات": "Notes",
    "ملخص مباشر مبني على سجلاتك المحلية الحالية": "A live summary based on your current local entries",
    "ميزان المراجعة": "Trial balance",
    "نسيت كلمة المرور؟": "Forgot password?",
    "نشط": "Active",
    "نظرة مالية": "Financial overview",
    "يفتح البرنامج مباشرة دون بريد أو كلمة مرور. تبقى البيانات على هذا الجهاز فقط.": "Opens directly without email or password. Data stays on this device only.",
    "يُحفظ سجل واحد لكل تاريخ، وأي تعديل يُسجّل بإصدار جديد للمزامنة الآمنة.": "One entry is kept per date; every edit creates a new version for safe synchronization.",
    "إدخال": "Entry",
    "الإعدادات": "Settings",
    "الحساب الدائن": "Credit account",
    "الحساب المدين": "Debit account",
    "الدخول": "Sign in",
    "الرئيسية": "Home",
    "العمليات": "Operations",
    "العميل أو المورد": "Customer or supplier",
    "المحاسبة": "Accounting",
    "النوع": "Type",
    "نوع العملية": "Transaction type",
    "نوع الفاتورة": "Invoice type",
    "English": "English",
    "اختر اللغة": "Choose your language",
    "اختر لغتك وطريقة استخدامك للتطبيق، وسنجهز لك تجربة مناسبة من البداية.": "Choose your language and how you use the app. We will prepare the right experience from the start.",
    "اختر نوع الحساب": "Choose your account type",
    "الحسابات الفردية": "Personal accounts",
    "الشركات والمحلات": "Business & shops",
    "العربية": "Arabic",
    "لا توجد حسابات.": "No accounts yet.",
    "لا توجد سجلات بعد.": "No entries yet.",
    "لا توجد سجلات في هذا الشهر.": "No entries this month.",
    "لا توجد فواتير.": "No invoices.",
    "لا توجد قيود مرحّلة بعد.": "No posted entries yet.",
    "لا توجد مدفوعات.": "No payments.",
    "لا يوجد عملاء أو موردون.": "No customers or suppliers.",
    "م": "M",
    "متابعة": "Continue",
    "مرحباً بك": "Welcome",
    "نظامك المالي، بصورة أوضح": "Your finances, made clearer",
    "يمكنك تغيير هذا الاختيار لاحقاً من الإعدادات.": "You can change this choice later from Settings.",
}

root = Path('/home/ubuntu/maen-accounting-debug/src/Maen.Accounting.App/Views')
static = set()
for path in root.glob('*.xaml'):
    text = path.read_text()
    for m in re.finditer(r'(?:(?:Text|Title))="([^"]+)"', text):
        value = m.group(1)
        if not value.startswith('{'):
            static.add(value)
    static.update(re.findall(r'EmptyView="([^"]+)"', text))

unknown = sorted(value for value in static if value not in translations)
if unknown:
    raise SystemExit('Missing translations: ' + repr(unknown))

keys = {value: f'T{index:03d}' for index, value in enumerate(sorted(static), 1)}
for path in root.glob('*.xaml'):
    text = path.read_text()
    if 'xmlns:local="clr-namespace:Maen.Accounting.App"' not in text:
        text = text.replace('xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"', 'xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"\n    xmlns:local="clr-namespace:Maen.Accounting.App"', 1)
    for value, key in keys.items():
        text = text.replace(f'Text="{value}"', f'Text="{{local:Tr Key={key}}}"')
        text = text.replace(f'Title="{value}"', f'Title="{{local:Tr Key={key}}}"')
        text = text.replace(f'EmptyView="{value}"', f'EmptyView="{{local:Tr Key={key}}}"')
    path.write_text(text)

lines = ['namespace Maen.Accounting.App;', '', 'public static class UiText', '{', '    public static AppLanguage Language { get; set; } = AppLanguage.Arabic;', '', '    private static readonly Dictionary<string, (string Arabic, string English)> Values = new()', '    {']
for value, key in keys.items():
    ar = value.replace('\\', '\\\\').replace('"', '\\"')
    en = translations[value].replace('\\', '\\\\').replace('"', '\\"')
    lines.append(f'        ["{key}"] = ("{ar}", "{en}"),')
lines += ['    };', '', '    public static string Get(string key)', '    {', '        if (!Values.TryGetValue(key, out var value)) return key;', '        return Language == AppLanguage.English ? value.English : value.Arabic;', '    }', '}']
Path('/home/ubuntu/maen-accounting-debug/src/Maen.Accounting.App/UiText.cs').write_text('\n'.join(lines) + '\n')
