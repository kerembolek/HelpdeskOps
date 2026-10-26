using HelpdeskOps.Services;

namespace HelpdeskOps.Tests;

public class TurkishIdentityErrorDescriberTests
{
    private readonly TurkishIdentityErrorDescriber _describer = new();

    [Fact]
    public void PasswordRules_KeepTheIdentityErrorCodes()
    {
        // The codes stay the same so Identity and the views can still tell the errors apart
        Assert.Equal("PasswordRequiresDigit", _describer.PasswordRequiresDigit().Code);
        Assert.Equal("PasswordRequiresUpper", _describer.PasswordRequiresUpper().Code);
        Assert.Equal("PasswordMismatch", _describer.PasswordMismatch().Code);
    }

    [Fact]
    public void InvalidToken_ExplainsThatTheLinkExpired()
    {
        Assert.Equal("Bağlantı geçersiz ya da süresi dolmuş.", _describer.InvalidToken().Description);
    }

    [Fact]
    public void PasswordRequiresUniqueChars_IncludesTheCount()
    {
        Assert.Contains("3 farklı karakter", _describer.PasswordRequiresUniqueChars(3).Description);
    }
}
