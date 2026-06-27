using System.ComponentModel.DataAnnotations;

namespace ShiftSchedulerMVC.Models
{
    public class ShiftRequirementInput : IValidatableObject
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

        // === Zmiany 12-godzinne (działają równolegle z 8h; 0 = brak takiego slotu) ===
        // Dni robocze
        public int Day12Count { get; set; } = 0;
        public int Night12Count { get; set; } = 0;

        // 12h – sobota
        public int SaturdayDay12Count { get; set; } = 0;
        public int SaturdayNight12Count { get; set; } = 0;

        // 12h – niedziela
        public int SundayDay12Count { get; set; } = 0;
        public int SundayNight12Count { get; set; } = 0;

        //public int EmployeeCount { get; set; } = 9;
        public int NumberOfDays { get; set; } = 31;

        [Required(ErrorMessage = "Podaj datę początkową.")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Podaj datę końcową.")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(6);

        [Range(1, 1000, ErrorMessage = "Liczba roboczogodzin musi być z zakresu 1–1000.")]
        public int WorkingHours { get; set; } = 160;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDate < StartDate)
            {
                yield return new ValidationResult(
                    "Data końcowa nie może być wcześniejsza niż data początkowa.",
                    new[] { nameof(EndDate) });
            }
        }
    }
}
