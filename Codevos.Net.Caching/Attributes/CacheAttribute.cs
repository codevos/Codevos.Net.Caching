using System;

namespace Codevos.Net.Caching.Attributes
{
    /// <summary>
    /// Cache attribute to place on methods which results should be cached.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CacheAttribute : Attribute, IMethodCacheOptions
    {
        /// <summary>
        /// Gets or sets the expiration hours.
        /// </summary>
        public int ExpirationHours { get; set; }

        /// <summary>
        /// Gets or sets the expiration minutes.
        /// </summary>
        public int ExpirationMinutes { get; set; }

        /// <summary>
        /// Gets or sets the expiration seconds.
        /// </summary>
        public int ExpirationSeconds { get; set; }

        /// <summary>
        /// Gets or sets a boolean whether the expiration is sliding or not.
        /// Defaults to false (absolute expiration).
        /// </summary>
        public bool SlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets the type to deserialize to when reading from cache.
        /// Useful for methods which return an interface type.
        /// </summary>
        public Type SerializeType { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheAttribute"/> class.
        /// </summary>
        public CacheAttribute()
        {
        }
    }
}
