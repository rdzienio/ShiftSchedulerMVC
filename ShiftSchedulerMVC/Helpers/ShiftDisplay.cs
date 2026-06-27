using ShiftSchedulerMVC.Models;

namespace ShiftSchedulerMVC.Helpers
{
    /// <summary>
    /// Centralna definicja kodów i klas CSS dla typów zmian, używana przez widoki
    /// (Drafts, Result, MyCalendar) zamiast powielanej logiki w każdym pliku.
    /// </summary>
    public static class ShiftDisplay
    {
        public static string Code(ShiftType shift) => shift switch
        {
            ShiftType.Morning => "1",
            ShiftType.Afternoon => "2",
            ShiftType.Night => "3",
            ShiftType.Day12 => "D",
            ShiftType.Night12 => "N",
            _ => ""
        };

        public static string CssClass(ShiftType shift) => shift switch
        {
            ShiftType.Morning => "shift-morning",
            ShiftType.Afternoon => "shift-afternoon",
            ShiftType.Night => "shift-night",
            ShiftType.Day12 => "shift-day12",
            ShiftType.Night12 => "shift-night12",
            _ => ""
        };

        /// <summary>
        /// Długość zmiany w godzinach — jedno źródło prawdy używane zarówno przez widoki
        /// (sumowanie godzin) jak i algorytm genetyczny.
        /// </summary>
        public static int DurationHours(ShiftType shift) => shift switch
        {
            ShiftType.Morning => 8,
            ShiftType.Afternoon => 8,
            ShiftType.Night => 8,
            ShiftType.Day12 => 12,
            ShiftType.Night12 => 12,
            _ => 8
        };

        /// <summary>
        /// Widok tabelaryczny (Drafts/Result): puste komórki pozostają bez treści.
        /// </summary>
        public static (string Code, string CssClass) Table(ShiftType? shift, bool isOnLeave) =>
            isOnLeave ? ("", "shift-leave")
            : shift.HasValue ? (Code(shift.Value), CssClass(shift.Value))
            : ("", "");

        /// <summary>
        /// Widok kalendarza: dni wolne jako „W", urlop jako „U".
        /// </summary>
        public static (string Code, string CssClass) Calendar(ShiftType? shift, bool isOnLeave) =>
            isOnLeave ? ("U", "shift-leave")
            : shift.HasValue ? (Code(shift.Value), CssClass(shift.Value))
            : ("W", "shift-free");
    }
}
