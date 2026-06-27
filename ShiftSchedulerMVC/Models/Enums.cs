using System.ComponentModel.DataAnnotations;

namespace ShiftSchedulerMVC.Models
{
    public enum ShiftType
    {
        // Zmiany 8-godzinne (klasyczna siatka 3 x 8h pokrywająca dobę)
        [Display(Name = "Poranna (06:00–14:00)")]
        Morning,
        [Display(Name = "Popołudniowa (14:00–22:00)")]
        Afternoon,
        [Display(Name = "Nocna (22:00–06:00)")]
        Night,

        // Zmiany 12-godzinne (działają równolegle z 8h; długość = cecha slotu/zapotrzebowania)
        [Display(Name = "Dzienna 12h (06:00–18:00)")]
        Day12,
        [Display(Name = "Nocna 12h (18:00–06:00)")]
        Night12
    }
}
