using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftSchedulerMVC.Data;
using ShiftSchedulerMVC.Models;

namespace ShiftSchedulerMVC.Controllers
{
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public LeaveController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> MyRequests()
        {
            var user = await _userManager.GetUserAsync(User);
            var requests = await _context.LeaveRequests
                .Where(l => l.EmployeeId == user.Id)
                .OrderByDescending(l => l.Date)
                .ToListAsync();

            return View(requests);
        }

        [HttpGet]
        public async Task<IActionResult> Request()
        {
            var user = await _userManager.GetUserAsync(User);
            var existing = await _context.LeaveRequests
                .Where(r => r.EmployeeId == user.Id && r.Status != LeaveStatus.Rejected)
                .Select(r => r.Date)
                .ToListAsync();

            ViewBag.DisabledDates = existing.Select(d => d.ToString("yyyy-MM-dd")).ToList();

            return View();
        }



        [HttpPost]
        public async Task<IActionResult> Request(LeaveRequestViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            var dateList = model.LeaveDates
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(d => DateTime.Parse(d))
                .ToList();

            var existingDates = await _context.LeaveRequests
                .Where(r => r.EmployeeId == user.Id && r.Status != LeaveStatus.Rejected)
                .Select(r => r.Date)
                .ToListAsync();

            var duplicates = dateList.Intersect(existingDates).ToList();
            if (duplicates.Any())
            {
                ModelState.AddModelError("", "Masz już złożony wniosek na niektóre z tych dni.");
                return View(model);
            }

            foreach (var date in dateList)
            {
                var request = new LeaveRequest
                {
                    EmployeeId = user.Id,
                    Date = date,
                    Reason = model.Reason,
                    Status = LeaveStatus.Pending
                };
                _context.LeaveRequests.Add(request);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("MyRequests");
        }


        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var request = await _context.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id && r.EmployeeId == user.Id);

            if (request == null || request.Status != LeaveStatus.Pending)
                return NotFound();

            _context.LeaveRequests.Remove(request);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Wniosek został usunięty.";
            return RedirectToAction("MyRequests");
        }
    }
}
