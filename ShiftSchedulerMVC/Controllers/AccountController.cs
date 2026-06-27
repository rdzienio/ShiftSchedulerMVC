using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShiftSchedulerMVC.Models;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string returnUrl = "/")
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password, string returnUrl = "/")
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            var result = await _signInManager.PasswordSignInAsync(user, password, true, false);
            if (result.Succeeded)
                return LocalRedirect(returnUrl ?? "/Home/Index");
        }

        ModelState.AddModelError("", "Nieprawidłowy login lub hasło");
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied()
    {
        return View("AccessDenied");
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login");

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var msg = error.Code switch
                {
                    "PasswordMismatch" => "Obecne hasło jest nieprawidłowe.",
                    "PasswordTooShort" => "Nowe hasło jest za krótkie (min. 6 znaków).",
                    "PasswordRequiresDigit" => "Nowe hasło musi zawierać cyfrę.",
                    "PasswordRequiresUpper" => "Nowe hasło musi zawierać wielką literę.",
                    "PasswordRequiresLower" => "Nowe hasło musi zawierać małą literę.",
                    "PasswordRequiresNonAlphanumeric" => "Nowe hasło musi zawierać znak specjalny.",
                    _ => error.Description
                };
                ModelState.AddModelError("", msg);
            }
            return View(model);
        }

        // Odśwież cookie uwierzytelnienia po zmianie hasła (inaczej stary stamp może wylogować).
        await _signInManager.RefreshSignInAsync(user);

        TempData["Success"] = "Hasło zostało zmienione.";
        return RedirectToAction("ChangePassword");
    }
}
