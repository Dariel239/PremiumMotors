using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using WEBTechnologies_Final.Models;
using WEBTechnologies_Final.Services;
using WEBTechnologies_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace WEBTechnologies_Final.Controllers
{
    // The public storefront: browsing the catalogue, viewing a car, bidding and buying.
    // This is also the app's default landing controller (see the route in Program.cs).
    //
    // STUDY NOTE: this controller injects BOTH the ApiClient data service AND the raw
    // AppDbContext, and several actions query _context directly — duplicating logic that
    // already exists in ApiClient (GetCarsAsync, PlaceBidAsync, BuyNowAsync). A cleaner
    // design would route all data access through _api so the rules live in one place. It is
    // left as-is here so you can see both styles side by side.
    public class CarsController : Controller
    {
        private readonly ApiClient _api;
        private readonly AppDbContext _context;

        public CarsController(ApiClient api, AppDbContext context)
        {
            _api = api;
            _context = context;
        }

        // GET / or /Cars or /Cars/Index — the catalogue with search/filter/sort.
        // Action parameters are bound automatically from the query string
        // (e.g. /Cars?search=audi&type=SUV&sortBy=price_asc).
        public async Task<IActionResult> Index(
            string? search, CarType? type, string? make,
            string? model, int? year, string sortBy = "newest")
        {
            // Build the filtered query step by step (same composable-LINQ pattern as ApiClient).
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

            var cars = await query.ToListAsync();

            // Distinct values that populate the filter dropdowns.
            var makes = await _context.Cars.Select(c => c.Make).Distinct().OrderBy(m => m).ToListAsync();
            var models = await _context.Cars.Select(c => c.Model).Distinct().OrderBy(m => m).ToListAsync();
            var years = await _context.Cars.Select(c => c.Year).Distinct().OrderByDescending(y => y).ToListAsync();

            // Assemble the view model. SelectList(items, selectedValue) marks the current
            // choice as selected so the form remembers what the user picked.
            var vm = new CarListViewModel
            {
                Cars = cars,
                Search = search,
                Type = type,
                Make = make,
                Model = model,
                Year = year,
                SortBy = sortBy,
                TypeOptions = BuildTypeOptions(type),
                MakeOptions = new SelectList(makes, make),
                ModelOptions = new SelectList(models, model),
                YearOptions = new SelectList(years, year),
                SortOptions = BuildSortOptions(sortBy)
            };

            return View(vm);
        }

        // GET /Cars/Details/5 — one car plus its bids.
        public async Task<IActionResult> Details(int id)
        {
            var car = await _context.Cars
                .Include(c => c.Bids)
                .FirstOrDefaultAsync(c => c.Id == id);

            // NotFound() returns HTTP 404 when the id doesn't exist.
            if (car is null) return NotFound();

            // If someone is logged in, pre-compute whether this car is one of their favourites
            // and stash it in ViewData. The view's heart button reads ViewData["IsFav_{id}"]
            // so it can render filled/empty without doing its own async query.
            var username = HttpContext.Session.GetString(SessionKeys.Username);
            if (username is not null)
            {
                ViewData[$"IsFav_{car.Id}"] = await _context.UserFavoriteCars
                    .AnyAsync(f => f.Username == username && f.CarId == id);
            }
            return View(car);
        }

        // POST /Cars/Bid — place a bid. [LoggedInOnly] forces sign-in first.
        [HttpPost]
        [LoggedInOnly]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Bid(int id, decimal amount)
        {
            // The "!" is the null-forgiving operator: [LoggedInOnly] guarantees a username
            // exists, so we assert it is non-null to the compiler.
            var bidderName = HttpContext.Session.GetString(SessionKeys.Username)!;

            var car = await _context.Cars.Include(c => c.Bids).FirstOrDefaultAsync(c => c.Id == id);
            if (car is null) return NotFound();

            // Must beat the current price (highest bid, or starting price if none yet).
            decimal currentPrice = car.Bids.Any() ? car.Bids.Max(b => b.Amount) : car.StartingPrice;

            // On a bad bid, store an error in TempData and redirect back to the details page
            // (the Post/Redirect/Get pattern: redirecting after a POST avoids the browser
            // re-submitting the form if the user refreshes).
            if (amount <= currentPrice)
            {
                TempData["Error"] = $"Your bid must be higher than the current price of {currentPrice:C}";
                return RedirectToAction(nameof(Details), new { id });
            }

            var newBid = new Bid
            {
                CarId = id,
                Amount = amount,
                BidderUsername = bidderName,
                CreatedUtc = DateTime.UtcNow
            };

            _context.Bids.Add(newBid);

            // Reaching the instant-buy price ends the auction immediately.
            if (car.InstantBuyPrice.HasValue && amount >= car.InstantBuyPrice.Value)
            {
                car.IsSold = true;
                car.SoldTo = bidderName;
                TempData["Success"] = $"Your bid of {amount:C} hit the instant-buy price — the car is yours!";
            }
            else
            {
                TempData["Success"] = $"Bid of {amount:C} placed successfully. You are the highest bidder!";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Cars/Buy — instant purchase at the buy-now price.
        [HttpPost]
        [LoggedInOnly]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(int id)
        {
            var buyerName = HttpContext.Session.GetString(SessionKeys.Username)!;

            var car = await _context.Cars.FirstOrDefaultAsync(c => c.Id == id);
            if (car is null) return NotFound();

            // Can't buy if already sold or no instant-buy price was set.
            if (car.IsSold || !car.InstantBuyPrice.HasValue)
            {
                TempData["Error"] = "This item is no longer available for instant purchase.";
                return RedirectToAction(nameof(Details), new { id });
            }

            car.IsSold = true;
            car.SoldTo = buyerName;

            // Record the purchase as a bid at the buy-now price (keeps history consistent).
            var buyingBid = new Bid
            {
                CarId = id,
                Amount = car.InstantBuyPrice.Value,
                BidderUsername = buyerName,
                CreatedUtc = DateTime.UtcNow
            };
            _context.Bids.Add(buyingBid);

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Congratulations! You bought the car for {car.InstantBuyPrice:C}.";

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET /Cars/Share/5 — a shareable page. Builds the full absolute URL to the listing
        // (Request.Scheme makes it "https://host/Cars/Details/5") for share links/QR codes.
        public async Task<IActionResult> Share(int id)
        {
            var car = await _context.Cars.FirstOrDefaultAsync(c => c.Id == id);
            if (car is null) return NotFound();
            ViewData["ListingUrl"] = Url.Action(nameof(Details), "Cars", new { id }, Request.Scheme);
            return View(car);
        }

        // ---- Private helpers that build the dropdown option lists ----
        // "static" because they don't use any instance state.

        // Turns the CarType enum into <option>s, marking the current selection.
        private static SelectList BuildTypeOptions(CarType? selected)
        {
            var items = Enum.GetValues<CarType>()
                .Select(t => new { Value = t.ToString(), Text = t.ToString() });
            return new SelectList(items, "Value", "Text", selected?.ToString());
        }

        // The fixed list of sort options shown in the "Sort by" dropdown.
        private static SelectList BuildSortOptions(string? selected)
        {
            var items = new[]
            {
                new { Value = "newest",     Text = "Newest first" },
                new { Value = "price_asc",  Text = "Price: low to high" },
                new { Value = "price_desc", Text = "Price: high to low" },
                new { Value = "year_desc",  Text = "Year: newest" },
                new { Value = "year_asc",   Text = "Year: oldest" },
            };
            return new SelectList(items, "Value", "Text", selected);
        }
    }
}
