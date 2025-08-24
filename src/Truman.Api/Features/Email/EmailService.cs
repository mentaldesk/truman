using brevo_csharp.Api;
using brevo_csharp.Client;
using brevo_csharp.Model;
using Microsoft.Extensions.Options;
using Task = System.Threading.Tasks.Task;

namespace Truman.Api.Features.Email;

public interface IEmailService
{
    Task SendMagicLinkEmailAsync(string email, string magicLink);
}

public class EmailService : IEmailService
{
    private readonly BrevoSettings _settings;
    private readonly TransactionalEmailsApi _emailsApi;

    public EmailService(IOptions<EmailConfiguration> options)
    {
        _settings = options.Value.Brevo;
        
        brevo_csharp.Client.Configuration.Default.ApiKey.Add("api-key", _settings.ApiKey);
        _emailsApi = new TransactionalEmailsApi();
    }

    public async Task SendMagicLinkEmailAsync(string toEmail, string magicLink)
    {
        var email = new SendSmtpEmail
        {
            To = new List<SendSmtpEmailTo> { new SendSmtpEmailTo(email: toEmail) },
            TemplateId = _settings.MagicLinkTemplateId,
            Params = new Dictionary<string, object> { { "link", magicLink } }
        };

        await _emailsApi.SendTransacEmailAsync(email);
    }
} 