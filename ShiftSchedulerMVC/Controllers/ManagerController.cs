using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftSchedulerMVC.Data;
using ShiftSchedulerMVC.Models;

namespace ShiftSchedulerMVC.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ManagerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> LeaveRequests()
        {
            var manager = await _userManager.GetUserAsync(User);

            var requests = await _context.LeaveRequests
                .Include(r => r.Employee)
                .Where(r => r.Employee.ManagerId == manager.Id)
                .OrderBy(r => r.Date)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _context.LeaveRequests.FindAsync(id);
            if (request != null && request.Status == LeaveStatus.Pending)
            {
                request.Status = LeaveStatus.Approved;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("LeaveRequests");
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var request = await _context.LeaveRequests.FindAsync(id);
            if (request != null && request.Status == LeaveStatus.Pending)
            {
                request.Status = LeaveStatus.Rejected;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("LeaveRequests");
        }

        public async Task<IActionResult> EmployeeList()
        {
            var manager = await _userManager.GetUserAsync(User);
            var employees = await _context.Users
                .Where(e => e.ManagerId == manager.Id)
                .ToListAsync();

            return View(employees);
        }

    }
}
