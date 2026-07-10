namespace CartSmart.Api.Auth;

public interface IEmailSender
{
    Task SendPasswordResetEmailAsync(string toEmail, string rawResetToken, CancellationToken cancellationToken);
}

// No email-delivery provider is wired up yet (see CartSmart.Api change-request doc, Section 3
// open question #4). This logs instead of sending so the reset flow is testable end to end;
// swap in a real provider (e.g. SendGrid/SES) behind this same interface before production.
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendPasswordResetEmailAsync(string toEmail, string rawResetToken, CancellationToken cancellationToken)
    {
        logger.LogInformation("Password reset requested for {Email}. Reset token: {ResetToken}", toEmail, rawResetToken);
        return Task.CompletedTask;
    }
}
