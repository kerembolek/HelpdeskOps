using System.Security.Claims;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
using HelpdeskOps.Services.Kvkk;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Tests;

public class PersonalDataAnonymizerTests : KvkkTestDatabase
{
    private static readonly HttpContextAccessor Http = new();

    // The audit interceptor runs too, as it does in the app, so the test sees the entry the anonymization writes
    public PersonalDataAnonymizerTests() : base(new AuditInterceptor(Http))
    {
    }

    private async Task<ApplicationUser> AnonymizeOnurAsync()
    {
        Http.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "it-1"),
                new Claim(ClaimTypes.Name, "it@x.com")
            }, "test"))
        };

        var user = await Context.Users.SingleAsync(u => u.Id == "emp-1");
        Assert.Null(await new PersonalDataAnonymizer(Context).CheckAsync(user, "it-1"));
        await new PersonalDataAnonymizer(Context).AnonymizeAsync(user);
        Context.ChangeTracker.Clear();
        return await Context.Users.SingleAsync(u => u.Id == "emp-1");
    }

    [Fact]
    public async Task Anonymize_ErasesTheIdentityAndLocksTheAccount()
    {
        var user = await AnonymizeOnurAsync();

        Assert.Equal("Silinmiş Kullanıcı", $"{user.FirstName} {user.LastName}");
        Assert.EndsWith("@anonim.invalid", user.Email);
        Assert.Equal(user.Email, user.UserName);
        Assert.Equal(user.Email!.ToUpperInvariant(), user.NormalizedEmail);
        Assert.Null(user.PhoneNumber);
        Assert.Null(user.Department);
        Assert.Null(user.PasswordHash);
        Assert.Equal(DateTimeOffset.MaxValue, user.LockoutEnd);
        Assert.NotNull(user.AnonymizedAt);
        Assert.Empty(Context.UserRoles.Where(r => r.UserId == "emp-1"));
        Assert.Empty(Context.Notifications.Where(n => n.UserId == "emp-1"));
        Assert.Single(Context.Notifications.Where(n => n.UserId == "it-1"));
    }

    [Fact]
    public async Task Anonymize_KeepsTheCompanyRecords()
    {
        await AnonymizeOnurAsync();

        Assert.Equal("emp-1", Context.Tickets.Single(t => t.Title == "Yazıcı çalışmıyor").RequesterId);
        Assert.Single(Context.TicketComments.Where(c => c.AuthorId == "emp-1"));
        Assert.Equal("Silinmiş Kullanıcı", Context.TicketActivities.Single().NewValue);

        // Zimmet forms prove a handover; the retention rules anonymize them later
        var form = Context.AssignmentForms.Single();
        Assert.Equal("Onur Güçlü", form.EmployeeName);
        Assert.NotNull(form.AcceptanceSignature);
    }

    [Fact]
    public async Task Anonymize_RemovesThePersonFromTheAuditLog()
    {
        var user = await AnonymizeOnurAsync();
        var logs = Context.AuditLogs.ToList();

        var own = logs.Single(a => a.UserId == "emp-1");
        Assert.Null(own.IpAddress);
        Assert.Equal(user.Email, own.UserName);

        // The change IT made to the account before: who and when stay, the values go
        var accountChange = logs.Single(a => a.EntityType == "ApplicationUser" && a.IpAddress == "10.0.0.7");
        Assert.Null(accountChange.Changes);
        Assert.Equal(user.Email, accountChange.EntityLabel);

        Assert.DoesNotContain(logs, a => (a.Changes ?? "").Contains("Onur") || (a.Changes ?? "").Contains("Muhasebe")
            || (a.EntityLabel ?? "").Contains("onur@x.com") || (a.UserName ?? "").Contains("onur@x.com"));
        Assert.Contains("Silinmi", logs.Single(a => a.EntityType == "Device").Changes);
    }

    [Fact]
    public async Task Anonymize_WritesAnAuditEntryWithoutTheOldValues()
    {
        await AnonymizeOnurAsync();

        var entry = Context.AuditLogs.Single(a => a.EntityType == "ApplicationUser" && a.UserId == "it-1" && a.Changes != null
            && a.Changes.Contains("Anonimle"));
        Assert.DoesNotContain("Onur", entry.Changes);
        Assert.Single(HelpdeskOps.Models.AuditListViewModel.ChangesOf(entry));
    }

    [Fact]
    public async Task Check_BlocksSelfDevicesAndActiveTickets()
    {
        var anonymizer = new PersonalDataAnonymizer(Context);
        var it = await Context.Users.SingleAsync(u => u.Id == "it-1");
        Assert.Equal("Kendi hesabınızı anonimleştiremezsiniz.", await anonymizer.CheckAsync(it, "it-1"));

        Context.Tickets.Add(new Ticket
        {
            Title = "Açık talep", Description = "-", RequesterId = "emp-1", AssignedToId = "it-1",
            Priority = TicketPriority.Low, Category = TicketCategory.Other
        });
        Context.Devices.Add(new Device
        {
            Hostname = "PC-02", IpAddress = "10.0.0.6", SerialNumber = "SN-2", DeviceType = DeviceType.Laptop,
            Status = DeviceStatus.Active, AssignedUserId = "emp-1"
        });
        await Context.SaveChangesAsync();

        Assert.Equal("Önce üzerindeki 1 aktif talebi başka birine atayın.", await anonymizer.CheckAsync(it, "someone-else"));
        var onur = await Context.Users.SingleAsync(u => u.Id == "emp-1");
        Assert.Equal("Önce üzerindeki 1 cihazı geri alın.", await anonymizer.CheckAsync(onur, "it-1"));
    }
}
