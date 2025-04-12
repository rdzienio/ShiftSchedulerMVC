using Microsoft.AspNetCore.Authorization;
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
        [Authorize(Roles = "Manager")]
        [HttpPost]
        public async Task<IActionResult> Generate(ShiftRequirementInput input)
        {
            var dates = GenerateDateRange(input.StartDate, input.EndDate);
            var manager = await _userManager.GetUserAsync(User);
            var employees = await _context.Users
                .Where(e => e.ManagerId == manager.Id)
                .Select(e => new Employee { Id = e.Id, Name = $"{e.FirstName} {e.LastName}" })
                .ToListAsync();

            var holidayOverrides = await _context.HolidayOverrides
                .Where(h => h.ManagerId == manager.Id &&
                h.Date >= input.StartDate && h.Date <= input.EndDate)
                    .ToListAsync();

            var overrideDict = holidayOverrides.ToDictionary(
                h => h.Date.Date,
                h => new Dictionary<ShiftType, int>
                {
                    { ShiftType.Morning, h.MorningCount },
                    { ShiftType.Afternoon, h.AfternoonCount },
                    { ShiftType.Night, h.NightCount }
                });


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
                    { ShiftType.Morning, overrideDict.ContainsKey(date) ? overrideDict[date][ShiftType.Morning] : morning },
                    { ShiftType.Afternoon, overrideDict.ContainsKey(date) ? overrideDict[date][ShiftType.Afternoon] : afternoon },
                    { ShiftType.Night, overrideDict.ContainsKey(date) ? overrideDict[date][ShiftType.Night] : night }
                };
            }


            var leaveDict = await _context.LeaveRequests
                .Where(r =>
            r.Status == LeaveStatus.Approved &&
            r.Date.Date >= input.StartDate.Date &&
            r.Date.Date <= input.EndDate.Date)
            .GroupBy(r => r.EmployeeId)
            .ToDictionaryAsync(
            g => g.Key,
            g => g.Select(r => r.Date.Date).ToHashSet()
            );



            var vacationDays = leaveDict
                .SelectMany(kvp => kvp.Value.Select(date => (empId: kvp.Key, date)))
                .ToHashSet();

            var result = GeneticScheduler.Run(employees, dates, shiftRequirements, input.WorkingHours, vacationDays);

            var employeeHours = result.Genes
                .SelectMany(g => g.AssignedEmployees.Select(e => e.Id))
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count() * 8);
            var leaveInRange = _context.LeaveRequests
            .Where(r => r.Status == LeaveStatus.Approved &&
                r.Date >= input.StartDate &&
                r.Date <= input.EndDate)
            .ToList();

            foreach (var leave in leaveInRange)
            {
                if (!employeeHours.ContainsKey(leave.EmployeeId))
                    employeeHours[leave.EmployeeId] = 0;

                employeeHours[leave.EmployeeId] += 8;
            }

            var leaveDays = _context.LeaveRequests
    .Where(r => r.Status == LeaveStatus.Approved &&
                r.Date >= input.StartDate &&
                r.Date <= input.EndDate)
    .ToList() // 👈 najpierw pobieramy z bazy
    .Select(r => (r.EmployeeId, r.Date.Date)) // 👈 teraz możemy zrobić krotkę
    .ToHashSet();


            ViewBag.LeaveDays = leaveDays;


            ViewBag.EmployeeHours = employeeHours;
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
