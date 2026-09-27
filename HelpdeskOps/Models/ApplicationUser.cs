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


        public ICollection<Device> AssignedDevices { get; set; } = new List<Device>();
        public ICollection<Ticket> RequestedTickets { get; set; } = new List<Ticket>();
        public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    }
}
