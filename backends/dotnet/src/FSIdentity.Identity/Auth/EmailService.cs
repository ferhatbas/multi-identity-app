using System.Net;
using System.Net.Mail;
using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Entities.Accounts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FSIdentity.Identity.Auth;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Mode { get; set; } = "Log";

    public bool LogBodies { get; set; }

    [EmailAddress]
    public string From { get; set; } = "no-reply@example.test";

    public string FromName { get; set; } = "FSIdentity";

    public string SmtpHost { get; set; } = "localhost";

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    public string? SmtpUser { get; set; }

    public string? SmtpPassword { get; set; }

    public bool UseSsl { get; set; } = true;

    public string PublicUrl { get; set; } = "http://localhost:3040";
}

public record EmailMessage(string To, string Subject, string Html, string Text);

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public class LoggingEmailService(IOptions<EmailOptions> options, ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("E-mail to {To}: {Subject}", message.To, message.Subject);
        if (options.Value.LogBodies)
            logger.LogInformation("{Body}", message.Text);
        return Task.CompletedTask;
    }
}

public class SmtpEmailService(IOptions<EmailOptions> options) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.From, settings.FromName),
            Subject = message.Subject,
            Body = message.Text,
            IsBodyHtml = false,
        };
        mail.To.Add(message.To);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.Html, null, "text/html"));

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort) { EnableSsl = settings.UseSsl };
        if (!string.IsNullOrEmpty(settings.SmtpUser))
            client.Credentials = new NetworkCredential(settings.SmtpUser, settings.SmtpPassword);

        await client.SendMailAsync(mail, cancellationToken);
    }
}

public class EmailTemplates(IOptions<EmailOptions> options)
{
    private string Link(string path, string token) => $"{options.Value.PublicUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(token)}";

    public EmailMessage PasswordReset(User user, string token, int validMinutes)
        => Build(user, "Reset your password",
            $"We received a request to reset your password. The link works for {validMinutes} minutes.",
            "If you did not ask for this, ignore this message; your password stays as it is.",
            Link("auth/reset-password", token), "Reset password");

    public EmailMessage EmailConfirmation(User user, string token)
        => Build(user, "Confirm your e-mail address",
            "Please confirm that this e-mail address is yours.",
            "If you did not create an account, ignore this message.",
            Link("auth/confirm-email", token), "Confirm e-mail");

    public EmailMessage PasswordChanged(User user)
        => Build(user, "Your password was changed",
            "The password of your account was just changed and you were signed out everywhere.",
            "If this was not you, contact your administrator immediately.", null, null);

    public EmailMessage TwoFactorChanged(User user, bool enabled)
        => Build(user, enabled ? "Two-factor authentication is on" : "Two-factor authentication is off",
            enabled ? "Two-factor authentication was turned on for your account."
                : "Two-factor authentication was turned off for your account.",
            "If this was not you, contact your administrator immediately.", null, null);

    private static EmailMessage Build(User user, string subject, string lead, string footer, string? link, string? button)
    {
        var html = $"<p>Hello {WebUtility.HtmlEncode(user.FirstName)},</p><p>{WebUtility.HtmlEncode(lead)}</p>"
                   + (link is null ? "" : $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(button)}</a></p>")
                   + $"<p>{WebUtility.HtmlEncode(footer)}</p>";
        var text = string.Join(Environment.NewLine + Environment.NewLine,
            new[] { $"Hello {user.FirstName},", lead, link, footer }.Where(part => part is not null));
        return new EmailMessage(user.Email, subject, html, text);
    }
}
