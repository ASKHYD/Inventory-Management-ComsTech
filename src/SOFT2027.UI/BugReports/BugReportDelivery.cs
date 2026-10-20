using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Windows;

namespace SOFT2027.UI.BugReports;

public sealed record BugReportMetadata(string AppVersion, string WindowsVersion, string Module, DateTimeOffset ReportedAt, string Role);
public sealed record BugReportInput(string Title, string Happened, string Steps, string Expected, string Severity, string Repeatable, BugReportMetadata Metadata);
public sealed record BugReportDestinations(string Email, string WhatsAppNumber);

/// <summary>All report destinations are configured in one place. Empty WhatsApp disables that route.</summary>
public static class BugReportConfiguration
{
    public static readonly BugReportDestinations Destinations = new("hello@comstech.tech", "");
    public const string SubjectPrefix = "[SOFT2027 Bug Report]";
}

public enum BugReportRoute { Copy, Save, Mailto, Gmail, WhatsApp }

public interface IBugReportSender
{
    string Deliver(BugReportInput report);
}

public static class BugReportText
{
    private static readonly Regex Credential = new(@"(?i)\b(password|passcode|token|api[_ -]?key|secret)\s*[:=]\s*[^\r\n]+", RegexOptions.Compiled);
    private static readonly Regex WindowsPath = new(@"(?i)(?:[a-z]:\\[^\s\r\n]+|\\\\[^\s\r\n]+)", RegexOptions.Compiled);

    public static string Sanitize(string? value)
    {
        var safe = Credential.Replace(value ?? "", "$1=[REDACTED]");
        return WindowsPath.Replace(safe, "[PATH REDACTED]");
    }

    public static string Subject(BugReportInput report) =>
        $"{BugReportConfiguration.SubjectPrefix} {Sanitize(report.Title).Trim()}";

    public static string Full(BugReportInput report) => string.Join("\r\n", new[]
    {
        Subject(report), $"App version: {Sanitize(report.Metadata.AppVersion)}",
        $"Windows version: {Sanitize(report.Metadata.WindowsVersion)}", $"Screen/module: {Sanitize(report.Metadata.Module)}",
        $"Date/time: {report.Metadata.ReportedAt:dd/MM/yyyy HH:mm}", $"Role: {Sanitize(report.Metadata.Role)}",
        $"Severity: {Sanitize(report.Severity)}", $"Repeatable: {Sanitize(report.Repeatable)}", "",
        "What happened:", Sanitize(report.Happened), "", "Steps to reproduce:", Sanitize(report.Steps), "",
        "Expected result:", Sanitize(report.Expected), "",
        "Privacy: do not include credentials, database contents, or full file paths. No screenshot is captured or attached."
    });
}

/// <summary>Builds prefilled URLs only; opening a URL never sends a report.</summary>
public static class BugReportUrlBuilder
{
    public const int MaximumUrlLength = 1500;
    private const string ShortenedNote = "\r\n[Report shortened for this link. Use Copy report or Save to file for the full text.]";

    public static string Build(BugReportRoute route, BugReportInput report, string? whatsappNumber = null)
    {
        var destinations = BugReportConfiguration.Destinations;
        var recipient = route == BugReportRoute.WhatsApp ? Digits(whatsappNumber ?? destinations.WhatsAppNumber) : destinations.Email;
        if (route == BugReportRoute.WhatsApp && recipient.Length == 0)
            throw new InvalidOperationException("A WhatsApp destination is not configured.");
        var subject = BugReportText.Subject(report);
        var body = BugReportText.Full(report);
        string Compose(string text)
        {
            var encodedSubject = Uri.EscapeDataString(subject);
            var encodedBody = Uri.EscapeDataString(text);
            return route switch
            {
                BugReportRoute.Mailto => $"mailto:{Uri.EscapeDataString(recipient)}?subject={encodedSubject}&body={encodedBody}",
                BugReportRoute.Gmail => $"https://mail.google.com/mail/?view=cm&fs=1&to={Uri.EscapeDataString(recipient)}&su={encodedSubject}&body={encodedBody}",
                BugReportRoute.WhatsApp => $"https://wa.me/{recipient}?text={Uri.EscapeDataString(subject + "\r\n" + text)}",
                _ => throw new ArgumentOutOfRangeException(nameof(route))
            };
        }

        var url = Compose(body);
        if (url.Length <= MaximumUrlLength) return url;

        // Trim by Unicode scalar values so an Urdu or supplementary character isn't split.
        var runes = body.EnumerateRunes().ToArray();
        var low = 0;
        var high = runes.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            var candidate = string.Concat(runes.Take(mid).Select(r => r.ToString())) + ShortenedNote;
            if (Compose(candidate).Length <= MaximumUrlLength) low = mid; else high = mid - 1;
        }
        url = Compose(string.Concat(runes.Take(low).Select(r => r.ToString())) + ShortenedNote);
        if (url.Length > MaximumUrlLength) throw new InvalidOperationException("The report title is too long for a prefilled URL.");
        return url;
    }

    private static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());
}

public sealed class CopyToClipboard : IBugReportSender
{
    public string Deliver(BugReportInput report) { Clipboard.SetText(BugReportText.Full(report)); return "Full report copied to the clipboard."; }
}

public sealed class SaveToFile : IBugReportSender
{
    public string Deliver(BugReportInput report)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOFT2027 Reports", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "report.txt"), BugReportText.Full(report), new UTF8Encoding(false));
        return folder;
    }
}

public sealed class MailtoDraft : IBugReportSender
{
    public string Deliver(BugReportInput report) { Open(BugReportUrlBuilder.Build(BugReportRoute.Mailto, report)); return "Email app opened with a draft. Review it before choosing whether to send."; }
    internal static void Open(string uri) => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
}

public sealed class GmailWeb : IBugReportSender
{
    public string Deliver(BugReportInput report) { MailtoDraft.Open(BugReportUrlBuilder.Build(BugReportRoute.Gmail, report)); return "Gmail opened with a draft. Review it before choosing whether to send."; }
}

public sealed class WhatsApp : IBugReportSender
{
    public string Deliver(BugReportInput report) { MailtoDraft.Open(BugReportUrlBuilder.Build(BugReportRoute.WhatsApp, report)); return "WhatsApp opened with a prefilled message. Review it before choosing whether to send."; }
}
