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
            //var employees = DummyEmployees();
            var employees = GenerateEmployees(input.EmployeeCount);

            //var dates = GenerateDateRange(DateTime.Today, 31); // tymczasowo
            var dates = GenerateDateRange(DateTime.Today, input.NumberOfDays); // tymczasowo

            var shiftRequirements = new Dictionary<DateTime, Dictionary<ShiftType, int>>();
            foreach (var date in dates)
            {
                shiftRequirements[date] = new Dictionary<ShiftType, int>
                {
                    { ShiftType.Morning, input.MorningCount },
                    { ShiftType.Afternoon, input.AfternoonCount },
                    { ShiftType.Night, input.NightCount }
                };
            }

            var result = GeneticScheduler.Run(employees, dates, shiftRequirements);
            return View("Result", result);
        }
        private List<Employee> DummyEmployees()
        {
            var employees = new List<Employee>();
            for (int i = 1; i <= 9; i++)
            {
                employees.Add(new Employee { Id = i, Name = $"Employee {i}" });
            }
            return employees;
        }

        private List<DateTime> GenerateDateRange(DateTime start, int days)
        {
            var dates = new List<DateTime>();
            for (int i = 0; i < days; i++)
            {
                dates.Add(start.AddDays(i));
            }
            return dates;
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

    }
}
