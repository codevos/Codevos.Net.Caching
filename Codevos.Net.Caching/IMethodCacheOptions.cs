using System;

namespace Codevos.Net.Caching
{
    /// <summary>
    /// Method cache options interface.
    /// </summary>
    public interface IMethodCacheOptions
    {
        /// <summary>
        /// Gets or sets the expiration hours.
        /// </summary>
        int ExpirationHours { get; set; }

        /// <summary>
        /// Gets or sets the expiration minutes.
        /// </summary>
        int ExpirationMinutes { get; set; }

        /// <summary>
        /// Gets or sets the expiration seconds.
        /// </summary>
        int ExpirationSeconds { get; set; }

        /// <summary>
        /// Gets or sets a boolean whether the expiration is sliding or not.
        /// Defaults to false (absolute expiration).
        /// </summary>
        bool SlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets the type to serialize from / deserialize to when reading from cache.
        /// Useful for methods which return an interface type.
        /// </summary>
        Type SerializeType { get; set; }
    }
}