using Microsoft.AspNetCore.Mvc;
using WEBTechnologies_Final.Data;
using WEBTechnologies_Final.Models;
using WEBTechnologies_Final.Services;

namespace WEBTechnologies_Final.Controllers
{
    // REST API for user accounts: register and validate (login) over JSON.
    //
    // IMPORTANT STUDY POINT — inconsistent password handling across the two code paths:
    //   * THIS API correctly HASHES: it stores PasswordHasher.Hash(password) on register and
    //     checks PasswordHasher.Verify(...) on validate.
    //   * The MVC path (AccountController -> ApiClient) does NOT hash: it stores and compares
    //     the raw password (see ApiClient.RegisterAsync/ValidateAsync).
    // Because of this mismatch, an account created through the web pages (raw password) would
    // fail to validate through this API (which hashes before comparing), and vice versa. In a
    // correct design both paths would share one hashing implementation.
    [ApiController]
    [Route("api/users")]
    public class UsersApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        public UsersApiController(AppDbContext db) => _db = db;

        // POST /api/users/register — body is JSON ({ "username":..., "email":..., "password":... }).
        // [FromBody] deserializes that JSON into the RegisterRequest record.
        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest req)
        {
            var username = req.Username.Trim();
            var email = req.Email.Trim();

            // Reject duplicates (case-insensitive). Conflict() returns HTTP 409.
            if (_db.Users.Any(u => u.Username.ToLower() == username.ToLower()))
                return Conflict(new { error = "That username is already taken." });

            if (_db.Users.Any(u => u.Email.ToLower() == email.ToLower()))
                return Conflict(new { error = "An account with that email already exists." });

            var user = new User
            {
                Username = username,
                Email = email,
                // Hash the password before storing — this is the correct approach.
                PasswordHash = PasswordHasher.Hash(req.Password),
                RegisteredUtc = DateTime.UtcNow
            };
            _db.Users.Add(user);
            _db.SaveChanges();

            // Return a minimal projection (never the password hash) with HTTP 200.
            return Ok(new { user.Id, user.Username, user.Email });
        }

        // POST /api/users/validate — check credentials.
        [HttpPost("validate")]
        public IActionResult Validate([FromBody] ValidateRequest req)
        {
            var user = _db.Users.FirstOrDefault(u => u.Username.ToLower() == req.Username.Trim().ToLower());
            // Verify hashes the incoming password and compares it to the stored hash.
            // One generic message for both "no such user" and "wrong password" avoids leaking
            // which usernames exist. Unauthorized() returns HTTP 401.
            if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
                return Unauthorized(new { error = "Invalid username or password." });

            return Ok(new { user.Id, user.Username, user.Email });
        }
    }

    // Request bodies for the two endpoints, as compact immutable records. Property names map
    // to the incoming JSON fields (case-insensitively by default).
    public record RegisterRequest(string Username, string Email, string Password);
    public record ValidateRequest(string Username, string Password);
}
