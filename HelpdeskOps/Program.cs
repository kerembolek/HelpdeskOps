using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Services.Email;
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<NotificationEmailInterceptor>();
builder.Services.AddSingleton<HelpdeskOps.Services.AuditInterceptor>();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>((services, options) =>
    options.UseSqlServer(connectionString)
        .AddInterceptors(
            services.GetRequiredService<HelpdeskOps.Services.AuditInterceptor>(),
            services.GetRequiredService<NotificationEmailInterceptor>()));

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
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Re-check sign-in cookies against the security stamp every minute, so a role change takes effect quickly
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.AddSingleton<HelpdeskOps.Services.AttachmentStorage>();

// KVKK: data controller details for the privacy notice, personal data export, anonymization, and daily retention
builder.Services.Configure<HelpdeskOps.Services.Kvkk.KvkkOptions>(builder.Configuration.GetSection(HelpdeskOps.Services.Kvkk.KvkkOptions.SectionName));
builder.Services.AddScoped<HelpdeskOps.Services.Kvkk.PersonalDataExporter>();
builder.Services.AddScoped<HelpdeskOps.Services.Kvkk.PersonalDataAnonymizer>();
builder.Services.AddScoped<HelpdeskOps.Services.Kvkk.RetentionCleaner>();
builder.Services.AddHostedService<HelpdeskOps.Services.Kvkk.RetentionService>();

// Keep data protection keys on disk so sign-in cookies survive app pool recycles on shared hosting
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    // Apply pending migrations on startup, since the hosted database is not reachable from development tools
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Employee", "ITAdmin", "Manager" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
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