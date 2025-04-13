using System.ComponentModel.DataAnnotations;

namespace ShiftSchedulerMVC.Models
{
    public class PlannedAbsence
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Wybierz pracownika")]
        public string EmployeeId { get; set; }

        public ApplicationUser Employee { get; set; }

        [Required(ErrorMessage = "Podaj datę nieobecności")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Wybierz typ nieobecności")]
        public AbsenceReason Reason { get; set; }
    }

    public enum AbsenceReason
    {
        PlannedLeave,
        SickLeave,
        Other
    }
}
