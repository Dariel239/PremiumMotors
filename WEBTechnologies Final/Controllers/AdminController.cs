using Microsoft.AspNetCore.Mvc;
using WEBTechnologies_Final.Models;
using WEBTechnologies_Final.Services;

namespace WEBTechnologies_Final.Controllers
{
    // The admin back-office: create / edit / delete listings, close auctions, view stats.
    // [AdminOnly] on the CLASS applies the admin check to EVERY action here, so a single
    // attribute protects the whole controller (no per-action repetition).
    [AdminOnly]
    public class AdminController : Controller
    {
        private readonly ApiClient _api;

        // IWebHostEnvironment gives access to environment info, notably WebRootPath
        // (the absolute path to wwwroot) which we need when saving uploaded photos to disk.
        private readonly IWebHostEnvironment _env;

        // The image file types we accept for uploads. "static readonly" = one shared,
        // immutable array for the whole class.
        private static readonly string[] AllowedImageExtensions =
            { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        public AdminController(ApiClient api, IWebHostEnvironment env)
        {
            _api = api;
            _env = env;
        }

        // GET /Admin or /Admin/Index — dashboard: all cars + summary statistics.
        public async Task<IActionResult> Index()
        {
            var cars = await _api.GetCarsAsync();
            var stats = await _api.GetStatsAsync();
            // Pass the stats to the view via ViewData (the cars go through the model).
            ViewData["Stats"] = stats;
            return View(cars);
        }

        // GET /Admin/Create — blank "new listing" form, defaulting the year to this year.
        public IActionResult Create() =>
            View(new Car { Year = DateTime.UtcNow.Year });

        // POST /Admin/Create — save a new listing.
        // Model binding maps the form fields onto "car"; the file inputs map onto "photos".
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Car car, List<IFormFile>? photos)
        {
            if (!ModelState.IsValid) return View(car);
            // Save any uploaded images to disk and record their web paths on the car.
            car.ImagePaths = await SavePhotosAsync(photos);
            await _api.CreateCarAsync(car);
            TempData["Success"] = $"\"{car.Title}\" was posted to the auction.";
            return RedirectToAction(nameof(Index));
        }

        // GET /Admin/Edit/5 — load a car into the edit form, or 404 if missing.
        public async Task<IActionResult> Edit(int id)
        {
            var car = await _api.GetCarAsync(id);
            return car is null ? NotFound() : View(car);
        }

        // POST /Admin/Edit/5 — save changes.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Car car, List<IFormFile>? photos)
        {
            // Defensive check: the id in the route must match the id in the posted model.
            if (id != car.Id) return BadRequest();
            if (!ModelState.IsValid) return View(car);
            // NOTE: this replaces ImagePaths with only the newly uploaded photos. If no new
            // files are chosen, SavePhotosAsync returns an empty list, effectively clearing
            // the existing images — worth knowing when editing a listing.
            car.ImagePaths = await SavePhotosAsync(photos);
            var result = await _api.UpdateCarAsync(car);
            if (result is null) return NotFound();
            TempData["Success"] = $"\"{car.Title}\" was updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET /Admin/Delete/5 — show a confirmation page first (don't delete on a GET).
        public async Task<IActionResult> Delete(int id)
        {
            var car = await _api.GetCarAsync(id);
            return car is null ? NotFound() : View(car);
        }

        // POST /Admin/Delete/5 — actually delete after confirmation.
        // [HttpPost, ActionName("Delete")] means: this method handles the POST to /Admin/Delete
        // even though the C# method is named DeleteConfirmed. The different method name is
        // needed because C# can't have two methods named "Delete" with the same parameters.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await _api.DeleteCarAsync(id)) return NotFound();
            TempData["Success"] = "Listing deleted.";
            return RedirectToAction(nameof(Index));
        }

        // POST /Admin/Close/5 — end an auction now. Sets the appropriate TempData key
        // ("Success" or "Error") using a conditional, so the view shows the right banner.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id)
        {
            var success = await _api.CloseAuctionAsync(id);
            TempData[success ? "Success" : "Error"] = success
                ? "Auction closed successfully."
                : "Could not close the auction.";
            return RedirectToAction(nameof(Index));
        }

        // Saves uploaded images to wwwroot/uploads/cars and returns their web-relative paths.
        private async Task<List<string>> SavePhotosAsync(List<IFormFile>? photos)
        {
            var saved = new List<string>();
            if (photos is null || photos.Count == 0) return saved;

            // Resolve the physical uploads folder and make sure it exists.
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadDir = Path.Combine(webRoot, "uploads", "cars");
            Directory.CreateDirectory(uploadDir);

            foreach (var photo in photos)
            {
                if (photo.Length == 0) continue;

                // Only allow whitelisted image extensions — a basic safeguard against users
                // uploading arbitrary/executable files.
                var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
                if (!AllowedImageExtensions.Contains(ext)) continue;

                // Generate a unique, random file name (Guid "N" format = 32 hex chars, no
                // dashes) so uploads never collide or overwrite each other, and the user's
                // original file name can't be used to probe the server.
                var fileName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(uploadDir, fileName);

                // "await using" disposes the file stream asynchronously once copying is done.
                await using var stream = new FileStream(fullPath, FileMode.Create);
                await photo.CopyToAsync(stream);

                // Store the URL form (what the browser requests), not the disk path.
                saved.Add($"/uploads/cars/{fileName}");
            }

            return saved;
        }
    }
}
