using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Models;
using RaceDay.MVC.Services;

namespace RaceDay.MVC.Controllers
{
    // Register, log in, log out. All the real work (checking passwords, saving users) is done by the API.
    public class AccountController : Controller
    {
        private readonly RaceDayApiClient _api;

        public AccountController(RaceDayApiClient api)
        {
            _api = api;
        }

        private bool IsLoggedIn => HttpContext.Session.GetInt32(SessionKeys.UserId) != null;

        // Where a logged-in user lands: Organisers on their dashboard, Participants on the events page
        private IActionResult RedirectToLanding()
        {
            return HttpContext.Session.GetString(SessionKeys.UserRole) == RoleNames.Organiser
                ? RedirectToAction("Dashboard", "Organiser")
                : RedirectToAction("Index", "Home");
        }

        // ---------------- Register ----------------

        [HttpGet]
        public IActionResult Register()
        {
            if (IsLoggedIn) return RedirectToLanding();
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (model.DateOfBirth.HasValue && model.DateOfBirth.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(model.DateOfBirth), "Your date of birth cannot be in the future.");
            }

            if (!ModelState.IsValid) return View(model);

            // The API expects the typed password in a field called "PasswordHash" and hashes it itself
            var body = new
            {
                model.FullName,
                model.Email,
                PasswordHash = model.Password,
                model.Role,
                model.Gender,
                model.DateOfBirth
            };

            var result = await _api.PostAsync<UserSummaryModel>("api/auth/register", body);

            if (!result.Success)
            {
                // For example "Email already registered." - shown at the top of the form
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "We could not create your account. Please try again.");
                return View(model);
            }

            TempData["Success"] = "Your account has been created. Please log in.";
            return RedirectToAction("Login");
        }

        // ---------------- Login ----------------

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (IsLoggedIn) return RedirectToLanding();
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _api.PostAsync<LoginResponseModel>("api/auth/login", new { model.Email, model.Password });

            if (!result.Success || result.Data == null)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "We could not log you in. Please try again.");
                return View(model);
            }

            // Remember who is logged in on every page. (ApiSessionHandler has already stored the API's cookie.)
            HttpContext.Session.SetInt32(SessionKeys.UserId, result.Data.UserId);
            HttpContext.Session.SetString(SessionKeys.UserName, result.Data.FullName);
            HttpContext.Session.SetString(SessionKeys.UserRole, result.Data.Role);

            // Go back to the page they wanted, but only if it is a page on THIS site
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            // Otherwise each role lands on its own page
            return RedirectToLanding();
        }

        // ---------------- Logout ----------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Tell the API to end its session too, then forget everything on our side
            await _api.PostAsync<object>("api/auth/logout", new { });
            HttpContext.Session.Clear();

            TempData["Info"] = "You have been logged out.";
            return RedirectToAction("Login");
        }

        // ---------------- Access denied ----------------

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}