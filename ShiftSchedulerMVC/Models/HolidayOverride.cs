using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ShiftSchedulerMVC.Models
{
    public class HolidayOverride
    {
        public int Id { get; set; }
        [ValidateNever]
        public string ManagerId { get; set; }
        [Required]
        public DateTime Date { get; set; }
        [Required]
        public int MorningCount { get; set; } = 0;
        [Required]
        public int AfternoonCount { get; set; } = 0;
        [Required]
        public int NightCount { get; set; } = 0;
        [ValidateNever]
        public ApplicationUser Manager { get; set; }
    }

}
