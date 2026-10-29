using System.ComponentModel.DataAnnotations;

namespace HelpdeskOps.Models;

// The fields IT fills in when opening a checklist; everything else is set on the server
public class StaffChecklistCreateViewModel
{
    [Required(ErrorMessage = "Kişi seçilmelidir.")]
    [Display(Name = "Kişi")]
    public string? UserId { get; set; }

    [Display(Name = "Süreç")]
    public ChecklistType Type { get; set; }

    [Required(ErrorMessage = "Hedef tarih zorunludur.")]
    [DataType(DataType.Date)]
    [Display(Name = "Hedef Tarih")]
    public DateTime? TargetDate { get; set; }

    [StringLength(500, ErrorMessage = "Not en fazla 500 karakter olabilir.")]
    [Display(Name = "Not")]
    public string? Note { get; set; }
}

public record StaffChecklistRow(int Id, ChecklistType Type, string UserName, string? Department, DateTime TargetDate,
    DateTime CreatedAt, DateTime? CompletedAt, int DoneItems, int TotalItems);

public class StaffChecklistDetailsViewModel
{
    public required StaffChecklist Checklist { get; init; }
    public required IReadOnlyList<ChecklistCheck> Checks { get; init; }
    public required IReadOnlyList<Device> Devices { get; init; }
    public required IReadOnlyList<AssignmentForm> Forms { get; init; }
    public int ActiveAssignedTickets { get; init; }
    public string? CompletionBlocker { get; init; }
}
