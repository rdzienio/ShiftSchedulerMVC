using ShiftSchedulerMVC.Models;

namespace ShiftSchedulerMVC.Services
{
    public static class DummyData
    {
        public static List<Employee> GetEmployees()
        {
            return Enumerable.Range(1, 9).Select(i => new Employee
            {
                Id = i,
                Name = $"Employee {i}"
            }).ToList();
        }

        public static List<DateTime> GetDates()
        {
            DateTime start = DateTime.Today;
            return Enumerable.Range(0, 30).Select(i => start.AddDays(i)).ToList();
        }

        public static Dictionary<DateTime, Dictionary<ShiftType, int>> GetShiftRequirements(List<DateTime> dates)
        {
            var dict = new Dictionary<DateTime, Dictionary<ShiftType, int>>();
            foreach (var date in dates)
            {
                dict[date] = new Dictionary<ShiftType, int>
                {
                    { ShiftType.Morning, 2 },
                    { ShiftType.Afternoon, 2 },
                    { ShiftType.Night, 2 }
                };
            }
            return dict;
        }
    }
}
