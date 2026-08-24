using System.Reflection;
using System.Text.Json;

namespace Maen.Accounting.App.Services;

internal static class CloudSyncExceptionFormatter
{
    public static string GetDetail(Exception exception)
    {
        System.Diagnostics.Debug.WriteLine($"[CloudSync] {exception}");

        var root = Unwrap(exception);
        var message = root.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message)
            || message.Contains("Arg_TargetInvocationException", StringComparison.OrdinalIgnoreCase))
        {
            return UiText.Format("T865", root.GetType().Name);
        }

        return message;
    }

    private static Exception Unwrap(Exception exception)
    {
        var current = exception;
        while (true)
        {
            if (current is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
            {
                current = aggregate.InnerExceptions[0];
                continue;
            }

            if (current.InnerException is not null
                && (current is TargetInvocationException
                    || current is TypeInitializationException
                    || current is JsonException
                    || current.Message.Contains("Arg_TargetInvocationException", StringComparison.OrdinalIgnoreCase)))
            {
                current = current.InnerException;
                continue;
            }

            return current;
        }
    }
}
