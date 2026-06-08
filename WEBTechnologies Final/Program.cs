// Program.cs is the application's entry point. It uses the "minimal hosting" model: the
// top-level statements below run at startup. The file has two halves:
//   1) configure SERVICES (the dependency-injection container) — everything before Build().
//   2) configure the MIDDLEWARE PIPELINE (how each HTTP request is processed) — after Build().

using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using WEBTechnologies_Final.Data;
using WEBTechnologies_Final.Services;

// Npgsql (the PostgreSQL provider) historically required this switch to accept DateTime
// values that aren't explicitly UTC ("legacy timestamp behavior"). It avoids runtime errors
// when saving the DateTime fields on Car/Bid.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// The builder gathers configuration (appsettings.json, environment variables) and the
// service collection used for dependency injection.
var builder = WebApplication.CreateBuilder(args);

// ----- Culture / currency -----
// Base the app on British English (en-GB: dd/MM/yyyy dates, comma thousands) but override the
// currency symbol to the euro. This is what makes "{value:C}" / ToString("C") render "€...".
// Setting DefaultThreadCurrentCulture makes it the fallback for all threads; the actual
// per-request culture is enforced later by UseRequestLocalization.
var appCulture = new CultureInfo("en-GB");
appCulture.NumberFormat.CurrencySymbol = "€";
CultureInfo.DefaultThreadCurrentCulture = appCulture;
CultureInfo.DefaultThreadCurrentUICulture = appCulture;

// Register MVC controllers + Razor views. The JSON option makes the serializer ignore
// reference cycles (e.g. Car -> Bids -> Car), preventing infinite loops when returning models.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Register attribute-routed API controllers (the [ApiController] classes), with the same
// cycle-safe JSON setting for API responses.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Swagger/OpenAPI: generates interactive API documentation for the api/* endpoints.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register the EF Core database context, using PostgreSQL with the "DefaultConnection"
// connection string from appsettings.json. Registered with the default scoped lifetime
// (one AppDbContext per HTTP request).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register ApiClient as a typed HttpClient. NOTE (see ApiClient.cs): the class actually uses
// the DbContext directly, so this HttpClient/BaseAddress is effectively unused — a remnant of
// an earlier design where the site called its own API over HTTP. AddHttpClient also registers
// ApiClient itself for injection into the controllers.
builder.Services.AddHttpClient<ApiClient>(client =>
{
    var baseUrl = builder.Configuration["AppBaseUrl"] ?? "http://localhost:7007";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
});

// Session support, backed by an in-memory cache. Sessions store the logged-in username and
// admin flag (see SessionKeys). HttpOnly stops client-side JS from reading the cookie;
// IsEssential exempts it from cookie-consent gating so login works without consent banners.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Build the app. After this point we configure the request pipeline (middleware order matters:
// each request flows through these in the order they are added).
var app = builder.Build();

// Expose Swagger and its UI (served at /swagger). Enabled unconditionally here so the API docs
// are available in every environment.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Car Auction API v1");
});

// Apply any pending EF Core migrations at startup, creating the database/tables if needed.
// We open a temporary DI scope because the DbContext is scoped and there is no request scope
// yet during startup. Convenient for a course project; some teams prefer running migrations
// as a separate deploy step instead of on every boot.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Outside Development, route unhandled exceptions to the friendly error page and enable HSTS
// (tells browsers to stick to HTTPS). In Development we instead see the detailed error page.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ----- Enforce the euro culture on every request -----
// RequestCultureProviders are cleared so nothing (e.g. the browser's Accept-Language header)
// can override our culture; every request uses appCulture, guaranteeing euro formatting.
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(appCulture),
    SupportedCultures = new[] { appCulture },
    SupportedUICultures = new[] { appCulture }
};
localizationOptions.RequestCultureProviders.Clear();
app.UseRequestLocalization(localizationOptions);

// Standard middleware, in order:
app.UseHttpsRedirection();   // redirect http:// to https://
app.UseRouting();            // match the request to an endpoint
app.UseSession();            // make HttpContext.Session available (must precede auth checks that read it)
app.UseAuthorization();      // authorization middleware
app.MapStaticAssets();       // serve wwwroot static files (optimized static-asset pipeline)

// Map the API controllers' attribute routes (api/cars, api/users, api/favorites).
app.MapControllers();

// Map the conventional MVC route. With no other segments, the site opens at Cars/Index — the
// car catalogue — which is therefore the home page. "{id?}" makes the id optional.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Cars}/{action=Index}/{id?}")
    .WithStaticAssets();

// Start handling requests (blocks until the app shuts down).
app.Run();
