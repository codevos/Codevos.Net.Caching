namespace Codevos.Net.Caching
{
    /// <summary>
    /// Distributed cache entry.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public class DistributedCacheEntry<T>
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Gets a boolean indicating whether the value was loaded or not.
        /// </summary>
        public bool Loaded { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedCacheEntry{T}"/> class.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="loaded">A boolean indicating whether the value was loaded or not.</param>
        internal DistributedCacheEntry(
            T value = default,
            bool loaded = false
        )
        {
            Value = value;
            Loaded = loaded;
        }
    }
}
