using HelpdeskOps.Services.CompanyDirectory;

namespace HelpdeskOps.Tests;

public class DirectorySyncPlannerTests
{
    private static DirectoryUser Person(string id, string email, bool enabled = true, string first = "Ada", string last = "Kaya", string? department = "Satış") =>
        new(id, email, first, last, department, enabled);

    private static LocalAccount Account(string id, string? directoryId, string email, bool closed = false, string first = "Ada", string last = "Kaya", string? department = "Satış") =>
        new(id, directoryId, email, first, last, department, closed);

    [Fact]
    public void EmptyDirectoryChangesNothing()
    {
        var plan = DirectorySyncPlanner.Plan(new[] { Account("1", "d1", "a@x.com") }, Array.Empty<DirectoryUser>());

        Assert.NotNull(plan.AbortReason);
        Assert.Empty(plan.ToClose);
    }

    [Fact]
    public void NewEnabledPersonIsCreatedButDisabledOneIsNot()
    {
        var plan = DirectorySyncPlanner.Plan(Array.Empty<LocalAccount>(),
            new[] { Person("d1", "a@x.com"), Person("d2", "b@x.com", enabled: false) });

        Assert.Equal("d1", Assert.Single(plan.ToCreate).Id);
    }

    [Fact]
    public void UnchangedAccountIsLeftAlone()
    {
        var plan = DirectorySyncPlanner.Plan(new[] { Account("1", "d1", "A@X.com") }, new[] { Person("d1", "a@x.com") });

        Assert.Empty(plan.ToCreate);
        Assert.Empty(plan.ToUpdate);
        Assert.Empty(plan.ToClose);
    }

    [Fact]
    public void RenamedPersonIsFoundByDirectoryIdNotEmail()
    {
        var plan = DirectorySyncPlanner.Plan(new[] { Account("1", "d1", "eski@x.com") },
            new[] { Person("d1", "yeni@x.com", last: "Yılmaz") });

        Assert.Equal("1", Assert.Single(plan.ToUpdate).Account.Id);
        Assert.Empty(plan.ToCreate);
    }

    [Fact]
    public void UnlinkedAccountWithSameEmailGetsLinked()
    {
        var plan = DirectorySyncPlanner.Plan(new[] { Account("1", null, "a@x.com") }, new[] { Person("d1", "a@x.com") });

        var update = Assert.Single(plan.ToUpdate);
        Assert.Equal("d1", update.Person.Id);
        Assert.Empty(plan.ToCreate);
    }

    [Fact]
    public void AddressReusedByAnotherLivePersonDoesNotTakeOverTheAccount()
    {
        // d1 still exists in the directory under its old address, and d2 now claims that address too
        var accounts = new[] { Account("1", "d1", "a@x.com") };
        var people = new[] { Person("d2", "a@x.com"), Person("d1", "a2@x.com") };

        var plan = DirectorySyncPlanner.Plan(accounts, people);

        Assert.Equal("1", Assert.Single(plan.ToUpdate).Account.Id);
        Assert.Equal("d1", plan.ToUpdate[0].Person.Id);
        Assert.Equal("d2", Assert.Single(plan.ToCreate).Id);
    }

    [Fact]
    public void AccountFromAPreviousDirectoryMovesToTheNewOne()
    {
        var plan = DirectorySyncPlanner.Plan(new[] { Account("1", "ad-guid", "a@x.com") }, new[] { Person("entra-oid", "a@x.com") });

        Assert.Equal("entra-oid", Assert.Single(plan.ToUpdate).Person.Id);
        Assert.Empty(plan.ToClose);
    }

    [Fact]
    public void DisabledPersonsOpenAccountIsClosed()
    {
        var plan = DirectorySyncPlanner.Plan(
            new[] { Account("1", "d1", "a@x.com"), Account("2", "d2", "b@x.com"), Account("3", "d3", "c@x.com") },
            new[] { Person("d1", "a@x.com", enabled: false), Person("d2", "b@x.com"), Person("d3", "c@x.com") });

        Assert.Equal("1", Assert.Single(plan.ToClose).Id);
    }

    [Fact]
    public void ClosedAccountIsNotReopenedOrClosedAgain()
    {
        var plan = DirectorySyncPlanner.Plan(new[] { Account("1", "d1", "a@x.com", closed: true) }, new[] { Person("d1", "a@x.com") });

        Assert.Empty(plan.ToClose);
        Assert.Empty(plan.ToUpdate);
    }

    [Fact]
    public void PersonDeletedFromDirectoryIsClosed()
    {
        var plan = DirectorySyncPlanner.Plan(
            new[] { Account("1", "d1", "a@x.com"), Account("2", "d2", "b@x.com"), Account("3", "d3", "c@x.com") },
            new[] { Person("d2", "b@x.com"), Person("d3", "c@x.com") });

        Assert.Equal("1", Assert.Single(plan.ToClose).Id);
    }

    [Fact]
    public void LocalAccountsOutsideTheDirectoryAreLeftAlone()
    {
        var plan = DirectorySyncPlanner.Plan(new[] { Account("1", null, "yerel@x.com") }, new[] { Person("d1", "a@x.com") });

        Assert.Empty(plan.ToClose);
    }

    [Fact]
    public void ClosingMostAccountsAtOnceStopsTheSync()
    {
        var accounts = Enumerable.Range(1, 5).Select(i => Account($"{i}", $"d{i}", $"u{i}@x.com")).ToArray();

        var plan = DirectorySyncPlanner.Plan(accounts, new[] { Person("d1", "u1@x.com") });

        Assert.Contains("kapatacaktı", plan.AbortReason);
    }

    [Fact]
    public void DuplicateDirectoryEntriesCreateOneAccount()
    {
        var plan = DirectorySyncPlanner.Plan(Array.Empty<LocalAccount>(), new[] { Person("d1", "a@x.com"), Person("d2", "A@x.com") });

        Assert.Single(plan.ToCreate);
    }
}
