using AttendanceManagementSystem.Services;
using AttendanceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AttendanceManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IAuthService authService, IConfiguration configuration, ILogger<AccountController> logger)
        {
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
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
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var userId = _authService.GetCurrentUserId();
            if (userId == null)
            {
                return Json(new { success = false, message = "Not authenticated." });
            }

            _logger.LogInformation("ChangePassword requested for UserId {UserId}", userId);

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return Json(new { success = false, message = "New password must be at least 6 characters." });

            if (newPassword != confirmPassword)
                return Json(new { success = false, message = "Passwords do not match." });

            try
            {
                var changed = await _authService.ChangePasswordAsync(userId.Value, currentPassword, newPassword);
                if (!changed)
                {
                    return Json(new { success = false, message = "Current password is incorrect or the password update did not persist." });
                }

                return Json(new { success = true, message = "Password changed successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChangePassword failed for UserId {UserId}", userId);
                return Json(new { success = false, message = "Unable to change password right now." });
            }
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
