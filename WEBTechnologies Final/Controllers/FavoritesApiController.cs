using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBTechnologies_Final.Data;
using WEBTechnologies_Final.Models;

namespace WEBTechnologies_Final.Controllers
{
    // REST/JSON API for favourites — the API counterpart to the MVC FavoritesController.
    // Like the other API controllers it uses the DbContext directly and returns JSON/status
    // codes. It identifies the user by a username in the URL (no session), so treat it as an
    // internal/demo API rather than a secured endpoint.
    [ApiController]
    [Route("api/favorites")]
    public class FavoritesApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        public FavoritesApiController(AppDbContext db) => _db = db;

        // GET /api/favorites/alice — the car ids this user has favourited.
        [HttpGet("{username}")]
        public IActionResult GetFavorites(string username)
        {
            if (string.IsNullOrEmpty(username)) return BadRequest("Username is required.");

            var carIds = _db.UserFavoriteCars
                .Where(f => f.Username.ToLower() == username.Trim().ToLower())
                .Select(f => f.CarId)
                .ToList();

            return Ok(carIds);
        }

        // GET /api/favorites/alice/5 — is car 5 a favourite of alice? Returns { isFavorite: bool }.
        [HttpGet("{username}/{carId}")]
        public IActionResult IsFavorite(string username, int carId)
        {
            if (string.IsNullOrEmpty(username)) return BadRequest("Username is required.");

            var isFav = _db.UserFavoriteCars.Any(f =>
                f.Username.ToLower() == username.Trim().ToLower() &&
                f.CarId == carId
            );

            return Ok(new { isFavorite = isFav });
        }

        // POST /api/favorites/alice/5/toggle — add the favourite if absent, remove it if
        // present. Returns the resulting state as { isFavorite: bool }.
        [HttpPost("{username}/{carId}/toggle")]
        public IActionResult Toggle(string username, int carId)
        {
            if (string.IsNullOrEmpty(username)) return BadRequest("Username is required.");

            var formattedUsername = username.Trim();

            // Look for an existing (user, car) favourite row.
            var existing = _db.UserFavoriteCars.FirstOrDefault(f =>
                f.Username.ToLower() == formattedUsername.ToLower() &&
                f.CarId == carId
            );

            if (existing is not null)
            {
                // Already a favourite -> remove it.
                _db.UserFavoriteCars.Remove(existing);
                _db.SaveChanges();
                return Ok(new { isFavorite = false });
            }

            // Not yet a favourite -> add it.
            _db.UserFavoriteCars.Add(new UserFavoriteCar
            {
                Username = formattedUsername,
                CarId = carId
            });

            _db.SaveChanges();
            return Ok(new { isFavorite = true });
        }
    }
}
