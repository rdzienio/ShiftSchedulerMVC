using System.ComponentModel.DataAnnotations;
using ShiftSchedulerMVC.Helpers;

namespace ShiftSchedulerMVC.Models
{
    public class ShiftTimeSettingsViewModel : IValidatableObject
    {
        public List<ShiftTimeRow> Shifts { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var startByType = Shifts
                .GroupBy(s => s.ShiftType)
                .ToDictionary(g => g.Key, g => g.First().StartHour);

            // Nakładanie sprawdzamy TYLKO w obrębie tej samej grupy długości. 8h i 12h
            // celowo działają równolegle (np. Dzienna 12h pokrywa się z Poranną 8h), więc
            // kolizji między grupami nie zgłaszamy.
            var groups = new[]
            {
                new[] { ShiftType.Morning, ShiftType.Afternoon, ShiftType.Night },
                new[] { ShiftType.Day12, ShiftType.Night12 }
            };

            foreach (var group in groups)
            {
                var present = group.Where(startByType.ContainsKey).ToList();

                for (int i = 0; i < present.Count; i++)
                {
                    for (int j = i + 1; j < present.Count; j++)
                    {
                        var a = present[i];
                        var b = present[j];

                        if (Overlaps(startByType[a], ShiftDisplay.DurationHours(a),
                                     startByType[b], ShiftDisplay.DurationHours(b)))
                        {
                            yield return new ValidationResult(
                                $"Zmiany nachodzą na siebie: {ShiftDisplay.Name(a)} i {ShiftDisplay.Name(b)}. " +
                                "Popraw godziny startu.");
                        }
                    }
                }
            }
        }

        // Każda zmiana zajmuje zbiór pełnych godzin na 24h zegarze (z zawijaniem przez północ).
        // Nakładanie = niepuste przecięcie tych zbiorów.
        private static bool Overlaps(int startA, int durationA, int startB, int durationB)
        {
            var hoursB = Enumerable.Range(0, durationB).Select(i => (startB + i) % 24).ToHashSet();
            return Enumerable.Range(0, durationA).Any(i => hoursB.Contains((startA + i) % 24));
        }
    }

    public class ShiftTimeRow
    {
        public ShiftType ShiftType { get; set; }

        [Range(0, 23, ErrorMessage = "Godzina startu musi być z zakresu 0–23.")]
        [Display(Name = "Godzina startu")]
        public int StartHour { get; set; }
    }
}
