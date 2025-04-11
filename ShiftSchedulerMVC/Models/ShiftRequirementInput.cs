namespace ShiftSchedulerMVC.Models
{
    public class ShiftRequirementInput
    {
        // Dni robocze
        public int MorningCount { get; set; } = 2;
        public int AfternoonCount { get; set; } = 2;
        public int NightCount { get; set; } = 2;

        // Weekend – sobota
        public int SaturdayMorningCount { get; set; } = 0;
        public int SaturdayAfternoonCount { get; set; } = 0;
        public int SaturdayNightCount { get; set; } = 0;

        // Weekend – niedziela
        public int SundayMorningCount { get; set; } = 0;
        public int SundayAfternoonCount { get; set; } = 0;
        public int SundayNightCount { get; set; } = 0;

        //public int EmployeeCount { get; set; } = 9;
        public int NumberOfDays { get; set; } = 31;
        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(6);
        public int WorkingHours { get; set; } = 160;
    }
}

