namespace ShiftSchedulerMVC.Models
{
    public class ShiftRequirementInput
    {
        public int MorningCount { get; set; } = 2;
        public int AfternoonCount { get; set; } = 2;
        public int NightCount { get; set; } = 2;
        public int EmployeeCount { get; set; } = 9;
        public int NumberOfDays { get; set; } = 31;

        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(6);


    }
}
