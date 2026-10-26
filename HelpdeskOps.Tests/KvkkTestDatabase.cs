using HelpdeskOps.Data;
using HelpdeskOps.Models;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace HelpdeskOps.Tests;

// An in-memory SQLite database with two people who have tickets, comments, devices, zimmet forms, notifications,
// and audit entries, for the KVKK export, anonymization, and retention tests
public abstract class KvkkTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    protected readonly ApplicationDbContext Context;

    protected KvkkTestDatabase(params IInterceptor[] interceptors)
    {
        _connection.Open();
        Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection).AddInterceptors(interceptors).Options);
        Context.Database.EnsureCreated();

        Context.Roles.Add(new IdentityRole("Employee") { Id = "role-emp", NormalizedName = "EMPLOYEE" });
        Context.Users.Add(new ApplicationUser
        {
            Id = "emp-1", UserName = "onur@x.com", NormalizedUserName = "ONUR@X.COM", Email = "onur@x.com", NormalizedEmail = "ONUR@X.COM",
            FirstName = "Onur", LastName = "Güçlü", Department = "Muhasebe", PhoneNumber = "05550000000", PasswordHash = "hash"
        });
        Context.Users.Add(new ApplicationUser { Id = "it-1", UserName = "it@x.com", Email = "it@x.com", FirstName = "Bilgehan", LastName = "Sungur" });
        Context.UserRoles.Add(new IdentityUserRole<string> { UserId = "emp-1", RoleId = "role-emp" });

        var ticket = new Ticket
        {
            Title = "Yazıcı çalışmıyor", Description = "Onur Güçlü masasındaki yazıcı", RequesterId = "emp-1", AssignedToId = "it-1",
            Status = TicketStatus.Resolved, Priority = TicketPriority.Medium, Category = TicketCategory.Printer
        };
        Context.Tickets.Add(ticket);
        Context.Tickets.Add(new Ticket
        {
            Title = "BT'nin kendi talebi", Description = "-", RequesterId = "it-1",
            Priority = TicketPriority.Low, Category = TicketCategory.Other
        });
        Context.TicketComments.Add(new TicketComment { Ticket = ticket, AuthorId = "emp-1", Body = "Hâlâ yazmıyor" });
        Context.TicketActivities.Add(new TicketActivity
        {
            Ticket = ticket, ActorId = "it-1", Type = TicketActivityType.AssigneeChanged, OldValue = "Atanmadı", NewValue = "Onur Güçlü"
        });

        Context.AssignmentForms.Add(new AssignmentForm
        {
            UserId = "emp-1", Hostname = "PC-01", SerialNumber = "SN-1", DeviceTypeName = "Dizüstü Bilgisayar",
            EmployeeName = "Onur Güçlü", Department = "Muhasebe", IssuedByName = "Bilgehan Sungur",
            IssuedAt = DateTime.UtcNow.AddYears(-12), ReturnedAt = DateTime.UtcNow.AddYears(-11),
            AcceptanceSignature = new byte[] { 1 }, ReturnSignature = new byte[] { 2 }, ReturnNote = "Ekran çizik"
        });

        Context.Notifications.Add(new Notification { UserId = "emp-1", Message = "Talebiniz çözüldü", Url = "/Tickets/Details/1" });
        Context.Notifications.Add(new Notification { UserId = "it-1", Message = "Yeni talep", Url = "/Tickets/Details/2" });

        Context.AuditLogs.Add(new AuditLog
        {
            UserId = "emp-1", UserName = "onur@x.com", IpAddress = "10.0.0.9", Action = AuditAction.Created,
            EntityType = "Ticket", EntityId = "1", EntityLabel = "#1 Yazıcı çalışmıyor"
        });
        Context.AuditLogs.Add(new AuditLog
        {
            UserId = "it-1", UserName = "it@x.com", IpAddress = "10.0.0.7", Action = AuditAction.Updated,
            EntityType = "ApplicationUser", EntityId = "emp-1", EntityLabel = "onur@x.com",
            Changes = "[{\"Field\":\"Departman\",\"Old\":\"Satış\",\"New\":\"Muhasebe\"}]"
        });
        Context.AuditLogs.Add(new AuditLog
        {
            UserId = "it-1", UserName = "it@x.com", IpAddress = "10.0.0.7", Action = AuditAction.Updated,
            EntityType = "Device", EntityId = "1", EntityLabel = "PC-01",
            // Serialized the way AuditInterceptor does, with "ü" and "ç" escaped
            Changes = JsonSerializer.Serialize(new[] { new AuditChange("Zimmetli Kullanıcı", null, "Onur Güçlü") })
        });

        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    public virtual void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
