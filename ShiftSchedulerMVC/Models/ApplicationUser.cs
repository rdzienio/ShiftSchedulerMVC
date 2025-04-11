using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftSchedulerMVC.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        public string Position { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public virtual ICollection<ApplicationUser>? Employees { get; set; }

        public string? ManagerId { get; set; }

        public string FullName => $"{FirstName} {LastName}";


        [ForeignKey("ManagerId")]
        public virtual ApplicationUser? Manager { get; set; }
    }
}
