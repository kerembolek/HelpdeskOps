using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HelpdeskOps.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required(ErrorMessage = "Ad zorunludur.")]
        [Display(Name = "Ad")]
        public required string FirstName { get; set; }

        [Required(ErrorMessage = "Soyad zorunludur.")]
        [Display(Name = "Soyad")]
        public required string LastName { get; set; }

        [Display(Name = "Departman")]
        public string? Department { get; set; }

        // The privacy notice version the user last confirmed reading, and when; proof that they were informed under KVKK
        [StringLength(20)]
        [Display(Name = "Aydınlatma Metni Sürümü")]
        public string? PrivacyNoticeVersion { get; set; }

        [Display(Name = "Aydınlatma Metni Okunma Tarihi")]
        public DateTime? PrivacyNoticeReadAt { get; set; }

        // Set when IT anonymizes a former employee for KVKK; the account stays so their records keep their links
        [Display(Name = "Anonimleştirilme Tarihi")]
        public DateTime? AnonymizedAt { get; set; }

        public ICollection<Device> AssignedDevices { get; set; } = new List<Device>();
        public ICollection<Ticket> RequestedTickets { get; set; } = new List<Ticket>();
        public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    }
}
