// SelectList is the MVC helper type that backs an HTML <select> dropdown.
using Microsoft.AspNetCore.Mvc.Rendering;

namespace WEBTechnologies_Final.Models
{
    // A "view model": a class shaped specifically for one view rather than for the database.
    // The car-listing page (Views/Cars/Index.cshtml) needs more than just a list of cars —
    // it also needs the current filter values and the dropdown option lists. Bundling all of
    // that into one object keeps the controller-to-view contract clear and strongly typed.
    public class CarListViewModel
    {
        // The cars to display (already filtered/sorted by the controller).
        // IReadOnlyList signals to the view "just render these; don't modify the collection".
        public IReadOnlyList<Car> Cars { get; set; } = new List<Car>();

        // ----- Current filter selections -----
        // These are nullable so "no filter applied" is distinct from a real value.
        // They are populated from the query string when the user submits the filter form,
        // and re-displayed so the form remembers what the user picked.
        public string? Search { get; set; }   // free-text search box
        public CarType? Type { get; set; }     // body-type filter
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }

        // Current sort order (e.g. "newest", "price-asc"). Defaults to newest first.
        public string SortBy { get; set; } = "newest";

        // ----- Dropdown option lists -----
        // The controller fills these with the available choices (and marks the current
        // selection). Nullable because the view guards against them being unset.
        public SelectList? TypeOptions { get; set; }
        public SelectList? MakeOptions { get; set; }
        public SelectList? ModelOptions { get; set; }
        public SelectList? YearOptions { get; set; }
        public SelectList? SortOptions { get; set; }

        // Convenience flag: true if ANY filter is active. The view uses it to decide whether
        // to show a "clear filters" button / "showing filtered results" message.
        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(Search) ||
            Type.HasValue ||
            !string.IsNullOrWhiteSpace(Make) ||
            !string.IsNullOrWhiteSpace(Model) ||
            Year.HasValue;
    }
}
