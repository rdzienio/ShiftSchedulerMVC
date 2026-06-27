using System.ComponentModel.DataAnnotations;

namespace ShiftSchedulerMVC.Models
{
    public class CreateUserViewModel
    {
        [Required]
        [Display(Name = "Nazwa użytkownika")]
        public string UserName { get; set; }

        [Required, EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required, DataType(DataType.Password)]
        [Display(Name = "Hasło")]
        public string Password { get; set; }

        [Required]
        [Display(Name = "Imię")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Nazwisko")]
        public string LastName { get; set; }

        [Display(Name = "Stanowisko")]
        public string Position { get; set; }

        [Required]
        [Display(Name = "Rola")]
        public string Role { get; set; }
    }
}
