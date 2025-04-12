using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftSchedulerMVC.Data;
using ShiftSchedulerMVC.Models;

namespace ShiftSchedulerMVC.Controllers
{
    [Authorize(Roles = "Manager")]
    public class HolidayOverrideController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HolidayOverrideController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var manager = await _userManager.GetUserAsync(User);
            var overrides = await _context.HolidayOverrides
                .Where(h => h.ManagerId == manager.Id)
                .OrderBy(h => h.Date)
                .ToListAsync();

            return View(overrides);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View("Form", new HolidayOverride { Date = DateTime.Today });
        }

        [HttpPost]
        public async Task<IActionResult> Create(HolidayOverride model)
        {
            var manager = await _userManager.GetUserAsync(User);
            model.ManagerId = manager.Id;

            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState)
                {
                    foreach (var sub in error.Value.Errors)
                    {
                        Console.WriteLine(manager.Id);
                        Console.WriteLine($"[ModelError] {error.Key}: {sub.ErrorMessage}");
                    }
                }

                return View("Form", model);
            }



            _context.HolidayOverrides.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.HolidayOverrides.FindAsync(id);
            if (item == null) return NotFound();
            return View("Form", item);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(HolidayOverride model)
        {
            var manager = await _userManager.GetUserAsync(User);

            if (!ModelState.IsValid)
                return View("Form", model);

            var existing = await _context.HolidayOverrides.AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == model.Id && h.ManagerId == manager.Id);

            if (existing == null)
                return NotFound();

            model.ManagerId = manager.Id; // upewnij się, że managera nie da się podmienić

            _context.HolidayOverrides.Update(model);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }


        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var manager = await _userManager.GetUserAsync(User);
            var entry = await _context.HolidayOverrides
                .FirstOrDefaultAsync(h => h.Id == id && h.ManagerId == manager.Id);

            if (entry != null)
            {
                _context.HolidayOverrides.Remove(entry);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

    }
}
