using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

public static class JournalEntryValidator
{
    public static IReadOnlyList<string> ValidateForPosting(
        JournalEntry entry,
        IReadOnlySet<string>? knownAccountIds = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(entry.EntryId))
        {
            errors.Add("معرّف القيد مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(entry.UserId))
        {
            errors.Add("معرّف المستخدم مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(entry.EntryNumber))
        {
            errors.Add("رقم القيد مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(entry.Description))
        {
            errors.Add("بيان القيد مطلوب.");
        }

        if (entry.Status == JournalEntryStatus.Voided)
        {
            errors.Add("لا يمكن ترحيل قيد ملغى.");
        }

        if (entry.Lines.Count == 0)
        {
            errors.Add("يجب أن يحتوي القيد على سطر واحد على الأقل.");
        }

        foreach (var line in entry.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.LineId))
            {
                errors.Add("كل سطر في القيد يحتاج إلى معرّف.");
            }

            if (string.IsNullOrWhiteSpace(line.AccountId))
            {
                errors.Add("كل سطر في القيد يحتاج إلى حساب.");
            }
            else if (knownAccountIds is not null && !knownAccountIds.Contains(line.AccountId))
            {
                errors.Add($"الحساب غير موجود: {line.AccountId}.");
            }

            if (line.DebitMinor < 0 || line.CreditMinor < 0)
            {
                errors.Add($"لا يسمح بقيمة سالبة في السطر {line.LineId}.");
            }

            if (line.DebitMinor > 0 && line.CreditMinor > 0)
            {
                errors.Add($"لا يجوز أن يجمع السطر {line.LineId} بين المدين والدائن.");
            }

            if (line.DebitMinor == 0 && line.CreditMinor == 0)
            {
                errors.Add($"يجب أن يحتوي السطر {line.LineId} على قيمة مدينة أو دائنة.");
            }
        }

        if (entry.Lines.Count > 0 && entry.TotalDebitMinor != entry.TotalCreditMinor)
        {
            errors.Add("إجمالي المدين يجب أن يساوي إجمالي الدائن.");
        }

        if (entry.Lines.All(static line => line.DebitMinor == 0))
        {
            errors.Add("يجب أن يحتوي القيد على طرف مدين.");
        }

        if (entry.Lines.All(static line => line.CreditMinor == 0))
        {
            errors.Add("يجب أن يحتوي القيد على طرف دائن.");
        }

        return errors;
    }

    public static void EnsurePostable(
        JournalEntry entry,
        IReadOnlySet<string>? knownAccountIds = null)
    {
        var errors = ValidateForPosting(entry, knownAccountIds);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }
    }
}
