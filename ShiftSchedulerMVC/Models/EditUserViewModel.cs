using System.ComponentModel.DataAnnotations;

namespace ShiftSchedulerMVC.Models
{
    public class EditUserViewModel
    {
        public string Id { get; set; }

        [Required]
        [Display(Name = "Nazwa użytkownika")]
        public string UserName { get; set; }

        [Required, EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Imię")]
        public string FirstName { get; set; }

        [Display(Name = "Nazwisko")]
        public string LastName { get; set; }

        [Display(Name = "Stanowisko")]
        public string Position { get; set; }

        [Display(Name = "Menedżer")]
        public string ManagerId { get; set; }


        [Required]
        [Display(Name = "Rola")]
        public string Role { get; set; }
    }
}
