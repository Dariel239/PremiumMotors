using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using WEBTechnologies_Final.Data;
using WEBTechnologies_Final.Models;

namespace WEBTechnologies_Final.Services
{
    // ApiClient is the application's data-access layer: every controller goes through it to
    // read or write data, instead of touching the DbContext directly. Centralising the data
    // logic here keeps the controllers thin and avoids duplicating queries.
    //
    // NOTE ON THE NAME (important for understanding the design): despite being called
    // "ApiClient" and being registered with AddHttpClient in Program.cs (so it receives an
    // HttpClient), this class does NOT actually make HTTP calls. It talks straight to the
    // database via AppDbContext. The matching Web API controllers (CarsApiController, etc.)
    // expose the same data over REST for the Swagger/API side of the project, but the MVC
    // pages call this class in-process for speed and simplicity. The injected HttpClient is
    // effectively unused — a leftover from a design where the MVC site called its own API
    // over the network.
    public class ApiClient
    {
        // The EF Core context used for all queries below. Injected per request.
        private readonly AppDbContext _context;

        // DI supplies both an HttpClient (because of AddHttpClient<ApiClient>) and the
        // AppDbContext. We keep only the context; "http" is intentionally not stored
        // because, as noted above, this class queries the database directly.
        public ApiClient(HttpClient http, AppDbContext context)
        {
            _context = context;
        }

        // ---------- Reads: listing & searching cars ----------

        // Returns cars matching the optional filters, in the requested sort order.
        // Builds the query step by step (composable LINQ) and only hits the database at the
        // very end with ToListAsync — so unused filters add no SQL.
        public async Task<List<Car>> GetCarsAsync(
            string? search = null, string? type = null, string? make = null,
            string? model = null, int? year = null, string? sortBy = null)
        {
            // Include(c => c.Bids) eager-loads each car's bids in the same round trip, so
            // Car.CurrentPrice/HighestBid work without a second query. AsQueryable lets us
            // keep appending Where/OrderBy clauses below before execution.
            var query = _context.Cars.Include(c => c.Bids).AsQueryable();

            // Each filter is applied only when a value was provided. EF translates the
            // string .Contains(...) into a SQL LIKE '%search%'.
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Make.Contains(search) || c.Model.Contains(search) || c.Description.Contains(search));
            }
            // Enum.TryParse turns the incoming text (e.g. "SUV") into the CarType enum;
            // "true" makes it case-insensitive. We only filter if parsing succeeded.
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<CarType>(type, true, out var parsedType))
            {
                query = query.Where(c => c.Type == parsedType);
            }
            if (!string.IsNullOrEmpty(make))
            {
                query = query.Where(c => c.Make == make);
            }
            if (!string.IsNullOrEmpty(model))
            {
                query = query.Where(c => c.Model == model);
            }
            if (year.HasValue)
            {
                query = query.Where(c => c.Year == year.Value);
            }

            // A switch expression maps the sort key to an OrderBy clause. The "_" (discard)
            // is the default case: newest first, by descending Id.
            query = sortBy switch
            {
                "price_asc" => query.OrderBy(c => c.StartingPrice),
                "price_desc" => query.OrderByDescending(c => c.StartingPrice),
                "year_asc" => query.OrderBy(c => c.Year),
                "year_desc" => query.OrderByDescending(c => c.Year),
                _ => query.OrderByDescending(c => c.Id)
            };

            // Execute the composed query and materialise the rows into Car objects.
            return await query.ToListAsync();
        }

        // Loads one car (with its bids) by id, or null if there is no such car.
        // FirstOrDefaultAsync returns the first match or default(null) — no exception.
        public async Task<Car?> GetCarAsync(int id)
        {
            return await _context.Cars
                .Include(c => c.Bids)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        // ---------- Reads: distinct values for the filter dropdowns ----------

        // All distinct makes, alphabetically — used to populate the "Make" filter.
        public async Task<List<string>> GetMakesAsync() =>
            await _context.Cars.Select(c => c.Make).Distinct().OrderBy(m => m).ToListAsync();

        // Distinct models, optionally narrowed to a single make (so the Model dropdown can
        // depend on the chosen Make).
        public async Task<List<string>> GetModelsAsync(string? make = null)
        {
            var query = _context.Cars.AsQueryable();
            if (make is not null)
            {
                query = query.Where(c => c.Make == make);
            }
            return await query.Select(c => c.Model).Distinct().OrderBy(m => m).ToListAsync();
        }

        // Distinct years, newest first.
        public async Task<List<int>> GetYearsAsync() =>
            await _context.Cars.Select(c => c.Year).Distinct().OrderByDescending(y => y).ToListAsync();

        // ---------- Writes: admin CRUD on cars ----------

        // Insert a new car. Add stages it; SaveChangesAsync runs the INSERT and fills in the
        // database-generated Id, which is then on the returned object.
        public async Task<Car?> CreateCarAsync(Car car)
        {
            _context.Cars.Add(car);
            await _context.SaveChangesAsync();
            return car;
        }

        // Update an existing car. We load the tracked "existing" row, then copy the incoming
        // values onto it with SetValues (which only marks genuinely changed columns as
        // modified). Returns null if the car id doesn't exist.
        public async Task<Car?> UpdateCarAsync(Car car)
        {
            var existing = await _context.Cars.FirstOrDefaultAsync(c => c.Id == car.Id);
            if (existing is null) return null;

            _context.Entry(existing).CurrentValues.SetValues(car);
            await _context.SaveChangesAsync();
            return existing;
        }

        // Delete a car by id. Returns false if not found. Because of the cascade configured
        // in AppDbContext, the car's bids are removed automatically.
        public async Task<bool> DeleteCarAsync(int id)
        {
            var car = await _context.Cars.FirstOrDefaultAsync(c => c.Id == id);
            if (car is null) return false;

            _context.Cars.Remove(car);
            await _context.SaveChangesAsync();
            return true;
        }

        // ---------- Writes: bidding & buying ----------

        // Place a bid. The return type is a tuple (car, error): on success error is null and
        // car is the updated car; on failure car is null and error holds a user-facing
        // message. This pattern lets callers handle the failure without try/catch.
        public async Task<(Car? car, string? error)> PlaceBidAsync(int id, string bidderName, decimal amount)
        {
            var car = await _context.Cars.Include(c => c.Bids).FirstOrDefaultAsync(c => c.Id == id);
            if (car is null) return (null, "Car listing not found.");

            // The current price is the highest existing bid, or the starting price if none.
            decimal currentPrice = car.Bids.Any() ? car.Bids.Max(b => b.Amount) : car.StartingPrice;

            // A valid bid must beat the current price. "{currentPrice:C}" formats as currency
            // using the app's culture (euros — configured in Program.cs).
            if (amount <= currentPrice)
            {
                return (null, $"Your bid must be higher than the current price of {currentPrice:C}");
            }

            var newBid = new Bid
            {
                CarId = id,
                Amount = amount,
                BidderUsername = bidderName,
                CreatedUtc = DateTime.UtcNow
            };
            _context.Bids.Add(newBid);

            // If the bid meets or exceeds the instant-buy price, the auction ends immediately
            // and the bidder becomes the buyer.
            if (car.InstantBuyPrice.HasValue && amount >= car.InstantBuyPrice.Value)
            {
                car.IsSold = true;
                car.SoldTo = bidderName;
            }

            // One SaveChanges commits both the new bid and any sold-state change together.
            await _context.SaveChangesAsync();
            return (car, null);
        }

        // "Buy it now": purchase immediately at the instant-buy price, skipping bidding.
        public async Task<(Car? car, string? error)> BuyNowAsync(int id, string buyerName)
        {
            var car = await _context.Cars.FirstOrDefaultAsync(c => c.Id == id);
            if (car is null) return (null, "Car listing not found.");

            // Guard: can't buy something already sold or that never offered instant buy.
            if (car.IsSold || !car.InstantBuyPrice.HasValue)
            {
                return (null, "This item is no longer available for instant purchase.");
            }

            car.IsSold = true;
            car.SoldTo = buyerName;

            // Record the purchase as a bid at the instant-buy price, so the bid history and
            // "current price" stay consistent.
            var buyingBid = new Bid
            {
                CarId = id,
                Amount = car.InstantBuyPrice.Value,
                BidderUsername = buyerName,
                CreatedUtc = DateTime.UtcNow
            };
            _context.Bids.Add(buyingBid);

            await _context.SaveChangesAsync();
            return (car, null);
        }

        // Admin action: end an auction now by stamping its end time. Car.IsClosed then
        // becomes true because AuctionEnd is in the past.
        public async Task<bool> CloseAuctionAsync(int id)
        {
            var car = await _context.Cars.FirstOrDefaultAsync(c => c.Id == id);
            if (car is null) return false;

            car.AuctionEnd = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        // Dashboard numbers for the admin home page. Counts are computed in the database
        // (CountAsync) rather than by loading all rows.
        public async Task<CarStats?> GetStatsAsync()
        {
            var total = await _context.Cars.CountAsync();
            var sold = await _context.Cars.CountAsync(c => c.IsSold);
            var active = total - sold;
            var totalBids = await _context.Bids.CountAsync();

            return new CarStats(total, active, sold, totalBids);
        }

        // ---------- Writes/reads: accounts ----------

        // Register a new account. Returns (user, error) like the bidding methods.
        public async Task<(UserDto? user, string? error)> RegisterAsync(
            string username, string email, string password)
        {
            // Normalise to lower case for the duplicate check so "Alice" and "alice" collide.
            var normalizedUsername = username.Trim().ToLower();
            var normalizedEmail = email.Trim().ToLower();

            // Reject if the username OR email already exists. (The unique indexes in the DB
            // are the ultimate safeguard; this gives a friendly message first.)
            var exists = await _context.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername || u.Email.ToLower() == normalizedEmail);
            if (exists) return (null, "Username or Email already registered.");

            // STUDY CAVEAT: the "password" argument arrives as RAW text — AccountController
            // does NOT hash before calling this — and it is stored straight into PasswordHash
            // unhashed. So web-registered accounts have plain-text passwords in the database.
            // (The API path in UsersApiController does hash; the two paths are inconsistent.
            // The fix would be to call PasswordHasher.Hash(password) here too.)
            var newUser = new User
            {
                Username = username,
                Email = email,
                PasswordHash = password
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Return a lightweight DTO (no password hash) rather than the full entity.
            return (new UserDto(newUser.Id, newUser.Username, newUser.Email), null);
        }

        // Verify login credentials. Consistent with RegisterAsync above, "password" is RAW
        // text and is compared directly to the stored value (plain-text comparison on the MVC
        // path). If hashing were added on register, it would need to be added here too.
        public async Task<(UserDto? user, string? error)> ValidateAsync(
            string username, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.Trim().ToLower());
            // Same generic message whether the user is missing or the password is wrong —
            // this avoids revealing which usernames exist.
            if (user is null || user.PasswordHash != password)
            {
                return (null, "Invalid username or password.");
            }

            return (new UserDto(user.Id, user.Username, user.Email), null);
        }

        // ---------- Favourites ----------

        // The car ids this user has favourited (used to pre-tick hearts across listings).
        public async Task<List<int>> GetFavoriteIdsAsync(string username)
        {
            return await _context.UserFavoriteCars
                .Where(f => f.Username.ToLower() == username.Trim().ToLower())
                .Select(f => f.CarId)
                .ToListAsync();
        }

        // Whether a specific car is favourited by this user.
        public async Task<bool> IsFavoriteAsync(string username, int carId)
        {
            return await _context.UserFavoriteCars
                .AnyAsync(f => f.Username.ToLower() == username.Trim().ToLower() && f.CarId == carId);
        }

        // Toggle a favourite on/off. If the link row exists we remove it (returns false =
        // "now not a favourite"); otherwise we add it (returns true = "now a favourite").
        public async Task<bool> ToggleFavoriteAsync(string username, int carId)
        {
            var formattedName = username.Trim();
            var existing = await _context.UserFavoriteCars
                .FirstOrDefaultAsync(f => f.Username.ToLower() == formattedName.ToLower() && f.CarId == carId);

            if (existing is not null)
            {
                _context.UserFavoriteCars.Remove(existing);
                await _context.SaveChangesAsync();
                return false;
            }

            _context.UserFavoriteCars.Add(new UserFavoriteCar { Username = formattedName, CarId = carId });
            await _context.SaveChangesAsync();
            return true;
        }
    }

    // Small immutable "data transfer objects" returned to callers/serialized by the API.
    // A C# record gives value-based equality and a compact constructor for free. Using DTOs
    // instead of the entities means we never accidentally leak fields like PasswordHash.
    public record UserDto(int Id, string Username, string Email);
    public record CarStats(int Total, int Active, int Sold, int TotalBids);
}
