using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Codevos.Net.Caching
{
    /// <summary>
    /// Hash calculator.
    /// </summary>
    public class HashCalculator
    {
        /// <summary>
        /// Calculates the SHA-256 hash for the given stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The SHA-256 hash for the given stream.</returns>
        public string CalculateSha256(Stream stream)
        {
            byte[] hash;

            using (var sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(stream);
            }

            var hashBuilder = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                hashBuilder.Append(hash[i].ToString("x2"));
            }

            return hashBuilder.ToString();
        }
    }
}
