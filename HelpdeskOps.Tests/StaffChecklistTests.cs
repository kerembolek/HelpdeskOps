using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
using HelpdeskOps.Services.Kvkk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

public class StaffChecklistRulesTests
{
    private static readonly ChecklistFacts Ready = new(HasRole: true, ReadPrivacyNotice: true, AssignedDevices: 2, UnsignedForms: 0,
        OpenForms: 2, ActiveAssignedTickets: 0, ReturnsWithoutSignature: 0);

    private static StaffChecklist WithItems(ChecklistType type, int done, int open)
    {
        var checklist = new StaffChecklist { Type = type, UserId = "u" };
        for (var i = 0; i < done + open; i++)
        {
            checklist.Items.Add(new ChecklistItem { Title = $"Adım {i}", DoneAt = i < done ? DateTime.UtcNow : null });
        }
        return checklist;
    }

    [Fact]
    public void Onboarding_PassesOnceDevicesAreSignedFor()
    {
        var checks = StaffChecklistRules.Checks(ChecklistType.Onboarding, Ready);

        Assert.All(checks, c => Assert.True(c.Passed));
        Assert.Null(StaffChecklistRules.CompletionBlocker(WithItems(ChecklistType.Onboarding, 3, 0), checks));
    }

    [Fact]
    public void Onboarding_WaitsForDevicesAndSignatures()
    {
        var noDevice = StaffChecklistRules.Checks(ChecklistType.Onboarding, Ready with { AssignedDevices = 0, OpenForms = 0 });
        Assert.False(noDevice.Single(c => c.Title == "Cihaz zimmetlendi").Passed);
        // With nothing assigned there is nothing signed either
        Assert.False(noDevice.Single(c => c.Title == "Zimmet formları imzalandı").Passed);

        var unsigned = StaffChecklistRules.Checks(ChecklistType.Onboarding, Ready with { UnsignedForms = 1 });
        Assert.Equal("1 form çalışanın onayını bekliyor (Cihazlarım).", unsigned.Single(c => !c.Passed).Detail);
        Assert.Equal("1 otomatik kontrol henüz geçmedi.", StaffChecklistRules.CompletionBlocker(WithItems(ChecklistType.Onboarding, 1, 0), unsigned));
    }

    [Fact]
    public void Offboarding_NeedsDevicesFormsAndTicketsCleared()
    {
        var leaving = Ready with { AssignedDevices = 1, OpenForms = 1, ActiveAssignedTickets = 3 };
        var checks = StaffChecklistRules.Checks(ChecklistType.Offboarding, leaving);

        Assert.Equal(3, checks.Count(c => c.Required && !c.Passed));
        Assert.Equal("3 aktif talebi başka birine atayın.", checks.Single(c => c.Title == "Üzerinde aktif talep yok").Detail);
    }

    [Fact]
    public void Offboarding_UnsignedReturnsOnlyWarn()
    {
        var checks = StaffChecklistRules.Checks(ChecklistType.Offboarding,
            Ready with { AssignedDevices = 0, OpenForms = 0, ReturnsWithoutSignature = 1 });

        var warning = checks.Single(c => !c.Passed);
        Assert.False(warning.Required);
        Assert.Null(StaffChecklistRules.CompletionBlocker(WithItems(ChecklistType.Offboarding, 2, 0), checks));
    }

    [Fact]
    public void CompletionBlocker_CountsOpenStepsAndFinishedLists()
    {
        var checks = StaffChecklistRules.Checks(ChecklistType.Onboarding, Ready);
        Assert.Equal("2 adım henüz işaretlenmedi.", StaffChecklistRules.CompletionBlocker(WithItems(ChecklistType.Onboarding, 1, 2), checks));

        var done = WithItems(ChecklistType.Onboarding, 1, 0);
        done.CompletedAt = DateTime.UtcNow;
        Assert.Equal("Bu liste zaten tamamlandı.", StaffChecklistRules.CompletionBlocker(done, checks));
    }

    [Theory]
    [InlineData(ChecklistType.Onboarding)]
    [InlineData(ChecklistType.Offboarding)]
    public void DefaultItems_AreFilledInAndShort(ChecklistType type)
    {
        var items = StaffChecklistRules.DefaultItems(type);
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.InRange(i.Length, 1, 200));
    }

    [Fact]
    public void IsOverdue_OnlyForOpenListsPastTheirDate()
    {
        var today = new DateTime(2026, 10, 29);
        var checklist = new StaffChecklist { UserId = "u", TargetDate = today.AddDays(-1) };
        Assert.True(checklist.IsOverdue(today));

        checklist.CompletedAt = DateTime.UtcNow;
        Assert.False(checklist.IsOverdue(today));
        Assert.False(new StaffChecklist { UserId = "u", TargetDate = today }.IsOverdue(today));
    }
}

// Reads the facts from the shared KVKK test database: Onur (emp-1) is an employee with one zimmet form returned long ago
public class StaffChecklistServiceTests : KvkkTestDatabase
{
    private StaffChecklistService Service() => new(Context, Options.Create(new KvkkOptions()));

    private async Task<StaffChecklist> AddChecklistAsync(ChecklistType type, string userId = "emp-1", bool itemsDone = true)
    {
        var checklist = new StaffChecklist { Type = type, UserId = userId, TargetDate = DateTime.Today, CreatedAt = DateTime.UtcNow.AddDays(-1) };
        checklist.Items.Add(new ChecklistItem { Title = "Kart teslim alındı", DoneAt = itemsDone ? DateTime.UtcNow : null });
        Context.StaffChecklists.Add(checklist);
        await Context.SaveChangesAsync();
        return checklist;
    }

    [Fact]
    public async Task Facts_ComeFromRolesDevicesFormsAndTickets()
    {
        Context.Devices.Add(new Device
        {
            Hostname = "PC-02", IpAddress = "10.0.0.6", SerialNumber = "SN-2", DeviceType = DeviceType.Laptop,
            Status = DeviceStatus.Active, AssignedUserId = "emp-1"
        });
        Context.AssignmentForms.Add(new AssignmentForm
        {
            UserId = "emp-1", Hostname = "PC-02", SerialNumber = "SN-2", DeviceTypeName = "Dizüstü Bilgisayar", EmployeeName = "Onur Güçlü"
        });
        await Context.SaveChangesAsync();

        var facts = await Service().FactsAsync(await AddChecklistAsync(ChecklistType.Onboarding));

        Assert.True(facts.HasRole);
        Assert.False(facts.ReadPrivacyNotice);
        Assert.Equal(1, facts.AssignedDevices);
        Assert.Equal(1, facts.UnsignedForms);
        Assert.Equal(1, facts.OpenForms);
        // The old return happened before this checklist was opened
        Assert.Equal(0, facts.ReturnsWithoutSignature);
    }

    [Fact]
    public async Task CompleteOffboarding_ClosesTheAccount()
    {
        var checklist = await AddChecklistAsync(ChecklistType.Offboarding);

        Assert.Null(await Service().CompleteAsync(checklist, "it-1"));
        Context.ChangeTracker.Clear();

        var user = await Context.Users.SingleAsync(u => u.Id == "emp-1");
        Assert.Equal(DateTimeOffset.MaxValue, user.LockoutEnd);
        Assert.True(user.LockoutEnabled);
        var saved = await Context.StaffChecklists.SingleAsync();
        Assert.NotNull(saved.CompletedAt);
        Assert.Equal("it-1", saved.CompletedById);
    }

    [Fact]
    public async Task Complete_RefusesOpenStepsAndOwnAccount()
    {
        var open = await AddChecklistAsync(ChecklistType.Offboarding, itemsDone: false);
        Assert.Equal("1 adım henüz işaretlenmedi.", await Service().CompleteAsync(open, "it-1"));

        var own = await AddChecklistAsync(ChecklistType.Offboarding, userId: "it-1");
        Assert.Equal("Kendi hesabınızı kapatamazsınız.", await Service().CompleteAsync(own, "it-1"));

        Context.ChangeTracker.Clear();
        Assert.Null((await Context.Users.SingleAsync(u => u.Id == "emp-1")).LockoutEnd);
        Assert.All(Context.StaffChecklists, c => Assert.Null(c.CompletedAt));
    }
}
