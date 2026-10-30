using HelpdeskOps.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.CompanyDirectory;

// Puts every password decision in one place. An account linked to the company directory has its password there, so
// Identity's sign-in keeps its lockout and two-factor steps, but the check itself goes to the directory, and nothing
// can give the account a separate password here. Turning the directory off in the settings brings local passwords
// back, which is the way in if the directory is ever lost.
public class DirectoryUserManager : UserManager<ApplicationUser>
{
    private readonly DirectoryOptions _directory;
    private readonly IDirectoryPasswordChecker _passwordChecker;

    public DirectoryUserManager(IUserStore<ApplicationUser> store, IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<ApplicationUser> passwordHasher, IEnumerable<IUserValidator<ApplicationUser>> userValidators,
        IEnumerable<IPasswordValidator<ApplicationUser>> passwordValidators, ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<ApplicationUser>> logger,
        IOptions<DirectoryOptions> directory, IDirectoryPasswordChecker passwordChecker)
        : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
    {
        _directory = directory.Value;
        _passwordChecker = passwordChecker;
    }

    public bool IsManaged(ApplicationUser user) => _directory.IsEnabled && user.DirectoryId != null;

    public override async Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
    {
        if (!IsManaged(user))
        {
            return await base.CheckPasswordAsync(user, password);
        }

        // Entra accounts sign in through Microsoft, so this form never accepts them
        return _directory.Provider == DirectoryProvider.ActiveDirectory
            && await _passwordChecker.CheckPasswordAsync(user.Email!, password, CancellationToken);
    }

    public override Task<IdentityResult> AddPasswordAsync(ApplicationUser user, string password) =>
        IsManaged(user) ? Refuse() : base.AddPasswordAsync(user, password);

    public override Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword) =>
        IsManaged(user) ? Refuse() : base.ChangePasswordAsync(user, currentPassword, newPassword);

    public override Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string token, string newPassword) =>
        IsManaged(user) ? Refuse() : base.ResetPasswordAsync(user, token, newPassword);

    public override Task<IdentityResult> ChangeEmailAsync(ApplicationUser user, string newEmail, string token) =>
        IsManaged(user) ? Refuse() : base.ChangeEmailAsync(user, newEmail, token);

    private static Task<IdentityResult> Refuse() => Task.FromResult(IdentityResult.Failed(new IdentityError
    {
        Code = "DirectoryManaged",
        Description = "Bu hesap şirket dizininden yönetiliyor; şifre ve e-posta orada değiştirilir."
    }));
}
