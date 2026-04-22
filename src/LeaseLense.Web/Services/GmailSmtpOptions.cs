namespace LeaseLense.Web.Services;

public sealed class GmailSmtpOptions
{
    public const string SectionName = "Email:GmailSmtp";

    public bool Enabled { get; init; }
    public string Host { get; init; } = "smtp.gmail.com";
    public int Port { get; init; } = 587;
    public string SenderEmail { get; init; } = string.Empty;
    public string SenderDisplayName { get; init; } = "LeaseLense";
    public string AppPassword { get; init; } = string.Empty;
}
