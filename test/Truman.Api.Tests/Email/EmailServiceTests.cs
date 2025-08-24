using Microsoft.Extensions.Options;
using NSubstitute;
using Truman.Api.Features.Email;
using brevo_csharp.Model;
using brevo_csharp.Api;
using Task = System.Threading.Tasks.Task;

namespace Truman.Api.Tests.Email;

public class EmailServiceTests
{
    private static IOptions<EmailConfiguration> CreateOptions(long templateId = 123, string apiKey = "TEST_API_KEY")
        => Options.Create(new EmailConfiguration
        {
            Brevo = new BrevoSettings
            {
                ApiKey = apiKey,
                MagicLinkTemplateId = templateId
            }
        });

    [Fact]
    public async Task SendMagicLinkEmailAsync_SendsExpectedTemplateAndParams()
    {
        var options = CreateOptions(templateId: 9876);
        var api = Substitute.For<ITransactionalEmailsApi>();
        var service = new EmailService(options, api);

        const string to = "user@example.com";
        const string link = "https://app.example.com/magic/abc";

        SendSmtpEmail? captured = null;
        api.SendTransacEmailAsync(Arg.Do<SendSmtpEmail>(e => captured = e))
            .Returns(Task.FromResult(new CreateSmtpEmail()));

        await service.SendMagicLinkEmailAsync(to, link);

        await api.Received(1).SendTransacEmailAsync(Arg.Any<SendSmtpEmail>());
        Assert.NotNull(captured);
        Assert.Equal(9876, captured!.TemplateId);
        Assert.Single(captured.To);
        Assert.Equal(to, captured.To.First().Email);
        Assert.IsType<IDictionary<string, object>>(captured.Params, exactMatch: false);
        var paramsDict = (IDictionary<string, object>)captured.Params!;
        Assert.Equal(link, paramsDict["link"].ToString());
    }

    [Fact]
    public async Task SendMagicLinkEmailAsync_AllowsDifferentApiKeysPerInstance()
    {
        var options1 = CreateOptions(templateId: 1, apiKey: "KEY1");
        var options2 = CreateOptions(templateId: 2, apiKey: "KEY2");
        var api1 = Substitute.For<ITransactionalEmailsApi>();
        var api2 = Substitute.For<ITransactionalEmailsApi>();
        api1.SendTransacEmailAsync(Arg.Any<SendSmtpEmail>())
            .Returns(Task.FromResult(new CreateSmtpEmail()));
        api2.SendTransacEmailAsync(Arg.Any<SendSmtpEmail>())
            .Returns(Task.FromResult(new CreateSmtpEmail()));
        var svc1 = new EmailService(options1, api1);
        var svc2 = new EmailService(options2, api2);

        await svc1.SendMagicLinkEmailAsync("a@ex.com", "L1");
        await svc2.SendMagicLinkEmailAsync("b@ex.com", "L2");

        await api1.Received(1).SendTransacEmailAsync(Arg.Is<SendSmtpEmail>(e => e.TemplateId == 1));
        await api2.Received(1).SendTransacEmailAsync(Arg.Is<SendSmtpEmail>(e => e.TemplateId == 2));
    }

    [Fact]
    public async Task SendMagicLinkEmailAsync_DoesNotThrow_WhenParamsAreValid()
    {
        var options = CreateOptions();
        var api = Substitute.For<ITransactionalEmailsApi>();
        api.SendTransacEmailAsync(Arg.Any<SendSmtpEmail>())
            .Returns(Task.FromResult(new CreateSmtpEmail()));
        var service = new EmailService(options, api);
        var ex = await Record.ExceptionAsync(() => service.SendMagicLinkEmailAsync("user@ex.com", "https://x/y"));
        Assert.Null(ex);
    }
}
