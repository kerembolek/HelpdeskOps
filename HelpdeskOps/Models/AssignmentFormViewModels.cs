using System.ComponentModel.DataAnnotations;
using HelpdeskOps.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HelpdeskOps.Models;

// The employee signs to confirm they received the device
public class AcceptAssignmentViewModel
{
    // Loaded on the server, never bound from the form
    [BindNever, ValidateNever]
    public AssignmentForm Form { get; set; } = null!;

    [Required(ErrorMessage = "Lütfen imzanızı atın.")]
    public string? Signature { get; set; }
}

// The employee asks to hand the device back and signs the request
public class ReturnRequestViewModel
{
    [BindNever, ValidateNever]
    public AssignmentForm Form { get; set; } = null!;

    [Required(ErrorMessage = "İade sebebini seçin.")]
    [Display(Name = "İade Sebebi")]
    public ReturnReason? Reason { get; set; }

    [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
    [Display(Name = "Açıklama (isteğe bağlı)")]
    public string? Note { get; set; }

    [Required(ErrorMessage = "Lütfen imzanızı atın.")]
    public string? Signature { get; set; }
}

// IT checks the returned device, records what it found, and signs for receiving it
public class CheckInViewModel
{
    [BindNever, ValidateNever]
    public AssignmentForm Form { get; set; } = null!;

    [Required(ErrorMessage = "Cihazın durumunu seçin.")]
    [Display(Name = "Kontrol Sonucu")]
    public DeviceCondition? Condition { get; set; }

    [StringLength(500, ErrorMessage = "Not en fazla 500 karakter olabilir.")]
    [Display(Name = "Kontrol Notu")]
    public string? Note { get; set; }

    [Display(Name = "Cihazın Yeni Durumu")]
    public DeviceStatus NewStatus { get; set; } = DeviceStatus.InStorage;

    [Required(ErrorMessage = "Lütfen imzanızı atın.")]
    public string? Signature { get; set; }
}
