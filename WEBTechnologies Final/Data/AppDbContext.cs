using Microsoft.EntityFrameworkCore;
using WEBTechnologies_Final.Models;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WEBTechnologies_Final.Data
{
    // AppDbContext is the Entity Framework Core "database context" — the bridge between the
    // C# model classes and the PostgreSQL database. Through it you query and save data, and
    // EF translates your LINQ into SQL. It is registered for dependency injection in
    // Program.cs (AddDbContext) and is created fresh per web request (scoped lifetime).
    public class AppDbContext : DbContext
    {
        // The options (provider, connection string, etc.) are injected by the DI container,
        // which got them from the AddDbContext(...UseNpgsql(...)) call in Program.cs.
        // We just hand them to the base DbContext constructor.
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Each DbSet<T> is a table you can query (LINQ) and modify (Add/Remove/Update).
        // The "=> Set<T>()" expression body is a concise, null-safe way to expose them.
        public DbSet<User> Users => Set<User>();
        public DbSet<Car> Cars => Set<Car>();
        public DbSet<Bid> Bids => Set<Bid>();
        public DbSet<UserFavoriteCar> UserFavoriteCars => Set<UserFavoriteCar>();

        // OnConfiguring runs as the context is being set up. Here it only suppresses one
        // warning: EF would otherwise complain at runtime if it thinks the model has changes
        // not captured in a migration. Silencing PendingModelChangesWarning stops that
        // (a convenience during development; in strict setups you'd add a migration instead).
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        // OnModelCreating is where the "fluent API" configures the database schema — the
        // parts that attributes on the models can't express. EF calls this once when it
        // builds its model of the database.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---- User table ----
            modelBuilder.Entity<User>(e =>
            {
                // Unique indexes: the database itself will reject a duplicate username or
                // email. This is a stronger guarantee than checking in code, because it holds
                // even under concurrent requests (no two registrations can race past it).
                e.HasIndex(u => u.Username).IsUnique();
                e.HasIndex(u => u.Email).IsUnique();
            });

            // ---- Car table ----
            modelBuilder.Entity<Car>(e =>
            {
                // HasPrecision(18, 2) => money columns store up to 18 digits with 2 decimals,
                // i.e. cents. Without this EF/PostgreSQL might pick a default that truncates.
                e.Property(c => c.StartingPrice).HasPrecision(18, 2);
                e.Property(c => c.InstantBuyPrice).HasPrecision(18, 2);

                // A relational column can't store a List<string> directly, so we use a "value
                // converter": when saving, the list is serialized to a JSON string; when
                // reading, that JSON is deserialized back into a List<string>. The "?? new
                // List<string>()" guards against a null/empty column producing a null list.
                e.Property(c => c.ImagePaths)
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
                    );
            });

            // ---- Bid table ----
            modelBuilder.Entity<Bid>(e =>
            {
                e.Property(b => b.Amount).HasPrecision(18, 2);

                // Define the one-to-many relationship explicitly:
                //   one Car (HasOne b.Car) has many Bids (WithMany c.Bids),
                //   joined on the Bid.CarId foreign key.
                // OnDelete(Cascade): deleting a car automatically deletes its bids, so we
                // never leave orphaned bid rows pointing at a car that no longer exists.
                e.HasOne(b => b.Car)
                    .WithMany(c => c.Bids)
                    .HasForeignKey(b => b.CarId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ---- UserFavoriteCar join table ----
            modelBuilder.Entity<UserFavoriteCar>(e =>
            {
                // Composite primary key: the (Username, CarId) pair is the key. This both
                // uniquely identifies a favourite and prevents the same user favouriting the
                // same car twice (the DB would reject the duplicate key).
                e.HasKey(f => new { f.Username, f.CarId });
            });
        }
    }
}
