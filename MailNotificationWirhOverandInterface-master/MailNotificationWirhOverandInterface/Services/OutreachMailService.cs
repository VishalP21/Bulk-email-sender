using System.Net;
using System.Text.RegularExpressions;
using MailNotificationWirhOverandInterface.Models;
using MailNotificationWirhOverandInterface.Options;
using Microsoft.Extensions.Options;

namespace MailNotificationWirhOverandInterface.Services;

public interface IOutreachMailService
{
    Task<IReadOnlyList<SendOutcome>> SendAsync(
        IReadOnlyList<RecruiterContact> recipients,
        ComposeInput compose,
        byte[]? attachmentBytes,
        string? attachmentName,
        CancellationToken cancellationToken);
}

public sealed class OutreachMailService : IOutreachMailService
{
    private readonly mailNotification _mail;
    private readonly OutreachOptions _options;
    private readonly ILogger<OutreachMailService> _logger;

    public OutreachMailService(
        mailNotification mail,
        IOptions<OutreachOptions> options,
        ILogger<OutreachMailService> logger)
    {
        _mail = mail;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SendOutcome>> SendAsync(
        IReadOnlyList<RecruiterContact> recipients,
        ComposeInput compose,
        byte[]? attachmentBytes,
        string? attachmentName,
        CancellationToken cancellationToken)
    {
        var host = string.IsNullOrWhiteSpace(compose.Host) ? "smtp.gmail.com" : compose.Host.Trim();
        _mail.Host = host;
        _mail.Port = compose.Port <= 0 ? 587 : compose.Port;
        _mail.EnableSsl = true;
        _mail.FromEmail = compose.FromEmail.Trim();
        _mail.FromPassword = compose.FromPassword.Replace(" ", "", StringComparison.Ordinal).Trim();
        _mail.FromName = compose.YourName.Trim();

        var contentType = ContentTypeFor(attachmentName);
        var outcomes = new List<SendOutcome>();
        var delay = TimeSpan.FromMilliseconds(Math.Clamp(_options.DelayMilliseconds, 0, 10000));

        for (var index = 0; index < recipients.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recipient = recipients[index];
            var tokens = Tokens(recipient, compose);
            var subject = SingleLine(Apply(compose.Subject, tokens));
            var body = ToHtml(Apply(compose.Body, tokens));
            var sent = _mail.TrySend(
                recipient.Email,
                subject,
                body,
                attachmentBytes,
                attachmentName,
                contentType,
                out var error);

            outcomes.Add(new SendOutcome
            {
                CompanyName = recipient.CompanyName,
                Email = recipient.Email,
                Success = sent,
                Detail = sent ? "Sent" : error
            });

            _logger.LogInformation(
                "Outreach mail to {Company} {Email} {Result}",
                recipient.CompanyName,
                recipient.Email,
                sent ? "sent" : "failed");

            if (sent && index < recipients.Count - 1 && delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        return outcomes;
    }

    public static Dictionary<string, string> Tokens(RecruiterContact recipient, ComposeInput compose)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CompanyName"] = recipient.CompanyName.Trim(),
            ["YourName"] = compose.YourName.Trim(),
            ["Role"] = compose.Role.Trim(),
            ["Phone"] = compose.Phone?.Trim() ?? "",
            ["Link"] = compose.Link?.Trim() ?? "",
            ["Extra"] = compose.Extra?.Trim() ?? ""
        };
    }

    public static string Apply(string template, IReadOnlyDictionary<string, string> tokens)
    {
        var result = template ?? "";
        foreach (var token in tokens)
        {
            result = result.Replace("{{" + token.Key + "}}", token.Value, StringComparison.OrdinalIgnoreCase);
        }

        result = Regex.Replace(result, @"[ \t]+\n", "\n");
        result = Regex.Replace(result, @"(\n\s*){3,}", "\n\n");
        return result.Trim();
    }

    private static string ToHtml(string text)
    {
        return WebUtility.HtmlEncode(text).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "<br>");
    }

    private static string SingleLine(string value)
    {
        return value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
    }

    private static string? ContentTypeFor(string? fileName)
    {
        return Path.GetExtension(fileName ?? "").ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "" => null,
            _ => "application/octet-stream"
        };
    }
}
