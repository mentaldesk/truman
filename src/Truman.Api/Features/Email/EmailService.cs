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
    private readonly ITransactionalEmailsApi _emailsApi;

    public EmailService(IOptions<EmailConfiguration> options, ITransactionalEmailsApi emailsApi)
    {
        _settings = options.Value.Brevo;
        _emailsApi = emailsApi;
    }

    public async Task SendMagicLinkEmailAsync(string toEmail, string magicLink)
    {
        var email = new SendSmtpEmail
        {
            To = [new(email: toEmail)],
            TemplateId = _settings.MagicLinkTemplateId,
            Params = new Dictionary<string, object> { { "link", magicLink } }
        };

        await _emailsApi.SendTransacEmailAsync(email);
    }
}
