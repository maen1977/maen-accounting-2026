using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

public static class DocumentJournalFactory
{
    public static JournalEntry CreateInvoiceJournal(Invoice invoice, string deviceId = "")
    {
        BusinessDocumentValidator.EnsureValidInvoice(invoice);
        if (invoice.Status != InvoiceStatus.Posted)
        {
            throw new InvalidOperationException("لا يمكن إنشاء قيد لفاتورة غير مرحّلة.");
        }

        var lines = new List<JournalLine>();
        var receivableOrPayable = invoice.Type == InvoiceType.Sales
            ? DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.ReceivablesCode)
            : DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.PayablesCode);
        var revenueOrExpense = invoice.Type == InvoiceType.Sales
            ? DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.SalesRevenueCode)
            : DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.OperatingExpensesCode);
        var taxAccount = DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.TaxPayableCode);

        if (invoice.Type == InvoiceType.Sales)
        {
            lines.Add(new JournalLine(
                $"{invoice.InvoiceId}-receivable",
                receivableOrPayable,
                DebitMinor: invoice.TotalMinor,
                Description: "إثبات ذمم فاتورة مبيعات"));
            lines.Add(new JournalLine(
                $"{invoice.InvoiceId}-revenue",
                revenueOrExpense,
                CreditMinor: invoice.SubtotalMinor,
                Description: "إثبات إيراد فاتورة مبيعات"));
            AddTaxLine(lines, invoice, taxAccount, credit: true);
        }
        else
        {
            lines.Add(new JournalLine(
                $"{invoice.InvoiceId}-expense",
                revenueOrExpense,
                DebitMinor: invoice.SubtotalMinor,
                Description: "إثبات مصروف فاتورة مشتريات"));
            lines.Add(new JournalLine(
                $"{invoice.InvoiceId}-payable",
                receivableOrPayable,
                CreditMinor: invoice.TotalMinor,
                Description: "إثبات ذمم فاتورة مشتريات"));
            AddTaxLine(lines, invoice, taxAccount, credit: false);
        }

        var entry = new JournalEntry(
            $"invoice-journal-{invoice.InvoiceId}",
            invoice.UserId,
            invoice.IssueDate,
            invoice.Number,
            invoice.Type == InvoiceType.Sales ? "ترحيل فاتورة مبيعات" : "ترحيل فاتورة مشتريات",
            lines,
            JournalEntryStatus.Posted,
            invoice.InvoiceId,
            invoice.CreatedAtUtc,
            invoice.UpdatedAtUtc,
            invoice.Version,
            string.IsNullOrWhiteSpace(deviceId) ? invoice.DeviceId : deviceId);
        JournalEntryValidator.EnsurePostable(entry);
        return entry;
    }

    public static JournalEntry CreatePaymentJournal(Payment payment, string deviceId = "")
    {
        BusinessDocumentValidator.EnsureValidPayment(payment);
        var cash = DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.CashCode);
        var receivableOrPayable = payment.Type == PaymentType.CustomerReceipt
            ? DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.ReceivablesCode)
            : DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.PayablesCode);
        var lines = payment.Type == PaymentType.CustomerReceipt
            ? new List<JournalLine>
            {
                new($"{payment.PaymentId}-cash", cash, DebitMinor: payment.AmountMinor, Description: "إثبات تحصيل من عميل"),
                new($"{payment.PaymentId}-receivable", receivableOrPayable, CreditMinor: payment.AmountMinor, Description: "تخفيض ذمم عميل")
            }
            :
            [
                new JournalLine($"{payment.PaymentId}-payable", receivableOrPayable, DebitMinor: payment.AmountMinor, Description: "تخفيض ذمم مورد"),
                new JournalLine($"{payment.PaymentId}-cash", cash, CreditMinor: payment.AmountMinor, Description: "إثبات دفعة لمورد")
            ];

        var entry = new JournalEntry(
            $"payment-journal-{payment.PaymentId}",
            payment.UserId,
            payment.PaymentDate,
            payment.Number,
            payment.Type == PaymentType.CustomerReceipt ? "ترحيل قبض من عميل" : "ترحيل دفعة إلى مورد",
            lines,
            JournalEntryStatus.Posted,
            payment.PaymentId,
            payment.CreatedAtUtc,
            payment.UpdatedAtUtc,
            payment.Version,
            string.IsNullOrWhiteSpace(deviceId) ? payment.DeviceId : deviceId);
        JournalEntryValidator.EnsurePostable(entry);
        return entry;
    }

    private static void AddTaxLine(
        ICollection<JournalLine> lines,
        Invoice invoice,
        string taxAccount,
        bool credit)
    {
        if (invoice.TaxMinor <= 0)
        {
            return;
        }

        lines.Add(credit
            ? new JournalLine($"{invoice.InvoiceId}-tax", taxAccount, CreditMinor: invoice.TaxMinor, Description: "إثبات ضريبة مبيعات مستحقة")
            : new JournalLine($"{invoice.InvoiceId}-tax", taxAccount, DebitMinor: invoice.TaxMinor, Description: "إثبات ضريبة مشتريات قابلة للخصم"));
    }
}
