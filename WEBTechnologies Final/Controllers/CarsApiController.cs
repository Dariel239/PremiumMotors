using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBTechnologies_Final.Data;
using WEBTechnologies_Final.Models;

namespace WEBTechnologies_Final.Controllers.Api
{
    // A REST/JSON Web API for cars — the counterpart to the MVC CarsController, exposing the
    // same data as JSON endpoints (browsable via Swagger, see Program.cs). It returns data,
    // not HTML views, so it derives from ControllerBase (no View() support) rather than
    // Controller.
    //
    // [ApiController] opts in to API conventions: automatic model validation, automatic 400
    // responses for bad input, and [FromBody] inference.
    // [Route("api/cars")] sets the base URL; the action attributes append to it.
    [ApiController]
    [Route("api/cars")]
    public class CarsApiController : ControllerBase
    {
        // This API talks to EF Core directly (it does not go through ApiClient), which is why
        // the querying logic below mirrors what ApiClient/CarsController already do.
        private readonly AppDbContext _context;

        public CarsApiController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/cars?search=...&type=...  — list/filter/sort cars.
        // [FromQuery] binds each parameter from the URL query string. ActionResult<T> lets the
        // method return either the data (serialized to JSON) or a status result like NotFound.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Car>>> GetCars(
            [FromQuery] string? search,
            [FromQuery] CarType? type,
            [FromQuery] string? make,
            [FromQuery] string? model,
            [FromQuery] int? year,
            [FromQuery] string sortBy = "newest")
        {
            var query = _context.Cars.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Make.Contains(search) || c.Model.Contains(search) || c.Description.Contains(search));
            }
            if (type.HasValue)
            {
                query = query.Where(c => c.Type == type.Value);
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

            query = sortBy switch
            {
                "price_asc" => query.OrderBy(c => c.StartingPrice),
                "price_desc" => query.OrderByDescending(c => c.StartingPrice),
                "year_asc" => query.OrderBy(c => c.Year),
                "year_desc" => query.OrderByDescending(c => c.Year),
                _ => query.OrderByDescending(c => c.Id)
            };

            return await query.ToListAsync();
        }

        // GET /api/cars/5 — one car with its bids, or 404. The "{id}" token in the route maps
        // to the method's id parameter.
        [HttpGet("{id}")]
        public async Task<ActionResult<Car>> GetCar(int id)
        {
            var car = await _context.Cars
                .Include(c => c.Bids)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (car is null) return NotFound();
            return car;
        }

        // GET /api/cars/makes — distinct makes for building client-side filters.
        [HttpGet("makes")]
        public async Task<ActionResult<IEnumerable<string>>> GetMakes()
        {
            return await _context.Cars
                .Select(c => c.Make)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();
        }

        // GET /api/cars/models — distinct models.
        [HttpGet("models")]
        public async Task<ActionResult<IEnumerable<string>>> GetModels()
        {
            return await _context.Cars
                .Select(c => c.Model)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();
        }

        // GET /api/cars/years — distinct years, newest first.
        [HttpGet("years")]
        public async Task<ActionResult<IEnumerable<int>>> GetYears()
        {
            return await _context.Cars
                .Select(c => c.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
        }

        // GET /api/cars/5/is-favorite?username=alice — whether a user favourited this car.
        [HttpGet("{id}/is-favorite")]
        public async Task<ActionResult<bool>> IsFavorite(int id, [FromQuery] string username)
        {
            if (string.IsNullOrEmpty(username)) return BadRequest("Username is required.");

            return await _context.UserFavoriteCars
                .AnyAsync(f => f.Username == username && f.CarId == id);
        }

        // POST /api/cars/5/bid?username=alice&amount=5000 — place a bid via the API.
        // Mirrors CarsController.Bid but returns JSON/status codes instead of redirects.
        // NOTE: unlike the MVC action, this API trusts the "username" passed in the query —
        // there is no session/auth here, so it should be treated as an internal/demo endpoint.
        [HttpPost("{id}/bid")]
        public async Task<ActionResult<Car>> PlaceBid(int id, [FromQuery] string username, [FromQuery] decimal amount)
        {
            if (string.IsNullOrEmpty(username)) return BadRequest("Username is required.");

            var car = await _context.Cars.Include(c => c.Bids).FirstOrDefaultAsync(c => c.Id == id);
            if (car is null) return NotFound("Car listing not found.");

            decimal currentPrice = car.Bids.Any() ? car.Bids.Max(b => b.Amount) : car.StartingPrice;

            if (amount <= currentPrice)
            {
                return BadRequest($"Bid must be higher than current price of {currentPrice:C}");
            }

            var bid = new Bid
            {
                CarId = id,
                Amount = amount,
                BidderUsername = username,
                CreatedUtc = DateTime.UtcNow
            };

            _context.Bids.Add(bid);

            if (car.InstantBuyPrice.HasValue && amount >= car.InstantBuyPrice.Value)
            {
                car.IsSold = true;
                car.SoldTo = username;
            }

            await _context.SaveChangesAsync();
            return Ok(car);   // 200 with the updated car as JSON
        }
    }
}
