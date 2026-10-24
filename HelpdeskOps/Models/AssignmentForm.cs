using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HelpdeskOps.Models.Enums;

namespace HelpdeskOps.Models;

// Digital zimmet (asset handover) record, opened whenever a device is assigned to someone and closed when it is taken back.
// Device and person details are copied when the form is opened, so the record and its PDF stay the same
// even if the device is edited or deleted later.
public class AssignmentForm
{
    public int Id { get; set; }

    // Null once the device itself has been deleted; the copied details below still describe it
    public int? DeviceId { get; set; }
    public Device? Device { get; set; }

    // The person the device was handed over to
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }

    // The IT staff member who made the assignment; null for forms created by the system (e.g. demo data)
    public string? IssuedById { get; set; }
    public ApplicationUser? IssuedBy { get; set; }

    public required string Hostname { get; set; }

    public required string SerialNumber { get; set; }

    public string? BrandModel { get; set; }

    [StringLength(50)]
    public required string DeviceTypeName { get; set; }

    public required string EmployeeName { get; set; }

    public string? Department { get; set; }

    public string? IssuedByName { get; set; }

    [Display(Name = "Teslim Tarihi")]
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    // Set when the employee confirms in "Cihazlarım" that they received the device
    [Display(Name = "Onay Tarihi")]
    public DateTime? AcceptedAt { get; set; }

    // PNG of the signature the employee draws when confirming receipt
    public byte[]? AcceptanceSignature { get; set; }

    // Set when the device is checked in by IT, assigned to someone else, taken back to storage, or deleted
    [Display(Name = "İade Tarihi")]
    public DateTime? ReturnedAt { get; set; }

    // Return request the employee sends from "Cihazlarım", signed by them
    [Display(Name = "İade Talebi Tarihi")]
    public DateTime? ReturnRequestedAt { get; set; }

    [Display(Name = "İade Sebebi")]
    public ReturnReason? ReturnReason { get; set; }

    [StringLength(500)]
    [Display(Name = "Çalışanın Açıklaması")]
    public string? ReturnNote { get; set; }

    public byte[]? ReturnSignature { get; set; }

    // Filled in by the IT staff member who checks the device in; the name is copied like the issuer's
    [Display(Name = "Kontrol Sonucu")]
    public DeviceCondition? ReturnCondition { get; set; }

    [StringLength(500)]
    [Display(Name = "Kontrol Notu")]
    public string? ReturnCheckNote { get; set; }

    public string? ReceivedById { get; set; }
    public ApplicationUser? ReceivedBy { get; set; }

    public string? ReceivedByName { get; set; }

    public byte[]? ReceiverSignature { get; set; }

    // "ZF-2026-00012"
    [NotMapped]
    public string FormNumber => $"ZF-{IssuedAt.ToTurkeyTime():yyyy}-{Id:D5}";
}
