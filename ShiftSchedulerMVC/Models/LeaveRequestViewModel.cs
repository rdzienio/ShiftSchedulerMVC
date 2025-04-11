using System.ComponentModel.DataAnnotations;

namespace ShiftSchedulerMVC.Models
{
    public class LeaveRequestViewModel
    {
        [Required(ErrorMessage = "Musisz wybrać co najmniej jeden dzień.")]
        public string LeaveDates { get; set; } // CSV: "2025-04-10,2025-04-12"

        public string Reason { get; set; }
    }
}
