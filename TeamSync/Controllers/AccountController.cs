using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using TeamSync.ViewModels;

namespace TeamSync.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ApplicationDbContext context,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
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
            // First check if user exists and is active
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null && !user.IsActive)
            {
                _logger.LogWarning("Login attempt by deactivated user {Email}.", model.Email);
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact an administrator.");
                return View(model);
            }

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

    /// <summary>
    /// Display alert preferences page for current user.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> AlertPreferences()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        var preference = await _context.AlertPreferences
            .FirstOrDefaultAsync(ap => ap.UserId == user.Id);

        // Create default preference if none exists
        if (preference == null)
        {
            preference = new AlertPreference
            {
                UserId = user.Id,
                NotificationFrequency = "Weekly",
                DigestDayOfWeek = 1, // Monday
                DigestHourUtc = 9
            };
            _context.AlertPreferences.Add(preference);
            await _context.SaveChangesAsync();
        }

        return View(preference);
    }

    /// <summary>
    /// Update alert preferences for current user.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> AlertPreferences(AlertPreference model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        var preference = await _context.AlertPreferences
            .FirstOrDefaultAsync(ap => ap.UserId == user.Id);

        if (preference == null)
        {
            model.UserId = user.Id;
            model.CreatedAt = DateTime.UtcNow;
            _context.AlertPreferences.Add(model);
        }
        else
        {
            preference.NotificationFrequency = model.NotificationFrequency;
            preference.DigestDayOfWeek = model.DigestDayOfWeek;
            preference.DigestHourUtc = model.DigestHourUtc;
            preference.ReceiveTaskAssignmentAlerts = model.ReceiveTaskAssignmentAlerts;
            preference.ReceiveApprovalRejectionAlerts = model.ReceiveApprovalRejectionAlerts;
            preference.ReceiveStatusChangeAlerts = model.ReceiveStatusChangeAlerts;
            preference.ReceiveCommentAlerts = model.ReceiveCommentAlerts;
            preference.ReceiveGroupAlerts = model.ReceiveGroupAlerts;
            preference.UpdatedAt = DateTime.UtcNow;
            _context.AlertPreferences.Update(preference);
        }

        try
        {
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Alert preferences updated successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating alert preferences for user {UserId}", user.Id);
            TempData["ErrorMessage"] = "An error occurred while updating preferences.";
        }

        return RedirectToAction(nameof(AlertPreferences));
    }
}


