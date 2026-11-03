using System;
using System.Security.Cryptography;

namespace RaceDay.API.Security
{
    // Hashes and checks passwords using PBKDF2 (built into .NET, no extra NuGet package needed).
    // A password is NEVER stored as typed. We store: PBKDF2$iterations$salt$hash
    //  - salt: random bytes so two users with the same password get different hashes
    //  - iterations: how many times the hash is repeated (slows down guessing attacks)
    public static class PasswordHasher
    {
        private const int SaltSize = 16;       // bytes
        private const int KeySize = 32;        // bytes
        private const int Iterations = 100_000;

        // Turns a plain password into the string we store in the database.
        public static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        // Checks a typed password against a stored hash.
        public static bool Verify(string password, string storedHash)
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4 || parts[0] != "PBKDF2") return false;   // not one of our hashes
            if (!int.TryParse(parts[1], out var iterations)) return false;

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

            // FixedTimeEquals takes the same time whether or not the bytes match (prevents timing attacks)
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}