using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Codevos.Net.Caching;

namespace Microsoft.Extensions.Caching.Distributed
{
    /// <summary>
    /// Provides typed <see cref="IDistributedCache"/> extension methods.
    /// </summary>
    public static class TypedDistributedCacheExtensions
    {
        private static JsonSerializerOptions _jsonSerializerOptions;
        private static JsonSerializerOptions JsonSerializerOptions
        {
            get
            {
                if (_jsonSerializerOptions != null) return _jsonSerializerOptions;

                _jsonSerializerOptions = new JsonSerializerOptions();
                _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                return _jsonSerializerOptions;
            }
        }

        /// <summary>
        /// Gets the item with the given key from the cache. 
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="cache">The <see cref="IDistributedCache"/>.</param>
        /// <param name="key">The key.</param>
        /// <param name="deserializeType">An optional type to deserialize to. Useful when <typeparamref name="T"/> is an interface.</param>
        /// <param name="cancellationToken">A cancellation token to cancel running tasks with.</param>
        /// <returns>A <see cref="DistributedCacheEntry{T}"/> with an indicator if the item was loaded or not and optionally, the value.</returns>
        public static async Task<DistributedCacheEntry<T>> GetAsync<T>(
            this IDistributedCache cache,
            string key,
            Type deserializeType = null,
            CancellationToken cancellationToken = default
        )
        {
            var cacheBytes = await cache.GetAsync(key, cancellationToken);
            if (cacheBytes == null || cacheBytes.Length == 0) return new DistributedCacheEntry<T>();

            if (deserializeType == null)
            {
                deserializeType = typeof(T);
            }

            object value;

            using (var memoryStream = new MemoryStream())
            {
                await memoryStream.WriteAsync(cacheBytes, 0, cacheBytes.Length, cancellationToken);
                memoryStream.Position = 0;
                value = await JsonSerializer.DeserializeAsync(memoryStream, deserializeType, JsonSerializerOptions, cancellationToken);                
            }

            return new DistributedCacheEntry<T>((T)value, true);
        }

        /// <summary>
        /// Sets the item with the given key.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="cache">The <see cref="IDistributedCache"/>.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="methodCacheOptions">Optional method cache options for configuring cache expiry.</param>
        /// <param name="cancellationToken">A cancellation token to cancel running tasks with.</param>
        /// <returns>A boolean indicating whether the item was written to the cache or not.</returns>
        public static async Task<bool> SetAsync<T>(
            this IDistributedCache cache,
            string key,
            T value,
            IMethodCacheOptions methodCacheOptions = null,
            CancellationToken cancellationToken = default
        )
        {
            byte[] cacheBytes;

            using (var memoryStream = new MemoryStream())
            {
                await JsonSerializer.SerializeAsync(memoryStream, value, methodCacheOptions?.SerializeType ?? typeof(T), JsonSerializerOptions, cancellationToken);
                memoryStream.Position = 0;
                cacheBytes = memoryStream.ToArray();
            }

            if (cacheBytes.Length == 0) return false;

            var cacheEntryOptions = new DistributedCacheEntryOptions();

            if (methodCacheOptions != null)
            {
                var expiration = new TimeSpan(
                    methodCacheOptions.ExpirationHours,
                    methodCacheOptions.ExpirationMinutes,
                    methodCacheOptions.ExpirationSeconds
                );

                if (expiration.Ticks > 0)
                {
                    if (methodCacheOptions.SlidingExpiration)
                    {
                        cacheEntryOptions.SlidingExpiration = expiration;
                    }
                    else
                    {
                        cacheEntryOptions.AbsoluteExpirationRelativeToNow = expiration;
                    }
                }
            }

            await cache.SetAsync(
                key,
                cacheBytes,
                cacheEntryOptions,
                cancellationToken
            );

            return true;
        }
    }
}
