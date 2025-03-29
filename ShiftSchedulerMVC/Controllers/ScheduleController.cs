using Microsoft.AspNetCore.Mvc;
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
        public IActionResult Generate()
        {
            var employees = DummyData.GetEmployees();
            var dates = DummyData.GetDates();
            var requirements = DummyData.GetShiftRequirements(dates);

            var best = GeneticScheduler.Run(employees, dates, requirements);

            return View("Result", best);
        }
    }
}
