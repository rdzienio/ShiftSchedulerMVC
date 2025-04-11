using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftSchedulerMVC.Data;
using ShiftSchedulerMVC.Models;
using ShiftSchedulerMVC.Services;

namespace ShiftSchedulerMVC.Controllers
{
    public class ScheduleController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ScheduleController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Generate(ShiftRequirementInput input)
        {
            var dates = GenerateDateRange(input.StartDate, input.EndDate);
            var manager = await _userManager.GetUserAsync(User);
            var employees = await _context.Users
                .Where(e => e.ManagerId == manager.Id)
                .Select(e => new Employee { Id = e.Id, Name = $"{e.FirstName} {e.LastName}" })
                .ToListAsync();


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

            var leaveDict = await _context.LeaveRequests
                .Where(r => r.Status == LeaveStatus.Approved)
                .GroupBy(r => r.EmployeeId)
                .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(r => r.Date.Date).ToHashSet()
            );

            var vacationDays = leaveDict
                .SelectMany(kvp => kvp.Value.Select(date => (empId: kvp.Key, date)))
                .ToHashSet();

            var result = GeneticScheduler.Run(employees, dates, shiftRequirements, input.WorkingHours, vacationDays);

            return View("Result", result);
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
