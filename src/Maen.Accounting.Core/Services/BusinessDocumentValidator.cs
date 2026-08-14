using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

public static class BusinessDocumentValidator
{
    public static IReadOnlyList<string> ValidateInvoice(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(invoice.InvoiceId)) errors.Add("معرّف الفاتورة مطلوب.");
        if (string.IsNullOrWhiteSpace(invoice.UserId)) errors.Add("معرّف المستخدم مطلوب.");
        if (string.IsNullOrWhiteSpace(invoice.Number)) errors.Add("رقم الفاتورة مطلوب.");
        if (string.IsNullOrWhiteSpace(invoice.ContactId)) errors.Add("العميل أو المورد مطلوب.");
        if (invoice.Lines.Count == 0) errors.Add("يجب أن تحتوي الفاتورة على بند واحد على الأقل.");
        if (invoice.TaxMinor < 0) errors.Add("الضريبة لا يمكن أن تكون سالبة.");
        if (invoice.Status == InvoiceStatus.Posted && invoice.TotalMinor <= 0) errors.Add("إجمالي الفاتورة المرحّلة يجب أن يكون موجباً.");
        foreach (var line in invoice.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Description)) errors.Add("وصف بند الفاتورة مطلوب.");
            if (line.AmountMinor <= 0) errors.Add("قيمة كل بند في الفاتورة يجب أن تكون موجبة.");
        }
        return errors;
    }

    public static IReadOnlyList<string> ValidatePayment(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(payment.PaymentId)) errors.Add("معرّف الدفعة مطلوب.");
        if (string.IsNullOrWhiteSpace(payment.UserId)) errors.Add("معرّف المستخدم مطلوب.");
        if (string.IsNullOrWhiteSpace(payment.Number)) errors.Add("رقم الدفعة مطلوب.");
        if (string.IsNullOrWhiteSpace(payment.ContactId)) errors.Add("العميل أو المورد مطلوب.");
        if (payment.AmountMinor <= 0) errors.Add("قيمة الدفعة يجب أن تكون موجبة.");
        return errors;
    }

    public static void EnsureValidInvoice(Invoice invoice)
    {
        var errors = ValidateInvoice(invoice);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
    }

    public static void EnsureValidPayment(Payment payment)
    {
        var errors = ValidatePayment(payment);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
    }
}
