using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HelpdeskOps.Models;

namespace HelpdeskOps.Data
{
    // Normal DbContext yerine IdentityDbContext kullanıyoruz ki kullanıcı tabloları (Users, Roles vb.) otomatik oluşsun
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Veritabanındaki diğer tablolarımız
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<TicketComment> TicketComments { get; set; }
        public DbSet<TicketActivity> TicketActivities { get; set; }
        public DbSet<DeviceEvent> DeviceEvents { get; set; }
        public DbSet<SolutionArticle> SolutionArticles { get; set; }
        public DbSet<AssignmentForm> AssignmentForms { get; set; }
        public DbSet<TicketAttachment> TicketAttachments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Ticket>()
                .HasOne(t => t.Requester)
                .WithMany(u => u.RequestedTickets)
                .HasForeignKey(t => t.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Ticket>()
                .HasOne(t => t.AssignedTo)
                .WithMany(u => u.AssignedTickets)
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Device>()
                .HasOne(d => d.AssignedUser)
                .WithMany(u => u.AssignedDevices)
                .HasForeignKey(d => d.AssignedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Self-reference: SQL Server rejects SET NULL here, so DevicesController clears the links before deleting a switch
            builder.Entity<Device>()
                .HasOne(d => d.Switch)
                .WithMany(s => s.ConnectedDevices)
                .HasForeignKey(d => d.SwitchId)
                .OnDelete(DeleteBehavior.Restrict);

            // One device per switch port
            builder.Entity<Device>()
                .HasIndex(d => new { d.SwitchId, d.SwitchPort })
                .IsUnique()
                .HasFilter("[SwitchId] IS NOT NULL AND [SwitchPort] IS NOT NULL");

            // Comments and history belong to the ticket and go away with it
            builder.Entity<TicketComment>()
                .HasOne(c => c.Ticket)
                .WithMany(t => t.Comments)
                .HasForeignKey(c => c.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TicketComment>()
                .HasOne(c => c.Author)
                .WithMany()
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TicketActivity>()
                .HasOne(a => a.Ticket)
                .WithMany(t => t.Activities)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            // A device's lifecycle history goes away with the device
            builder.Entity<DeviceEvent>()
                .HasOne(e => e.Device)
                .WithMany(d => d.Events)
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DeviceEvent>()
                .HasOne(e => e.PerformedBy)
                .WithMany()
                .HasForeignKey(e => e.PerformedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TicketActivity>()
                .HasOne(a => a.Actor)
                .WithMany()
                .HasForeignKey(a => a.ActorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Attachment rows go away with the ticket; TicketsController deletes the files on disk
            builder.Entity<TicketAttachment>()
                .HasOne(a => a.Ticket)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TicketAttachment>()
                .HasOne(a => a.UploadedBy)
                .WithMany()
                .HasForeignKey(a => a.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Zimmet forms are records: they outlive the device and keep their copied details
            builder.Entity<AssignmentForm>()
                .HasOne(f => f.Device)
                .WithMany()
                .HasForeignKey(f => f.DeviceId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<AssignmentForm>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AssignmentForm>()
                .HasOne(f => f.IssuedBy)
                .WithMany()
                .HasForeignKey(f => f.IssuedById)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}