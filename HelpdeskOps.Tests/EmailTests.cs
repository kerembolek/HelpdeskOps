using HelpdeskOps.Services;
using HelpdeskOps.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

public class EmailTests
{
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "HelpdeskOps";
        public string EnvironmentName { get; set; } = "Development";
    }

    [Fact]
    public void MessageWithButton_EncodesTextAndLink()
    {
        var html = EmailTemplate.MessageWithButton("<script>alert(1)</script>", "Aç", "https://site/a?x=1&y=\"2\"");

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("x=1&amp;y=&quot;2&quot;", html);
    }

    [Fact]
    public void Wrap_EncodesTheTitle()
    {
        var html = EmailTemplate.Wrap("<b>Başlık</b>", "<p>gövde</p>");

        Assert.Contains("&lt;b&gt;", html);
        Assert.Contains("<p>gövde</p>", html);
    }

    [Fact]
    public async Task QueuedEmailSender_QueuesOnlyWhenEmailIsEnabled()
    {
        var queue = new EmailQueue();
        await new QueuedEmailSender(queue, Options.Create(new EmailOptions { Mode = EmailMode.None }))
            .SendEmailAsync("a@b.com", "Kapalı", "x");
        await new QueuedEmailSender(queue, Options.Create(new EmailOptions { Mode = EmailMode.Pickup }))
            .SendEmailAsync("a@b.com", "Açık", "x");

        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var enumerator = queue.ReadAllAsync(cancel.Token).GetAsyncEnumerator(cancel.Token);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("Açık", enumerator.Current.Subject);
    }

    [Fact]
    public async Task PickupMode_WritesAnEmlFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "helpdeskops-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = Options.Create(new EmailOptions { Mode = EmailMode.Pickup, PickupDirectory = "outbox" });
            var service = new EmailDeliveryService(new EmailQueue(), options, new TestEnvironment { ContentRootPath = root },
                NullLogger<EmailDeliveryService>.Instance);

            await service.DeliverAsync(new EmailMessage("it@helpdeskops.com", "Şifre sıfırlama", "<p>Merhaba</p>"), CancellationToken.None);

            var file = Assert.Single(Directory.GetFiles(Path.Combine(root, "outbox"), "*.eml"));
            var text = await File.ReadAllTextAsync(file);
            Assert.Contains("To: it@helpdeskops.com", text);
            Assert.Contains("<p>Merhaba</p>", text);
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "outbox"), "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void OnlySmtpModeDeliversRealMail()
    {
        Assert.False(new EmailOptions { Mode = EmailMode.None }.IsEnabled);
        Assert.True(new EmailOptions { Mode = EmailMode.Pickup }.IsEnabled);
        Assert.False(new EmailOptions { Mode = EmailMode.Pickup }.DeliversRealMail);
        Assert.True(new EmailOptions { Mode = EmailMode.Smtp }.DeliversRealMail);
    }

    [Fact]
    public void TurkishIdentityErrors_AreTranslated()
    {
        var describer = new TurkishIdentityErrorDescriber();

        Assert.Equal("Şifre en az 8 karakter olmalıdır.", describer.PasswordTooShort(8).Description);
        Assert.Contains("zaten var", describer.DuplicateEmail("a@b.com").Description);
    }
}
