using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Services.CompanyDirectory;
using HelpdeskOps.Services.Email;
using HelpdeskOps.Services.Teams;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;


// QuestPDF is free under its Community license for individuals and companies under $1M annual revenue
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// E-mail: queued by requests, sent in the background; the "Email" settings section picks the delivery mode
var emailOptions = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddSingleton<IEmailSender, QueuedEmailSender>();
builder.Services.AddHostedService<EmailDeliveryService>();
// Opening tickets by e-mail: the "Email:Inbound" settings point at the helpdesk mailbox
builder.Services.AddScoped<InboundEmailProcessor>();
builder.Services.AddHostedService<InboundEmailService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<NotificationEmailInterceptor>();
builder.Services.AddSingleton<HelpdeskOps.Services.AuditInterceptor>();
// SLA targets count working hours; the "Sla" settings section sets the working day and extra days off
builder.Services.Configure<SlaOptions>(builder.Configuration.GetSection(SlaOptions.SectionName));
builder.Services.AddSingleton<HelpdeskOps.Services.SlaDueInterceptor>();
// Teams: new tickets go to the IT channel through a Workflows webhook; off until "Teams:WebhookUrl" is set
var teamsOptions = builder.Configuration.GetSection(TeamsOptions.SectionName).Get<TeamsOptions>() ?? new TeamsOptions();
if (teamsOptions.Validate(builder.Environment.IsDevelopment()) is string teamsError)
{
    throw new InvalidOperationException(teamsError);
}
builder.Services.Configure<TeamsOptions>(builder.Configuration.GetSection(TeamsOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<TeamsQueue>();
builder.Services.AddHostedService<TeamsDeliveryService>();
builder.Services.AddSingleton<TicketTeamsInterceptor>();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>((services, options) =>
    options.UseSqlServer(connectionString)
        .AddInterceptors(
            services.GetRequiredService<HelpdeskOps.Services.SlaDueInterceptor>(),
            services.GetRequiredService<HelpdeskOps.Services.AuditInterceptor>(),
            services.GetRequiredService<NotificationEmailInterceptor>(),
            services.GetRequiredService<TicketTeamsInterceptor>()));

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        // New accounts must confirm their address only when real mail can reach them
        options.SignIn.RequireConfirmedAccount = emailOptions.DeliversRealMail;

        // Longer passwords with some variety; the existing digit, case, and symbol rules stay on
        options.Password.RequiredLength = 10;
        options.Password.RequiredUniqueChars = 4;

        // Five wrong passwords lock the account for 15 minutes (the read-only demo turns this off at sign-in)
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddErrorDescriber<HelpdeskOps.Services.TurkishIdentityErrorDescriber>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddUserManager<DirectoryUserManager>();

// Company directory: the "Directory" settings section picks Entra ID, Active Directory, or neither
var directoryOptions = builder.Configuration.GetSection(DirectoryOptions.SectionName).Get<DirectoryOptions>() ?? new DirectoryOptions();
if (directoryOptions.Validate(builder.Environment.IsDevelopment()) is string directoryError)
{
    throw new InvalidOperationException(directoryError);
}
builder.Services.Configure<DirectoryOptions>(builder.Configuration.GetSection(DirectoryOptions.SectionName));
builder.Services.AddSingleton<LdapDirectoryClient>();
builder.Services.AddSingleton<IDirectoryPasswordChecker>(services => services.GetRequiredService<LdapDirectoryClient>());
builder.Services.AddHttpClient<GraphDirectoryClient>();
builder.Services.AddScoped<IDirectoryClient>(services => directoryOptions.Provider == DirectoryProvider.Entra
    ? services.GetRequiredService<GraphDirectoryClient>()
    : services.GetRequiredService<LdapDirectoryClient>());
builder.Services.AddScoped<DirectorySync>();
builder.Services.AddHostedService<DirectorySyncService>();

if (directoryOptions.Provider == DirectoryProvider.Entra)
{
    // Microsoft checks the password and any MFA, then this app receives a signed token. The handler verifies the
    // signature, the issuer (this tenant only), and the audience (this app only).
    builder.Services.AddAuthentication().AddOpenIdConnect(EntraSignIn.Scheme, "Microsoft", options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.Authority = directoryOptions.Entra.Authority;
        options.ClientId = directoryOptions.Entra.ClientId;
        options.ClientSecret = directoryOptions.Entra.ClientSecret;
        options.ResponseType = "code";
        options.CallbackPath = "/signin-entra";
        // Keep the token's own claim names, such as "oid"
        options.MapInboundClaims = false;
        // Signing out here leaves the Microsoft session open, so on a shared computer the next person must be able to
        // pick their own account instead of landing in the previous one
        options.Prompt = "select_account";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        // A refused consent or a rejected token ends on the sign-in page with a message, not on an error page
        options.Events.OnRemoteFailure = context =>
        {
            context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>()
                .LogWarning(context.Failure, "Microsoft sign-in failed.");
            context.Response.Redirect("/Identity/Account/ExternalLogin?handler=Callback&remoteError=failed");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
}

// Re-check sign-in cookies against the security stamp every minute, so a role change takes effect quickly
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.AddSingleton<HelpdeskOps.Services.AttachmentStorage>();

// KVKK: data controller details for the privacy notice, personal data export, anonymization, and daily retention
builder.Services.Configure<HelpdeskOps.Services.Kvkk.KvkkOptions>(builder.Configuration.GetSection(HelpdeskOps.Services.Kvkk.KvkkOptions.SectionName));
builder.Services.AddScoped<HelpdeskOps.Services.Kvkk.PersonalDataExporter>();
builder.Services.AddScoped<HelpdeskOps.Services.Kvkk.PersonalDataAnonymizer>();
builder.Services.AddScoped<HelpdeskOps.Services.Kvkk.RetentionCleaner>();
builder.Services.AddScoped<HelpdeskOps.Services.StaffChecklistService>();
builder.Services.AddHostedService<HelpdeskOps.Services.Kvkk.RetentionService>();

// Keep data protection keys on disk so sign-in cookies survive app pool recycles on shared hosting
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

builder.Services.Configure<HelpdeskOps.Services.Inventory.InventoryOptions>(builder.Configuration.GetSection(HelpdeskOps.Services.Inventory.InventoryOptions.SectionName));
builder.Services.AddScoped<HelpdeskOps.Services.Inventory.InventoryImporter>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    // Apply pending migrations on startup, since the hosted database is not reachable from development tools
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<HelpdeskOps.Services.SlaDueInterceptor>()
        .BackfillAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Employee", "ITAdmin", "Manager" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
    // A fresh install gets its first IT staff member from the "InitialAdminEmail" setting
    var initialAdmin = await HelpdeskOps.Services.InitialAdmin.EnsureAsync(
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        app.Configuration[HelpdeskOps.Services.InitialAdmin.SettingName]);
    if (initialAdmin != null)
        app.Logger.LogWarning("{Message}", initialAdmin);

    // The live demo site turns seeding on with "SeedDemoData": true in its own appsettings.Production.json
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("SeedDemoData"))
        await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// The public live demo turns this on in its appsettings.Production.json so visitors cannot change anything
if (HelpdeskOps.Services.DemoReadOnlyMiddleware.IsEnabled(app.Configuration))
{
    app.UseMiddleware<HelpdeskOps.Services.DemoReadOnlyMiddleware>();
}
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();

app.Run();