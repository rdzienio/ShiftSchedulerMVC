using System.ComponentModel.DataAnnotations;

namespace ShiftSchedulerMVC.Models
{
    public class EditUserViewModel
    {
        public string Id { get; set; }

        [Required]
        public string UserName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Position { get; set; }

        public string ManagerId { get; set; }


        [Required]
        public string Role { get; set; }
    }
}
