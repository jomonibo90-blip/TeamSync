using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeamSync.Models;
using TeamSync.ViewModels;

namespace TeamSync.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new User
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                StudentId = model.StudentId
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} registered successfully.", user.Email);

                // Assign default Student role
                await _userManager.AddToRoleAsync(user, "Student");

                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(
        model.Email,
              model.Password,
        model.RememberMe,
             lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} logged in successfully.", model.Email);
                return LocalRedirect(returnUrl ?? "/");
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User {Email} account locked out.", model.Email);
                return RedirectToAction(nameof(Lockout));
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    public IActionResult Lockout()
    {
        return View();
    }
}
