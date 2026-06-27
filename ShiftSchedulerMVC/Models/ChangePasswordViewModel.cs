using System.ComponentModel.DataAnnotations;

namespace ShiftSchedulerMVC.Models
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Podaj obecne hasło.")]
        [DataType(DataType.Password)]
        [Display(Name = "Obecne hasło")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Podaj nowe hasło.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nowe hasło")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Potwierdź nowe hasło.")]
        [DataType(DataType.Password)]
        [Display(Name = "Potwierdź nowe hasło")]
        [Compare(nameof(NewPassword), ErrorMessage = "Hasła nie są takie same.")]
        public string ConfirmPassword { get; set; }
    }
}
