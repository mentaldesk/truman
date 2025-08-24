namespace Truman.Api.Features.Email;

public class EmailConfiguration
{
    public BrevoSettings Brevo { get; set; } = new();
}

public class BrevoSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public long MagicLinkTemplateId { get; set; }
} 