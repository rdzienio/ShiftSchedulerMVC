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
            _ => ""
        };

        public static string CssClass(ShiftType shift) => shift switch
        {
            ShiftType.Morning => "shift-morning",
            ShiftType.Afternoon => "shift-afternoon",
            ShiftType.Night => "shift-night",
            _ => ""
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
