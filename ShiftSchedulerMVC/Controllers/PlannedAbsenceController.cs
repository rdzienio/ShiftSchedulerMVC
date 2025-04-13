using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShiftSchedulerMVC.Data;
using ShiftSchedulerMVC.Models;

[Authorize(Roles = "Manager")]
public class PlannedAbsenceController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public PlannedAbsenceController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var manager = await _userManager.GetUserAsync(User);
        var employees = await _context.Users
            .Where(e => e.ManagerId == manager.Id)
            .ToListAsync();

        var absences = await _context.PlannedAbsences
            .Where(a => employees.Select(e => e.Id).Contains(a.EmployeeId))
            .Include(a => a.Employee)
            .OrderBy(a => a.Date)
            .ToListAsync();

        return View(absences);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var manager = await _userManager.GetUserAsync(User);
        var employees = await _context.Users
            .Where(e => e.ManagerId == manager.Id)
            .ToListAsync();

        ViewBag.EmployeeSelectList = new SelectList(
            employees.Select(e => new { e.Id, FullName = e.FirstName + " " + e.LastName }),
            "Id", "FullName"
        );

        return View(new PlannedAbsence { Date = DateTime.Today });
    }

    [HttpPost]
    public async Task<IActionResult> Create(PlannedAbsence model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);

            var request = new LeaveRequest
            {
                EmployeeId = model.EmployeeId,
                Date = model.Date,
                Reason = model.Reason.ToString(),
                Status = LeaveStatus.Approved
            };
            _context.LeaveRequests.Add(request);


        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }


    /*[HttpPost]
    public async Task<IActionResult> Create(PlannedAbsence model)
    {
        Console.WriteLine($"Received: {model.EmployeeId}, {model.Date}, {model.Reason}");

        if (!ModelState.IsValid)
        {
            var manager = await _userManager.GetUserAsync(User);
            var employees = await _context.Users
                .Where(e => e.ManagerId == manager.Id)
                .ToListAsync();

            ViewBag.EmployeeSelectList = new SelectList(
                employees.Select(e => new { e.Id, FullName = e.FirstName + " " + e.LastName }),
                "Id", "FullName"
            );

            return View(model);
        }


    _context.PlannedAbsences.Add(model);
        await _context.SaveChangesAsync();
        Console.WriteLine("✔️ Zapisano do bazy");

        return RedirectToAction("Index");
    }*/


    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var absence = await _context.PlannedAbsences.FindAsync(id);
        if (absence != null)
        {
            _context.PlannedAbsences.Remove(absence);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Index");
    }
}
