using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services;
using HelpdeskOps.Services.Kvkk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Tests;

public class SoftwareLicenseTests
{
    private static readonly DateTime Today = new(2026, 10, 29);

    [Theory]
    [InlineData(null, WarrantyState.Unknown)]
    [InlineData(-1, WarrantyState.Expired)]
    [InlineData(0, WarrantyState.ExpiringSoon)]
    [InlineData(30, WarrantyState.ExpiringSoon)]
    [InlineData(31, WarrantyState.Active)]
    public void RenewalState_FollowsTheDate(int? daysLeft, WarrantyState expected)
    {
        var license = new SoftwareLicense { Name = "Test", RenewalDate = daysLeft is int days ? Today.AddDays(days) : null };

        Assert.Equal(expected, license.GetRenewalState(Today));
    }

    [Fact]
    public void Offboarding_WaitsForLicensesToComeBack()
    {
        var facts = new ChecklistFacts(HasRole: true, ReadPrivacyNotice: true, AssignedDevices: 0, UnsignedForms: 0, OpenForms: 0,
            ActiveAssignedTickets: 0, ReturnsWithoutSignature: 0, AssignedLicenses: 2);

        var check = StaffChecklistRules.Checks(ChecklistType.Offboarding, facts).Single(c => c.Title == "Lisanslar geri alındı");
        Assert.False(check.Passed);
        Assert.True(check.Required);
        Assert.Equal("2 lisans hâlâ bu kişiye atanmış (Lisanslar).", check.Detail);
        // Now an automatic check, so it is no longer a manual step
        Assert.DoesNotContain("Lisanslar geri alındı", StaffChecklistRules.DefaultItems(ChecklistType.Offboarding));
    }
}

public class LicenseDatabaseTests : KvkkTestDatabase
{
    private async Task<SoftwareLicense> AddLicenseAsync()
    {
        var license = new SoftwareLicense { Name = "Microsoft 365", Seats = 2 };
        license.Assignments.Add(new LicenseAssignment { UserId = "emp-1" });
        Context.SoftwareLicenses.Add(license);
        await Context.SaveChangesAsync();
        return license;
    }

    [Fact]
    public async Task OffboardingFacts_CountTheSeatsAPersonHolds()
    {
        await AddLicenseAsync();
        var checklist = new StaffChecklist { Type = ChecklistType.Offboarding, UserId = "emp-1", TargetDate = DateTime.Today };

        var facts = await new StaffChecklistService(Context, Options.Create(new KvkkOptions())).FactsAsync(checklist);

        Assert.Equal(1, facts.AssignedLicenses);
    }

    [Fact]
    public async Task OnePersonHoldsOneSeatOfALicense()
    {
        var license = await AddLicenseAsync();
        Context.LicenseAssignments.Add(new LicenseAssignment { SoftwareLicenseId = license.Id, UserId = "emp-1" });

        await Assert.ThrowsAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingALicense_RemovesItsSeats()
    {
        var license = await AddLicenseAsync();
        Context.SoftwareLicenses.Remove(license);
        await Context.SaveChangesAsync();

        Assert.Empty(Context.LicenseAssignments);
        Assert.True(await Context.Users.AnyAsync(u => u.Id == "emp-1"));
    }

    [Fact]
    public async Task Export_ListsThePersonsLicenses()
    {
        await AddLicenseAsync();

        var json = System.Text.Encoding.UTF8.GetString((await new PersonalDataExporter(Context).ExportAsync("emp-1"))!);

        Assert.Contains("Yazılım Lisanslarınız", json);
        Assert.Contains("Microsoft 365", json);
    }
}
