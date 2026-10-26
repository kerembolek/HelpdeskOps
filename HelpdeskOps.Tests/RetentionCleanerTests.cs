using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
using HelpdeskOps.Services.Kvkk;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

public class RetentionCleanerTests : KvkkTestDatabase
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "helpdeskops-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AttachmentStorage _storage;

    public RetentionCleanerTests()
    {
        _storage = new AttachmentStorage(new TestEnvironment { ContentRootPath = _root });
    }

    private RetentionCleaner Cleaner(RetentionOptions? options = null) =>
        new(Context, _storage, Options.Create(new KvkkOptions { Retention = options ?? new RetentionOptions() }));

    private async Task<TicketAttachment> AddAttachmentAsync(TicketStatus status, DateTime? resolvedAt)
    {
        var ticket = new Ticket
        {
            Title = "Ekli talep", Description = "-", RequesterId = "emp-1", Status = status, ResolvedAt = resolvedAt,
            CreatedAt = Now.AddYears(-3), Priority = TicketPriority.Low, Category = TicketCategory.Other
        };
        var attachment = new TicketAttachment
        {
            Ticket = ticket, FileName = "ekran.png", StoredName = Guid.NewGuid().ToString("N"), ContentType = "image/png", Size = 1
        };
        Context.TicketAttachments.Add(attachment);
        await Context.SaveChangesAsync();
        await File.WriteAllTextAsync(Path.Combine(_root, "App_Data", "attachments", attachment.StoredName), "x");
        return attachment;
    }

    [Fact]
    public async Task OldNotifications_AreDeleted_NewOnesStay()
    {
        Context.Notifications.Add(new Notification { UserId = "emp-1", Message = "Eski", Url = "/", CreatedAt = Now.AddDays(-181) });
        await Context.SaveChangesAsync();

        var counts = await Cleaner().RunAsync(Now);

        Assert.Equal(1, counts.Notifications);
        Assert.DoesNotContain(Context.Notifications, n => n.Message == "Eski");
        Assert.Equal(2, Context.Notifications.Count());
    }

    [Fact]
    public async Task Attachments_OfLongResolvedTickets_AreDeletedWithTheirFiles()
    {
        var old = await AddAttachmentAsync(TicketStatus.Resolved, Now.AddDays(-731));
        var recent = await AddAttachmentAsync(TicketStatus.Closed, Now.AddDays(-10));
        var open = await AddAttachmentAsync(TicketStatus.Open, null);

        Assert.Equal(1, (await Cleaner().CountDueAsync(Now)).Attachments);
        await Cleaner().RunAsync(Now);

        Context.ChangeTracker.Clear();
        Assert.Equal(new[] { recent.Id, open.Id }, Context.TicketAttachments.Select(a => a.Id).OrderBy(id => id).ToArray());
        Assert.Null(_storage.OpenRead(old));
        using var stillThere = _storage.OpenRead(recent);
        Assert.NotNull(stillThere);
    }

    [Fact]
    public async Task AuditLog_LosesOldIpAddressesAndVeryOldEntries()
    {
        Context.AuditLogs.Add(new AuditLog { CreatedAt = Now.AddDays(-800), IpAddress = "10.0.0.1", Action = AuditAction.Created, EntityType = "Device", EntityLabel = "eski" });
        Context.AuditLogs.Add(new AuditLog { CreatedAt = Now.AddDays(-4000), IpAddress = "10.0.0.2", Action = AuditAction.Created, EntityType = "Device", EntityLabel = "çok eski" });
        await Context.SaveChangesAsync();

        var counts = await Cleaner().RunAsync(Now);

        Assert.Equal(1, counts.AuditLogs);
        Assert.Equal(1, counts.AuditIpAddresses);
        Context.ChangeTracker.Clear();
        Assert.Null(Context.AuditLogs.Single(a => a.EntityLabel == "eski").IpAddress);
        Assert.DoesNotContain(Context.AuditLogs, a => a.EntityLabel == "çok eski");
        Assert.All(Context.AuditLogs.Where(a => a.CreatedAt > Now.AddDays(-1) && a.EntityType != RetentionCleaner.AuditEntityType),
            a => Assert.NotNull(a.IpAddress));
    }

    [Fact]
    public async Task ZimmetForms_LosePersonalDetailsTenYearsAfterReturn_Once()
    {
        var counts = await Cleaner().RunAsync(Now);

        Assert.Equal(1, counts.AssignmentForms);
        Context.ChangeTracker.Clear();
        var form = Context.AssignmentForms.Single();
        Assert.Equal("Silinmiş Kullanıcı", form.EmployeeName);
        Assert.Equal("Silinmiş Kullanıcı", form.IssuedByName);
        Assert.Null(form.ReceivedByName);
        Assert.Null(form.Department);
        Assert.Null(form.ReturnNote);
        Assert.Null(form.AcceptanceSignature);
        Assert.Null(form.ReturnSignature);
        Assert.Equal("SN-1", form.SerialNumber);

        Assert.Equal(0, (await Cleaner().RunAsync(Now)).AssignmentForms);
    }

    [Fact]
    public async Task ARuleSetToZero_IsOff()
    {
        Context.Notifications.Add(new Notification { UserId = "emp-1", Message = "Eski", Url = "/", CreatedAt = Now.AddYears(-5) });
        await Context.SaveChangesAsync();

        var counts = await Cleaner(new RetentionOptions { NotificationDays = 0, AssignmentFormDays = 0 }).RunAsync(Now);

        Assert.Equal(0, counts.Notifications);
        Assert.Equal(0, counts.AssignmentForms);
        Assert.Contains(Context.Notifications, n => n.Message == "Eski");
    }

    [Fact]
    public async Task ARunThatChangesSomething_LeavesOneSummaryInTheAuditLog()
    {
        await Cleaner().RunAsync(Now);
        var summary = Assert.Single(Context.AuditLogs.Where(a => a.EntityType == RetentionCleaner.AuditEntityType));
        Assert.Null(summary.UserId);
        Assert.Contains(AuditListViewModel.ChangesOf(summary), c => c.Field == "Zimmet formları" && c.New == "1 formdaki kişisel bilgiler silindi");

        await Cleaner().RunAsync(Now);
        Assert.Single(Context.AuditLogs.Where(a => a.EntityType == RetentionCleaner.AuditEntityType));
    }

    [Theory]
    [InlineData(730, "2 yıl")]
    [InlineData(3650, "10 yıl")]
    [InlineData(180, "6 ay")]
    [InlineData(45, "45 gün")]
    public void Periods_ReadNaturally(int days, string expected) => Assert.Equal(expected, RetentionPeriod.Format(days));

    public override void Dispose()
    {
        base.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "HelpdeskOps";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Testing";
    }
}
