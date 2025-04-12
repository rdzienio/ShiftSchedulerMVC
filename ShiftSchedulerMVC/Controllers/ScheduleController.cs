using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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

            var drafts = result.Genes
    .SelectMany(g => g.AssignedEmployees.Select(emp => new DraftSchedule
    {
        ManagerId = manager.Id,
        EmployeeId = emp.Id,
        Date = g.Date.Date,
        Shift = g.Shift
    }))
    .ToList();

            // Usuń stare szkice
            var existingDrafts = _context.DraftSchedules.Where(d => d.ManagerId == manager.Id);
            _context.DraftSchedules.RemoveRange(existingDrafts);
            _context.DraftSchedules.AddRange(drafts);
            await _context.SaveChangesAsync();

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

        [Authorize(Roles = "Manager")]
        [HttpPost]
        public async Task<IActionResult> SaveDraftSchedule(Chromosome chromosome)
        {
            var manager = await _userManager.GetUserAsync(User);

            var drafts = chromosome.Genes
                .SelectMany(g => g.AssignedEmployees.Select(emp => new DraftSchedule
                {
                    ManagerId = manager.Id,
                    EmployeeId = emp.Id,
                    Date = g.Date.Date,
                    Shift = g.Shift
                }))
                .ToList();

            // Usuń poprzednie szkice tego managera
            var existing = _context.DraftSchedules.Where(d => d.ManagerId == manager.Id);
            _context.DraftSchedules.RemoveRange(existing);

            _context.DraftSchedules.AddRange(drafts);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }



        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> ApprovedSchedules()
        {
            var manager = await _userManager.GetUserAsync(User);

            var scheduleDates = await _context.FinalizedSchedules
                .Where(s => s.ManagerId == manager.Id)
                .Select(s => s.Date.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            return View(scheduleDates);
        }


        [Authorize(Roles = "Manager")]
        [HttpPost]
        public async Task<IActionResult> DeleteScheduleGroup(DateTime date)
        {
            var manager = await _userManager.GetUserAsync(User);

            var entries = await _context.FinalizedSchedules
                .Where(s => s.ManagerId == manager.Id && s.Date == date)
                .ToListAsync();

            _context.FinalizedSchedules.RemoveRange(entries);
            await _context.SaveChangesAsync();

            return RedirectToAction("ApprovedSchedules");
        }

        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Drafts()
        {
            var manager = await _userManager.GetUserAsync(User);

            var drafts = await _context.DraftSchedules
                .Where(d => d.ManagerId == manager.Id)
                .Include(d => d.Employee)
                .OrderBy(d => d.Date)
                .ToListAsync();

            var grouped = drafts
                .GroupBy(d => d.EmployeeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(d => d.Date).ToList()
                );

            var employees = await _context.Users
                .Where(e => grouped.Keys.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => $"{e.FirstName} {e.LastName}");

            var dateList = drafts.Select(d => d.Date).ToList();
            var minDate = dateList.Any() ? dateList.Min() : DateTime.Today;
            var maxDate = dateList.Any() ? dateList.Max() : DateTime.Today;

            // 🔧 Unikamy błędu przez przeniesienie operacji Date.Date do pamięci
            ViewBag.Dates = dateList
                .Select(d => d.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var leaveDays = await _context.LeaveRequests
    .Where(r => r.Status == LeaveStatus.Approved &&
                r.Date >= drafts.Min(d => d.Date) &&
                r.Date <= drafts.Max(d => d.Date))
    .ToListAsync();

            ViewBag.LeaveDays = leaveDays
                .Select(r => (r.EmployeeId, r.Date.Date))
                .ToHashSet();


            return View(grouped);
        }

        [Authorize(Roles = "Manager")]
        [HttpPost]
        public async Task<IActionResult> ConfirmDrafts()
        {
            var manager = await _userManager.GetUserAsync(User);

            var drafts = await _context.DraftSchedules
                .Where(d => d.ManagerId == manager.Id)
                .ToListAsync();

            var finalized = drafts.Select(d => new FinalizedSchedule
            {
                ManagerId = d.ManagerId,
                EmployeeId = d.EmployeeId,
                Date = d.Date,
                Shift = d.Shift
            });

            _context.FinalizedSchedules.AddRange(finalized);
            _context.DraftSchedules.RemoveRange(drafts);
            await _context.SaveChangesAsync();

            return RedirectToAction("ApprovedSchedules");
        }

        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> FinalizedSchedules(DateTime date)
        {
            var manager = await _userManager.GetUserAsync(User);

            var entries = await _context.FinalizedSchedules
                .Where(s => s.ManagerId == manager.Id && s.Date == date.Date)
                .Include(s => s.Employee)
                .ToListAsync();

            var grouped = entries
                .GroupBy(e => e.EmployeeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(e => e.Date).ToList()
                );

            var employees = await _context.Users
                .Where(u => grouped.Keys.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

            ViewBag.Employees = employees;
            ViewBag.Dates = entries.Select(e => e.Date.Date).Distinct().OrderBy(d => d).ToList();

            return View(grouped);
        }


    }
}
