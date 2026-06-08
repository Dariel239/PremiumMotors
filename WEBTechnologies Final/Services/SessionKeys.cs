namespace WEBTechnologies_Final.Services
{
    // Central place for the string keys used to read/write values in the server-side Session.
    // Defining them as constants in one spot avoids "magic strings" scattered around the code
    // and the bugs that come from a typo in one place ("isadmin" vs "IsAdmin").
    //
    // This app uses session-based auth: after login the controller stores the username (and,
    // for admins, an admin flag) in Session. The filters and views then read these keys to
    // decide what the current user may see/do.
    public static class SessionKeys
    {
        // Stores the literal string "true" for an administrator (see AccountController / AdminOnlyAttribute).
        public const string IsAdmin = "IsAdmin";

        // Stores the logged-in user's username; absent/empty means "not logged in".
        public const string Username = "Username";
    }
}
