using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShiftSchedulerMVC.Data;
using ShiftSchedulerMVC.Helpers;
using ShiftSchedulerMVC.Models;
//using ShiftSchedulerMVC.ViewModels;

namespace ShiftSchedulerMVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> ShiftTimes()
        {
            var saved = await _context.ShiftTimeSettings.ToDictionaryAsync(s => s.ShiftType, s => s.StartHour);

            var model = new ShiftTimeSettingsViewModel
            {
                Shifts = Enum.GetValues<ShiftType>()
                    .Select(st => new ShiftTimeRow
                    {
                        ShiftType = st,
                        StartHour = saved.TryGetValue(st, out var h) ? h : ShiftSchedulerMVC.Helpers.ShiftTimes.DefaultStartHour(st)
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ShiftTimes(ShiftTimeSettingsViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            foreach (var row in model.Shifts)
            {
                var setting = await _context.ShiftTimeSettings
                    .FirstOrDefaultAsync(s => s.ShiftType == row.ShiftType);

                if (setting == null)
                {
                    _context.ShiftTimeSettings.Add(new ShiftTimeSetting
                    {
                        ShiftType = row.ShiftType,
                        StartHour = row.StartHour
                    });
                }
                else
                {
                    setting.StartHour = row.StartHour;
                }
            }

            await _context.SaveChangesAsync();

            // Odświeżenie globalnego cache'u, żeby algorytm i widoki od razu użyły nowych godzin.
            var dict = await _context.ShiftTimeSettings.ToDictionaryAsync(s => s.ShiftType, s => s.StartHour);
            ShiftSchedulerMVC.Helpers.ShiftTimes.Load(dict);

            TempData["Success"] = "Zapisano godziny rozpoczęcia zmian.";
            return RedirectToAction("ShiftTimes");
        }

        public async Task<IActionResult> Index(string roleFilter = null, string nameFilter = null, string positionFilter = null)
        {
            var users = await _userManager.Users.ToListAsync();
            var model = new List<UserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var matchesRole = string.IsNullOrEmpty(roleFilter) || roles.Contains(roleFilter);
                var matchesName = string.IsNullOrEmpty(nameFilter) ||
                                  user.FirstName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) ||
                                  user.LastName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) ||
                                  user.Email.Contains(nameFilter, StringComparison.OrdinalIgnoreCase);

                var matchesPosition = string.IsNullOrEmpty(positionFilter) ||
                                      (user.Position?.Contains(positionFilter, StringComparison.OrdinalIgnoreCase) ?? false);

                if (matchesRole && matchesName && matchesPosition)
                {
                    model.Add(new UserViewModel
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Position = user.Position,
                        Roles = roles.ToList()
                    });
                }
            }

            ViewBag.AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            ViewBag.RoleFilter = roleFilter;
            ViewBag.NameFilter = nameFilter;
            ViewBag.PositionFilter = positionFilter;

            return View(model);
        }




        public IActionResult Create()
        {
            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.UserName,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Position = model.Position
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);

            var model = new EditUserViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Position = user.Position,
                Role = currentRoles.FirstOrDefault(),
                ManagerId = user.ManagerId
            };

            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            ViewBag.Managers = new SelectList(
                await _userManager.GetUsersInRoleAsync("Manager"),
                    "Id", "FullName"
);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.UserName = model.UserName;
            user.Email = model.Email;
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Position = model.Position;
            user.ManagerId = model.ManagerId;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View(model);
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, userRoles);
            await _userManager.AddToRoleAsync(user, model.Role);

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            // Nie pozwalamy na usunięcie samego siebie
            if (User.Identity?.Name == user.UserName)
            {
                TempData["Error"] = "Nie możesz usunąć własnego konta.";
                return RedirectToAction("Index");
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = "Wystąpił błąd podczas usuwania użytkownika.";
            }

            return RedirectToAction("Index");
        }

    }
}
