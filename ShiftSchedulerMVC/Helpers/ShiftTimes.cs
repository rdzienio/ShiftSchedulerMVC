using ShiftSchedulerMVC.Models;

namespace ShiftSchedulerMVC.Helpers
{
    /// <summary>
    /// Cache godzin rozpoczęcia zmian (globalny, w pamięci). Ładowany z bazy przy starcie
    /// aplikacji i odświeżany po zapisie w panelu admina. Algorytm i widoki czytają godziny
    /// stąd zamiast z zaszytych na sztywno wartości.
    /// </summary>
    public static class ShiftTimes
    {
        // Domyślne godziny startu – fallback oraz wartości seedowane do bazy.
        private static readonly Dictionary<ShiftType, int> Defaults = new()
        {
            { ShiftType.Morning, 6 },
            { ShiftType.Afternoon, 14 },
            { ShiftType.Night, 22 },
            { ShiftType.Day12, 6 },
            { ShiftType.Night12, 18 },
        };

        private static Dictionary<ShiftType, int> _startHours = new(Defaults);

        /// <summary>Aktualna (skonfigurowana) godzina rozpoczęcia danej zmiany.</summary>
        public static int StartHour(ShiftType shift) =>
            _startHours.TryGetValue(shift, out var h) ? h : DefaultStartHour(shift);

        /// <summary>Domyślna (fabryczna) godzina startu – używana przy seedowaniu i jako fallback.</summary>
        public static int DefaultStartHour(ShiftType shift) =>
            Defaults.TryGetValue(shift, out var d) ? d : 0;

        /// <summary>Podmienia cache; brakujące typy uzupełniane są wartościami domyślnymi.</summary>
        public static void Load(IDictionary<ShiftType, int> startHours)
        {
            var updated = new Dictionary<ShiftType, int>(Defaults);
            foreach (var kvp in startHours)
                updated[kvp.Key] = kvp.Value;
            _startHours = updated;
        }
    }
}
