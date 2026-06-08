using AttendanceManagementSystem.Services;
using AttendanceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace AttendanceManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;

        public AccountController(IAuthService authService, IConfiguration configuration)
        {
            _authService = authService;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            if (Request.Query.ContainsKey("azureLoginError") && TempData["Error"] == null)
            {
                TempData["Error"] = "Azure sign-in failed. Check that the client secret value is configured, not the secret ID.";
            }

            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return View(new LoginViewModel());
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _authService.AuthenticateAsync(model.Email, model.Password);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            _authService.SignIn(user, model.RememberMe);

            if (user.Role?.Name == Models.RoleNames.Worker)
            {
                // Workers should always land on the Worker Dashboard first after login.
                return RedirectToAction("Index", "Dashboard");
            }

            return RedirectToAction("Index", "Dashboard");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AzureLogin(string? returnUrl = null)
        {
            if (!IsAzureSignInEnabled())
            {
                TempData["Error"] = "Azure sign-in is not configured correctly. Use local sign-in until the client secret is fixed.";
                return RedirectToAction(nameof(Login));
            }

            var targetReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? Url.Action("Index", "Dashboard") ?? "/Dashboard/Index"
                : returnUrl;

            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(AzureCallback), new { returnUrl = targetReturnUrl })
            };

            return Challenge(properties, "AzureAd");
        }

        private bool IsAzureSignInEnabled()
        {
            var clientId = _configuration["AzureAd:ClientId"];
            return !string.IsNullOrWhiteSpace(clientId);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> AzureCallback(string returnUrl = "/")
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction(nameof(Login));
            }

            var email = User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("preferred_username")
                ?? User.FindFirstValue(ClaimTypes.Upn);

            if (string.IsNullOrWhiteSpace(email))
            {
                await HttpContext.SignOutAsync("CookieAuth");
                ModelState.AddModelError(string.Empty, "Azure sign-in did not provide an email address.");
                return View("Login", new LoginViewModel());
            }

            var user = await _authService.GetUserByEmailAsync(email);
            if (user == null)
            {
                await HttpContext.SignOutAsync("CookieAuth");
                ModelState.AddModelError(string.Empty, $"No local account exists for {email}. Please contact an administrator.");
                return View("Login", new LoginViewModel());
            }

            _authService.SignIn(user, rememberMe: true);

            if (!Url.IsLocalUrl(returnUrl))
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return LocalRedirect(returnUrl);
        }

        [Authorize]
        [HttpGet]
        public IActionResult Profile()
        {
            var user = _authService.GetCurrentUser();
            return View(user);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var user = _authService.GetCurrentUser();
            if (user == null) return RedirectToAction(nameof(Login));

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                TempData["PasswordError"] = "New password must be at least 6 characters.";
                return RedirectToAction(nameof(Profile));
            }

            if (newPassword != confirmPassword)
            {
                TempData["PasswordError"] = "Passwords do not match.";
                return RedirectToAction(nameof(Profile));
            }

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                TempData["PasswordError"] = "Current password is incorrect.";
                return RedirectToAction(nameof(Profile));
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            var dbContext = HttpContext.RequestServices.GetRequiredService<Data.ApplicationDbContext>();
            dbContext.Users.Update(user);
            await dbContext.SaveChangesAsync();

            TempData["PasswordSuccess"] = "Password changed successfully.";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpGet]
        public IActionResult Logout()
        {
            _authService.SignOut();
            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return Forbid();
        }
    }
}
