using System.ComponentModel.DataAnnotations;

namespace ShiftSchedulerMVC.Models
{
    public class FinalizedSchedule
    {
        public int Id { get; set; }

        [Required]
        public string ManagerId { get; set; }
        public ApplicationUser Manager { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public ShiftType Shift { get; set; }

        [Required]
        public string EmployeeId { get; set; }
        public ApplicationUser Employee { get; set; }
    }

}
