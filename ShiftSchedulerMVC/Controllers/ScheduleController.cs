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

            // 🗓️ Ostatnie 7 dni z zatwierdzonego grafiku poprzedniego okresu - żeby reguły
            // sekwencyjne (35h odpoczynku, max 5 dni z rzędu, po nocnej tylko nocna) działały
            // przez granicę miesięcy, a nie zerowały się na 1. dniu nowego horyzontu.
            var priorStart = input.StartDate.Date.AddDays(-7);
            var priorEnd = input.StartDate.Date.AddDays(-1);
            var priorShifts = (await _context.FinalizedSchedules
                .Where(s => s.ManagerId == manager.Id && s.Date >= priorStart && s.Date <= priorEnd)
                .Select(s => new { s.EmployeeId, s.Date, s.Shift })
                .ToListAsync())
                .Select(s => (s.EmployeeId, s.Date, s.Shift))
                .ToList();

            var result = GeneticScheduler.Run(employees, dates, shiftRequirements, input.WorkingHours, vacationDays, priorShifts: priorShifts);



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
            var fitness = GeneticScheduler.EvaluateFitnessWrapper(result, shiftRequirements, input.WorkingHours, vacationDays, employees);
            ViewBag.FitnessScore = fitness;


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



        /*[Authorize(Roles = "Manager")]
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
        }*/

        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> ApprovedSchedules(DateTime? startDate, DateTime? endDate)
        {
            var manager = await _userManager.GetUserAsync(User);

            var schedules = await _context.FinalizedSchedules
                .Where(s => s.ManagerId == manager.Id)
                .ToListAsync();

            if (startDate.HasValue && endDate.HasValue)
            {
                schedules = schedules
                    .Where(s => s.Date.Date >= startDate.Value.Date && s.Date.Date <= endDate.Value.Date)
                    .ToList();
            }

            var groupedDates = schedules
                .Select(s => s.Date.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
            ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");


            return View(groupedDates);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteScheduleGroup(DateTime date)
        {
            var manager = await _userManager.GetUserAsync(User);

            var entries = await _context.FinalizedSchedules
                .Where(s => s.ManagerId == manager.Id && s.Date.Date == date.Date)
                .ToListAsync();

            if (!entries.Any())
            {
                TempData["Error"] = $"Nie znaleziono żadnych wpisów do usunięcia dla daty {date:yyyy-MM-dd}";
                Console.WriteLine($"Nie znaleziono żadnych wpisów do usunięcia dla daty {date:yyyy-MM-dd}");
                return RedirectToAction("ApprovedSchedules");
            }

            _context.FinalizedSchedules.RemoveRange(entries);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Usunięto harmonogram z {date:yyyy-MM-dd}";
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

            var allDraftDates = drafts.Select(d => d.Date).ToList(); // w pamięci
            if (allDraftDates.Count == 0)
            {
                ViewBag.LeaveDays = new HashSet<(string, DateTime)>();
            }
            else
            {
                var minDateL= allDraftDates.Min();
                var maxDateL = allDraftDates.Max();

                var leaveDays = await _context.LeaveRequests
                    .Where(r => r.Status == LeaveStatus.Approved &&
                                r.Date >= minDateL &&
                                r.Date <= maxDateL)
                    .ToListAsync();

                ViewBag.LeaveDays = leaveDays
                    .Select(r => (r.EmployeeId, r.Date.Date))
                    .ToHashSet();
            }



            ViewBag.Employees = employees;
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

        /*[Authorize(Roles = "Manager")]
        public async Task<IActionResult> Finalized(DateTime? date)
        {
            var manager = await _userManager.GetUserAsync(User);

            // Jeśli nie podano daty – weź ostatnią (lub żadną)
            date ??= await _context.FinalizedSchedules
                .Where(s => s.ManagerId == manager.Id)
                .Select(s => s.Date)
                .MaxAsync();

            var schedules = await _context.FinalizedSchedules
                .Where(s => s.ManagerId == manager.Id && s.Date.Date == date.Value.Date)
                .Include(s => s.Employee)
                .ToListAsync();

            // Rekonstrukcja Chromosome
            var genes = schedules
                .GroupBy(s => new { s.Date, s.Shift })
                .Select(g => new Gene
                {
                    Date = g.Key.Date,
                    Shift = g.Key.Shift,
                    AssignedEmployees = g.Select(s => new Employee
                    {
                        Id = s.Employee.Id,
                        Name = $"{s.Employee.FirstName} {s.Employee.LastName}"
                    }).ToList()
                }).ToList();

            var chromosome = new Chromosome { Genes = genes };

            // Liczby godzin
            var employeeHours = schedules
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(g => g.Key, g => g.Count() * 8);

            ViewBag.EmployeeHours = employeeHours;

            // Zatwierdzone urlopy do podświetlenia
            var empIds = schedules.Select(s => s.EmployeeId).Distinct().ToList();
            var leaveDays = await _context.LeaveRequests
                .Where(r => r.Status == LeaveStatus.Approved &&
                            empIds.Contains(r.EmployeeId) &&
                            r.Date == date.Value.Date)
                .ToListAsync(); // <-- pobieramy z bazy

            var leaveSet = leaveDays
                .Select(r => (r.EmployeeId, r.Date.Date))
                .ToHashSet();

            ViewBag.LeaveDays = leaveSet;

            return View("Result", chromosome);
        }*/

        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Finalized(DateTime? startDate, DateTime? endDate)
        {
            var manager = await _userManager.GetUserAsync(User);

            var schedules = await _context.FinalizedSchedules
                .Where(s => s.ManagerId == manager.Id)
                .Include(s => s.Employee)
                .ToListAsync();

            if (startDate.HasValue && endDate.HasValue)
            {
                schedules = schedules
                    .Where(s => s.Date >= startDate.Value && s.Date <= endDate.Value)
                    .ToList();
            }

            var grouped = schedules
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(s => s.Date).ToList()
                );

            var employees = await _context.Users
                .Where(e => grouped.Keys.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => $"{e.FirstName} {e.LastName}");

            var leaveDays = await _context.LeaveRequests
                .Where(r => r.Status == LeaveStatus.Approved &&
                            grouped.Keys.Contains(r.EmployeeId))
                .ToListAsync();

            ViewBag.Employees = employees;
            ViewBag.Dates = schedules.Select(s => s.Date.Date).Distinct().OrderBy(d => d).ToList();
            ViewBag.LeaveDays = leaveDays.Select(r => (r.EmployeeId, r.Date.Date)).ToHashSet();

            return View(grouped);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteScheduleRange(DateTime startDate, DateTime endDate)
        {
            var manager = await _userManager.GetUserAsync(User);

            var entries = await _context.FinalizedSchedules
                .Where(s => s.ManagerId == manager.Id && s.Date.Date >= startDate.Date && s.Date.Date <= endDate.Date)
                .ToListAsync();

            if (!entries.Any())
            {
                TempData["Error"] = $"Nie znaleziono żadnych wpisów do usunięcia w zakresie {startDate:yyyy-MM-dd} – {endDate:yyyy-MM-dd}";
                return RedirectToAction("ApprovedSchedules");
            }

            _context.FinalizedSchedules.RemoveRange(entries);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Usunięto {entries.Count} wpisów z zakresu {startDate:yyyy-MM-dd} – {endDate:yyyy-MM-dd}";
            return RedirectToAction("ApprovedSchedules");
        }

        [Authorize(Roles = "Manager")]
        [HttpGet]
        public async Task<IActionResult> EditSchedule(int id)
        {
            var entry = await _context.FinalizedSchedules.Include(f => f.Employee).FirstOrDefaultAsync(f => f.Id == id);
            if (entry == null) return NotFound();

            ViewBag.AllEmployees = await _context.Users
                .Where(u => u.ManagerId == entry.ManagerId)
                .ToListAsync();

            return View(entry);
        }

        [Authorize(Roles = "Manager")]
        [HttpPost]
        public async Task<IActionResult> EditSchedule(FinalizedSchedule model)
        {
            var entry = await _context.FinalizedSchedules.FindAsync(model.Id);
            if (entry == null) return NotFound();

            entry.EmployeeId = model.EmployeeId;
            entry.Shift = model.Shift;
            await _context.SaveChangesAsync();

            return RedirectToAction("Finalized", new { startDate = model.Date, endDate = model.Date });
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> MySchedule(DateTime? startDate, DateTime? endDate)
        {
            var user = await _userManager.GetUserAsync(User);
            var query = _context.FinalizedSchedules
                .Where(f => f.EmployeeId == user.Id);

            if (startDate.HasValue)
                query = query.Where(f => f.Date >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(f => f.Date <= endDate.Value);

            var schedule = await query.OrderBy(f => f.Date).ToListAsync();

            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            return View(schedule);
        }

        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> MyCalendar(int? year, int? month)
        {
            var user = await _userManager.GetUserAsync(User);
            var today = DateTime.Today;
            int y = year ?? today.Year;
            int m = month ?? today.Month;

            var daysInMonth = DateTime.DaysInMonth(y, m);
            var start = new DateTime(y, m, 1);
            var end = new DateTime(y, m, daysInMonth);

            var shifts = await _context.FinalizedSchedules
                .Where(f => f.EmployeeId == user.Id && f.Date >= start && f.Date <= end)
                .ToListAsync();

            var leaves = await _context.LeaveRequests
                .Where(r => r.EmployeeId == user.Id &&
                            r.Status == LeaveStatus.Approved &&
                            r.Date >= start && r.Date <= end)
                .Select(r => r.Date.Date)
                .ToListAsync();

            var calendar = Enumerable.Range(1, daysInMonth)
                .Select(day =>
                {
                    var date = new DateTime(y, m, day);
                    var shift = shifts.FirstOrDefault(f => f.Date.Date == date)?.Shift;

                    return new CalendarEntry
                    {
                        Date = date,
                        Shift = shift,
                        IsOnLeave = leaves.Contains(date)
                    };
                }).ToList();

            ViewBag.Year = y;
            ViewBag.Month = m;
            ViewBag.Employee = $"{user.FirstName} {user.LastName}";

            return View(calendar);
        }


        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> EmployeeCalendar(string employeeId, int? year, int? month)
        {
            var employee = await _context.Users.FirstOrDefaultAsync(u => u.Id == employeeId);
            if (employee == null) return NotFound();

            var today = DateTime.Today;
            int y = year ?? today.Year;
            int m = month ?? today.Month;

            var start = new DateTime(y, m, 1);
            var end = start.AddMonths(1).AddDays(-1);

            var entries = await _context.FinalizedSchedules
                .Where(f => f.EmployeeId == employeeId && f.Date >= start && f.Date <= end)
                .ToListAsync();

            var model = Enumerable.Range(1, DateTime.DaysInMonth(y, m))
                .Select(day => new CalendarEntry
                {
                    Date = new DateTime(y, m, day),
                    Shift = entries.FirstOrDefault(e => e.Date.Day == day)?.Shift
                })
                .ToList();

            ViewBag.Year = y;
            ViewBag.Month = m;
            ViewBag.Employee = $"{employee.FirstName} {employee.LastName}";

            return View("MyCalendar", model); // Używamy tego samego widoku
        }


    }
}
