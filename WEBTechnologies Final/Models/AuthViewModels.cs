using System.ComponentModel.DataAnnotations;

namespace WEBTechnologies_Final.Models
{
    // View models for the authentication forms. They are deliberately separate from the
    // User entity: a form only needs the fields the user types (including ConfirmPassword,
    // which is never stored), and we never want raw passwords flowing into a database entity.
    // The [Required]/[StringLength]/etc. attributes give automatic client- and server-side
    // validation with no extra controller code.

    // Backs the "create an account" form (Views/Account/Register.cshtml).
    public class RegisterViewModel
    {
        // [StringLength(max, MinimumLength = min)] enforces a length window and shows the
        // custom ErrorMessage when violated.
        [Required]
        [Display(Name = "Username")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "Username must be 3–30 characters.")]
        public string Username { get; set; } = string.Empty;

        // [EmailAddress] checks the value looks like an email (has an @, etc.).
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        // [DataType(DataType.Password)] makes the rendered <input> a masked password box.
        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; } = string.Empty;

        // [Compare(nameof(Password))] validates that this field exactly matches the Password
        // field — the standard "confirm your password" check. Using nameof() instead of the
        // literal string "Password" means a rename refactor won't silently break it.
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // Backs the "sign in" form (Views/Account/Login.cshtml).
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        // Where to send the user after a successful login. This carries the page they were
        // trying to reach before being redirected to login, so we can return them there
        // (the "return URL" pattern). Nullable: empty means "just go to the home page".
        public string? ReturnUrl { get; set; }
    }
}
