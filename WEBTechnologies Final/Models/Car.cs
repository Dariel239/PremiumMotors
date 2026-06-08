// System.ComponentModel.DataAnnotations gives us the validation/display attributes
// ([Required], [Range], [Display], [DataType]) used on the properties below.
// ASP.NET Core MVC reads these attributes automatically during model binding to
// validate incoming form data and to drive how fields are labelled in the UI.
using System.ComponentModel.DataAnnotations;

namespace WEBTechnologies_Final.Models
{
    // Car is the central "domain model" / entity of the whole application.
    // The same class is used three ways:
    //   1. As an Entity Framework Core entity -> it maps to the "Cars" table in PostgreSQL.
    //   2. As an MVC model -> its data-annotation attributes drive form validation.
    //   3. As an API DTO -> it is serialized to JSON by the Web API controllers.
    public class Car
    {
        // Primary key. EF Core recognises a property literally named "Id" (or "CarId")
        // as the primary key by convention, and PostgreSQL auto-generates the value
        // (an identity/serial column), so we never set it ourselves when creating a car.
        public int Id { get; set; }

        // [Required] => model binding fails validation if this is left blank on a form.
        // [Display(Name = "...")] => the text used by <label asp-for="Make"> in the views.
        // = string.Empty initialises the property so it is never null (works nicely with
        // C# nullable reference types: a non-null default avoids "possible null" warnings).
        [Required]
        [Display(Name = "Make")]
        public string Make { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Model")]
        public string Model { get; set; } = string.Empty;

        // The car body style. CarType is an enum (see CarType.cs). EF stores enums as
        // integers in the database by default (Sedan = 0, SUV = 1, ...).
        [Display(Name = "Body Type")]
        public CarType Type { get; set; }

        // [Range] keeps the year sensible. Validation runs both client-side (via the
        // jQuery validation scripts) and server-side (when the controller checks ModelState).
        [Range(1900, 2100)]
        [Display(Name = "Model Year")]
        public int Year { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        // The price the auction opens at. decimal (not double) is the correct type for money:
        // it avoids binary floating-point rounding errors.
        // [DataType(DataType.Currency)] is a *display* hint (format as money), not validation.
        [Range(0, double.MaxValue, ErrorMessage = "Starting price must be a positive number.")]
        [Display(Name = "Starting Auction Price")]
        [DataType(DataType.Currency)]
        public decimal StartingPrice { get; set; }

        // Optional "buy it now" price. The "?" makes it a nullable decimal: null means the
        // seller did not offer instant buy, so this car is auction-only.
        [Display(Name = "Instant Buy Price")]
        [DataType(DataType.Currency)]
        public decimal? InstantBuyPrice { get; set; }

        // Relative web paths to the uploaded photos (e.g. "/uploads/cars/abc.jpg").
        // EF Core persists this List<string> via a value converter (configured in
        // AppDbContext) because a relational column cannot hold a list directly.
        // "= new()" is the C# target-typed new expression, short for "new List<string>()".
        public List<string> ImagePaths { get; set; } = new();

        // When the auction closes. Nullable: null means "no fixed end time".
        [Display(Name = "Auction Ends")]
        public DateTime? AuctionEnd { get; set; }

        // Navigation property: the bids placed on this car. EF Core uses the Bid.CarId
        // foreign key to load these (a one-to-many relationship: one Car has many Bids).
        public List<Bid> Bids { get; set; } = new();

        // Auction outcome flags.
        public bool IsSold { get; set; }
        public string? SoldTo { get; set; }   // username of the buyer, null until sold

        // Audit timestamp, stored in UTC so it is timezone-independent.
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // ----- Computed (read-only) properties -----
        // These have only a getter using "=>" (expression-bodied). They are NOT stored in
        // the database; EF ignores get-only computed properties. They exist purely to keep
        // display logic out of the views, so a view can just call @Model.Title etc.

        // A friendly heading like "2021 Mercedes-Benz C-Class".
        public string Title => $"{Year} {Make} {Model}";

        // The single highest bid, or null if nobody has bid yet.
        // OrderByDescending(...).First() sorts bids high-to-low and takes the top one.
        public Bid? HighestBid =>
            Bids.Count == 0 ? null : Bids.OrderByDescending(b => b.Amount).First();

        // What the car currently costs: the highest bid if one exists, otherwise the
        // starting price. "?." is the null-conditional operator and "??" supplies the
        // fallback when HighestBid is null.
        public decimal CurrentPrice => HighestBid?.Amount ?? StartingPrice;

        // True only when an instant-buy price was set AND the car has not already sold.
        public bool HasInstantBuy => InstantBuyPrice.HasValue && !IsSold;

        // The auction is "closed" if it has been sold, or its end time has passed.
        public bool IsClosed => IsSold || (AuctionEnd.HasValue && AuctionEnd.Value <= DateTime.Now);

        // The first photo to show in lists/cards, falling back to a placeholder image
        // (shipped in wwwroot/img) when the car has no uploaded photos.
        public string PrimaryImage =>
            ImagePaths.Count > 0 ? ImagePaths[0] : "/img/no-image.svg";
    }
}
