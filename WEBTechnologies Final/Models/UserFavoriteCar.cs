using System.ComponentModel.DataAnnotations;

namespace WEBTechnologies_Final.Models
{
    // A "join" (link) entity implementing the many-to-many relationship
    // "a user can favourite many cars, and a car can be favourited by many users".
    // Each row simply records one (username, carId) pairing.
    //
    // The composite primary key (Username + CarId together) is configured in
    // AppDbContext.OnModelCreating via HasKey(...). Making the pair the key also
    // guarantees a user cannot favourite the same car twice.
    public class UserFavoriteCar
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public int CarId { get; set; }
    }
}
