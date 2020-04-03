using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Codevos.Net.Caching
{
    /// <summary>
    /// Cachke key provider.
    /// </summary>
    public class CacheKeyProvider
    {
        private readonly HashCalculator HashCalculator;
        private readonly CacheOptions CacheOptions;
        private readonly JsonSerializerOptions JsonSerializerOptions;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="CacheKeyProvider"/> class.
        /// </summary>
        /// <param name="hashCalculator">The <see cref="Caching.HashCalculator"/>.</param>
        /// <param name="cacheOptions">The <see cref="Caching.CacheOptions"/>.</param>
        public CacheKeyProvider(
            HashCalculator hashCalculator,
            CacheOptions cacheOptions
        )
        {
            HashCalculator = hashCalculator ?? throw new  ArgumentNullException(nameof(hashCalculator));
            CacheOptions = cacheOptions ?? throw new ArgumentNullException(nameof(cacheOptions));

            JsonSerializerOptions = new JsonSerializerOptions();
            JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        }

        /// <summary>
        /// Gets the cache key for the given method.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>The cache key for the given method.</returns>
        public string GetCacheKey(MethodInfo method)
        {
            return $"method_result_cache_{method.DeclaringType.FullName}.{method.Name}";
        }

        /// <summary>
        /// Gets the cache key for the given method with the given arguments.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <param name="cancellationToken">A cancellation token to cancel running tasks with.</param>
        /// <returns>The cache key for the given method with the given arguments.</returns>
        public async Task<string> GetCacheKey(MethodInfo method, object[] arguments, CancellationToken cancellationToken = default)
        {
            var key = GetCacheKey(method);

            var suffix = CacheOptions.CacheKeySuffixFactory?.Invoke();           
            if (!string.IsNullOrWhiteSpace(suffix))
            {
                key = $"{key}-{suffix}";
            }

            if (arguments == null || arguments.Length == 0) return key;

            var filteredArguments = arguments
                .Where(obj => !CacheOptions.CacheKeyIgnoreTypes.Contains(obj))
                .Select(obj =>
                {
                    if (obj == null) return obj;

                    var argumentType = obj.GetType();

                    var value = CacheOptions.CacheKeyArgumentResolver?.Invoke(argumentType, obj);
                    if (value != null) return value;

                    if (argumentType.IsValueType) return obj;

                    var stringValue = obj.ToString();
                    if (!string.Equals(stringValue, argumentType.FullName)) return stringValue;

                    return obj;
                });

            string hash;

            using (var memoryStream = new MemoryStream())
            {
                await JsonSerializer.SerializeAsync(
                    memoryStream,
                    filteredArguments,
                    typeof(IEnumerable<object>),
                    JsonSerializerOptions,
                    cancellationToken
                );

                memoryStream.Position = 0;
                hash = HashCalculator.CalculateSha256(memoryStream);
            }

            return $"{key}-{hash}";
        }
    }
}