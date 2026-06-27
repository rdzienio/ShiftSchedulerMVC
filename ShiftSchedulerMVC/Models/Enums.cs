using System.ComponentModel.DataAnnotations;

namespace ShiftSchedulerMVC.Models
{
    public enum ShiftType
    {
        // Zmiany 8-godzinne (klasyczna siatka 3 x 8h pokrywająca dobę)
        [Display(Name = "Poranna 8h")]
        Morning,
        [Display(Name = "Popołudniowa 8h")]
        Afternoon,
        [Display(Name = "Nocna 8h")]
        Night,

        // Zmiany 12-godzinne (działają równolegle z 8h; długość = cecha slotu/zapotrzebowania)
        [Display(Name = "Dzienna 12h")]
        Day12,
        [Display(Name = "Nocna 12h")]
        Night12
    }
}
