using Microsoft.AspNetCore.Mvc;
using WEBTechnologies_Final.Models;
using WEBTechnologies_Final.Services;

namespace WEBTechnologies_Final.Controllers
{
    // Handles register / login / logout for the MVC (web page) side of the app.
    // A controller's public methods are "actions" that respond to URLs; this one is reached
    // at /Account/Login, /Account/Register, etc.
    public class AccountController : Controller
    {
        // Hardcoded administrator credentials. This is a simple shortcut for a course project:
        // logging in with these exact values grants admin rights without a database user.
        // In a real app you would never hardcode credentials in source — they belong in
        // configuration/secrets, and the admin would be a proper role on a real account.
        private const string AdminUsername = "admin";
        private const string AdminPassword = "admin123";

        // The data-access service (see Services/ApiClient.cs), injected by DI.
        private readonly ApiClient _api;

        // Constructor injection: the framework supplies the ApiClient. The "=>" is an
        // expression-bodied constructor, shorthand for { _api = api; }.
        public AccountController(ApiClient api) => _api = api;

        // GET /Account/Login — just show the empty login form, remembering where to return to.
        [HttpGet]
        public IActionResult Login(string? returnUrl = null) =>
            View(new LoginViewModel { ReturnUrl = returnUrl });

        // POST /Account/Login — process the submitted form.
        // [ValidateAntiForgeryToken] checks the hidden anti-forgery token the form includes,
        // protecting against CSRF (cross-site request forgery) attacks.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // ModelState reflects the data-annotation validation on LoginViewModel. If the
            // input is invalid, redisplay the form (with the validation messages).
            if (!ModelState.IsValid) return View(model);

            // First check the hardcoded admin shortcut.
            if (model.Username == AdminUsername && model.Password == AdminPassword)
            {
                SignIn(AdminUsername, isAdmin: true);
                return RedirectToLocalOr(model.ReturnUrl, "Admin", "Index");
            }

            // Otherwise validate against the database via the data service.
            // NOTE: model.Password is the RAW password here — ApiClient.ValidateAsync compares
            // it directly to the stored value (the MVC path does not hash; see ApiClient.cs).
            var (user, error) = await _api.ValidateAsync(model.Username, model.Password);
            if (user is not null)
            {
                SignIn(user.Username, isAdmin: false);
                return RedirectToLocalOr(model.ReturnUrl, "Cars", "Index");
            }

            // Validation failed: surface the error at the top of the form (key = string.Empty
            // means a model-level error, not tied to one field).
            ModelState.AddModelError(string.Empty, error ?? "Invalid username or password.");
            return View(model);
        }

        // GET /Account/Register — show the empty registration form.
        [HttpGet]
        public IActionResult Register() => View(new RegisterViewModel());

        // POST /Account/Register — create the account.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Again the RAW password is passed through; ApiClient stores it as-is.
            var (user, error) = await _api.RegisterAsync(model.Username, model.Email, model.Password);
            if (user is null)
            {
                ModelState.AddModelError(string.Empty, error ?? "Registration failed.");
                return View(model);
            }

            // Auto-login the new user, then use TempData to pass a one-time success message
            // that survives the redirect (TempData persists for exactly the next request).
            SignIn(user.Username, isAdmin: false);
            TempData["Success"] = $"Welcome, {user.Username}! Your account has been created.";
            return RedirectToAction("Index", "Cars");
        }

        // POST /Account/Logout — end the session.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            // Clearing the session removes the Username/IsAdmin keys, so the user is logged out.
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Cars");
        }

        // Helper that records the signed-in identity in the session. The filters
        // (LoggedInOnly/AdminOnly) and the layout read these keys later.
        private void SignIn(string username, bool isAdmin)
        {
            HttpContext.Session.SetString(SessionKeys.Username, username);
            HttpContext.Session.SetString(SessionKeys.IsAdmin, isAdmin ? "true" : "false");
        }

        // Helper implementing the safe "return URL" redirect. Url.IsLocalUrl is a SECURITY
        // check: it ensures we only redirect to a path on our own site, never to an external
        // URL an attacker might have injected (an "open redirect" vulnerability). If there is
        // no safe return URL, fall back to the given controller/action.
        private IActionResult RedirectToLocalOr(string? returnUrl, string controller, string action)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(action, controller);
        }
    }
}
