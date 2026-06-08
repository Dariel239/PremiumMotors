using System.Security.Cryptography;
using System.Text;

namespace WEBTechnologies_Final.Services
{
    // Turns a plain-text password into a fixed-length hash so the raw password is never
    // stored. "static" because it holds no state — it's just two pure helper functions.
    //
    // Learning caveat: plain SHA-256 (no per-user salt, no slow/iterated function) is fine
    // for a course project but NOT recommended for real systems. Production code should use a
    // password-specific algorithm such as PBKDF2, bcrypt, scrypt or Argon2, which add salting
    // and deliberate slowness to resist brute-force and rainbow-table attacks.
    public static class PasswordHasher
    {
        // Hash: bytes -> SHA-256 -> lowercase hex string.
        public static string Hash(string password)
        {
            // Encode the text as UTF-8 bytes, then compute the 32-byte SHA-256 digest.
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            // Convert those bytes to a hex string (e.g. "a1b2...") for easy storage/compare.
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        // Verify: hash the candidate password and compare it to the stored hash.
        // The same input always produces the same hash, so equal hashes => correct password.
        public static bool Verify(string password, string hash)
            => Hash(password) == hash;
    }
}
