using Microsoft.AspNetCore.Mvc;
using ShiftSchedulerMVC.Models;
using ShiftSchedulerMVC.Services;

namespace ShiftSchedulerMVC.Controllers
{
    public class ScheduleController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Generate(ShiftRequirementInput input)
        {
            var dates = GenerateDateRange(input.StartDate, input.EndDate);
            var employees = GenerateEmployees(input.EmployeeCount);

            var shiftRequirements = new Dictionary<DateTime, Dictionary<ShiftType, int>>();

            foreach (var date in dates)
            {
                var isSaturday = date.DayOfWeek == DayOfWeek.Saturday;
                var isSunday = date.DayOfWeek == DayOfWeek.Sunday;

                int morning = input.MorningCount;
                int afternoon = input.AfternoonCount;
                int night = input.NightCount;

                if (isSaturday)
                {
                    morning = input.SaturdayMorningCount;
                    afternoon = input.SaturdayAfternoonCount;
                    night = input.SaturdayNightCount;
                }
                else if (isSunday)
                {
                    morning = input.SundayMorningCount;
                    afternoon = input.SundayAfternoonCount;
                    night = input.SundayNightCount;
                }

                shiftRequirements[date] = new Dictionary<ShiftType, int>
                {
                    { ShiftType.Morning, morning },
                    { ShiftType.Afternoon, afternoon },
                    { ShiftType.Night, night }
                };
            }

            var result = GeneticScheduler.Run(employees, dates, shiftRequirements, input.WorkingHours);
            return View("Result", result);
        }

        private List<Employee> GenerateEmployees(int count)
        {
            var employees = new List<Employee>();
            for (int i = 1; i <= count; i++)
            {
                employees.Add(new Employee { Id = i, Name = $"Employee {i}" });
            }
            return employees;
        }

        private List<DateTime> GenerateDateRange(DateTime start, DateTime end)
        {
            var dates = new List<DateTime>();
            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                dates.Add(date);
            }
            return dates;
        }
    }
}
