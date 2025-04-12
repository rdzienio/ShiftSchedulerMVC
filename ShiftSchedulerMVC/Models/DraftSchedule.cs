using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftSchedulerMVC.Models
{
    public class DraftSchedule
    {
        public int Id { get; set; }

        [Required]
        public string ManagerId { get; set; }

        [Required]
        public string EmployeeId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public ShiftType Shift { get; set; }

        public ApplicationUser Employee { get; set; }
    }
}
