using System.ComponentModel.DataAnnotations;

namespace WEBTechnologies_Final.Models
{
    // The set of body styles a car can have. Using an enum (instead of a free-text string)
    // means the choices are fixed and type-checked at compile time.
    //
    // Each member has an implicit integer value by position: Sedan = 0, SUV = 1, Hatchback = 2,
    // and so on. EF Core stores that integer in the database. (Be careful reordering members
    // later — it would change the stored numbers and mismatch existing data.)
    //
    // In the views, Html.GetEnumSelectList<CarType>() turns this enum into a <select> dropdown.
    public enum CarType
    {
        Sedan,
        SUV,
        Hatchback,
        Coupe,
        Convertible,
        Wagon,
        Pickup,
        Van,

        // [Display(Name = "Sports Car")] provides a friendly label with a space, so the UI
        // shows "Sports Car" instead of the C# identifier "SportsCar".
        [Display(Name = "Sports Car")]
        SportsCar,

        Other
    }
}
