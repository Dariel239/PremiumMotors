using Microsoft.AspNetCore.Mvc;
using WEBTechnologies_Final.Services;

namespace WEBTechnologies_Final.Controllers
{
    // The MVC pages for a user's saved cars. [LoggedInOnly] on the class means you must be
    // signed in to reach any action here.
    [LoggedInOnly]
    public class FavoritesController : Controller
    {
        private readonly ApiClient _api;

        public FavoritesController(ApiClient api) => _api = api;

        // Convenience property for the current user's name from the session.
        // The "!" asserts non-null, which is safe because [LoggedInOnly] guarantees a login.
        private string CurrentUser => HttpContext.Session.GetString(SessionKeys.Username)!;

        // GET /Favorites — show the current user's favourited cars, newest first.
        public async Task<IActionResult> Index()
        {
            // Get the ids this user favourited, then load each car.
            var ids = await _api.GetFavoriteIdsAsync(CurrentUser);
            var cars = new List<Models.Car>();
            foreach (var id in ids)
            {
                var car = await _api.GetCarAsync(id);
                if (car is not null) cars.Add(car);
            }
            // NOTE: this loads cars one-by-one in a loop (an "N+1" query pattern). It's fine
            // for small data, but a single query filtering by the id list would scale better.
            return View(cars.OrderByDescending(c => c.CreatedUtc).ToList());
        }

        // POST /Favorites/Toggle — add or remove a car from favourites, then return the user
        // to where they were (returnUrl) or to the car's details page.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id, string? returnUrl = null)
        {
            var car = await _api.GetCarAsync(id);
            if (car is null) return NotFound();

            // ToggleFavoriteAsync returns true if the car is now a favourite, false if removed.
            var nowFavorite = await _api.ToggleFavoriteAsync(CurrentUser, id);
            TempData["Success"] = nowFavorite
                ? "Added to your favourites."
                : "Removed from your favourites.";

            // Safe local redirect (Url.IsLocalUrl guards against open-redirect to other sites).
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Details", "Cars", new { id });
        }
    }
}
