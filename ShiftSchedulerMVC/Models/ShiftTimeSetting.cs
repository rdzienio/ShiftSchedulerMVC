using System.ComponentModel.DataAnnotations;

namespace ShiftSchedulerMVC.Models
{
    /// <summary>
    /// Globalne ustawienie godziny rozpoczęcia danego typu zmiany (jeden wiersz na typ).
    /// Długość zmiany pozostaje stała (8h/12h) – konfigurowalny jest tylko start.
    /// </summary>
    public class ShiftTimeSetting
    {
        public int Id { get; set; }

        public ShiftType ShiftType { get; set; }

        [Range(0, 23, ErrorMessage = "Godzina startu musi być z zakresu 0–23.")]
        public int StartHour { get; set; }
    }
}
