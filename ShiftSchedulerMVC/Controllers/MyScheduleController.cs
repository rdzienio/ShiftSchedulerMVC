using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftSchedulerMVC.Data;
using ShiftSchedulerMVC.Models;

namespace ShiftSchedulerMVC.Controllers
{
    [Authorize(Roles = "Employee")]
    public class MyScheduleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MyScheduleController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            var schedule = await _context.FinalizedSchedules
                .Where(s => s.EmployeeId == user.Id)
                .OrderBy(s => s.Date)
                .ToListAsync();

            return View(schedule);
        }
    }

}
