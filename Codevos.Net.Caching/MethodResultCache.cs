using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Codevos.Net.Caching.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Codevos.Net.Caching
{
    /// <summary>
    /// Method result cache.
    /// </summary>
    public class MethodResultCache
    {
        private readonly CacheOptions CacheOptions;
        private readonly IDistributedCache DistributedCache;
        private readonly CacheKeyProvider CacheKeyProvider;
        private readonly ILogger Logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodResultCache"/> class.
        /// </summary>
        /// <param name="cacheOptions">The <see cref="CacheOptions"/>.</param>
        /// <param name="cacheKeyProvider">The <see cref="Caching.CacheKeyProvider"/>.</param>
        /// <param name="distributedCache">The <see cref="IDistributedCache"/>.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
        public MethodResultCache(
            CacheOptions cacheOptions,
            CacheKeyProvider cacheKeyProvider,
            IDistributedCache distributedCache,
            ILoggerFactory loggerFactory
        )
        {
            CacheOptions = cacheOptions ?? throw new ArgumentNullException(nameof(cacheOptions));
            DistributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            CacheKeyProvider = cacheKeyProvider ?? throw new ArgumentNullException(nameof(cacheKeyProvider));
            Logger = loggerFactory?.CreateLogger(GetType()) ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <summary>
        /// Gets or creates a memory cache entry.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="method">The method.</param>
        /// <param name="arguments">The method arguments.</param>
        /// <param name="methodCacheOptions">The <see cref="IMethodCacheOptions"/>.</param>
        /// <param name="factory">The result factory.</param>
        /// <returns>The existing or created memory cache entry value.</returns>
        public async Task<T> GetOrCreate<T>(MethodInfo method, object[] arguments, IMethodCacheOptions methodCacheOptions, Func<Task<T>> factory)
        {
            var invocationKey = await CacheKeyProvider.GetCacheKey(method, arguments);
            
            try
            {
                var cacheEntry = await DistributedCache.GetAsync<T>(invocationKey, methodCacheOptions.SerializeType);
                if (cacheEntry.Loaded) return cacheEntry.Value;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while reading value with '{Key}' from distributed cache", invocationKey);
            }
                
            T value = await factory.Invoke();

            var cacheSet = false;

            try
            {
                cacheSet = await DistributedCache.SetAsync(invocationKey, value, methodCacheOptions);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while writing value with key '{Key}' to distributed cache", invocationKey);
            }

            if (!cacheSet) return value;

            await UpdateMethodKeys(invocationKey);
            return value;
        }

        /// <summary>
        /// Removes all cache entries for the given method.
        /// </summary>
        /// <param name="method">The method.</param>
        public async Task Remove(MethodInfo method)
        {
            var methodCacheOptions = GetMethodCacheOptions(method);
            if (methodCacheOptions == null) return;

            var methodKey = CacheKeyProvider.GetCacheKey(method);
            var methodKeysCacheKey = GetMethodKeysCacheKey(methodKey);

            HashSet<string> methodKeyVariants = null;
            var keysLoadedFromCache = false;

            try
            {
                var cacheEntry = await DistributedCache.GetAsync<HashSet<string>>(methodKeysCacheKey);
                methodKeyVariants = cacheEntry.Value;
                keysLoadedFromCache = cacheEntry.Loaded;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while reading value with '{Key}' from distributed cache", methodKeysCacheKey);
            }

            var tasks = new List<Task>();

            if (keysLoadedFromCache)
            {
                if (methodKeyVariants != null)
                {
                    foreach (var methodKeyVariant in methodKeyVariants)
                    {
                        tasks.Add(DistributedCache.RemoveAsync(methodKeyVariant));
                    }

                    if (!methodKeyVariants.Contains(methodKey))
                    {
                        tasks.Add(DistributedCache.RemoveAsync(methodKey));
                    }
                }

                tasks.Add(DistributedCache.RemoveAsync(methodKeysCacheKey));
            }
            else
            {
                tasks.Add(DistributedCache.RemoveAsync(methodKey));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while removing cache for method '{Method}' from distributed cache", methodKey);
            }
        }

        /// <summary>
        /// Gets the configured <see cref="IMethodCacheOptions"/> from the given <paramref name="method"/>.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>The configured <see cref="IMethodCacheOptions"/> or null.</returns>
        public IMethodCacheOptions GetMethodCacheOptions(MethodInfo method)
        {
            if (!CacheOptions.Configurations.TryGetValue(method.DeclaringType, out IDictionary<string, IMethodCacheOptions> methodCacheOptionsDictionary) ||
                !methodCacheOptionsDictionary.TryGetValue(method.Name, out IMethodCacheOptions methodCacheOptions))
            {
                methodCacheOptions = method.GetCustomAttribute<CacheAttribute>()
                    ?? method.DeclaringType.GetCustomAttribute<CacheAttribute>();
            }

            return methodCacheOptions;
        }

        private async Task UpdateMethodKeys(string invocationKey)
        {
            var dashIndex = invocationKey.IndexOf('-');
            var methodKey = dashIndex > -1 ? invocationKey.Substring(0, dashIndex) : invocationKey;
            
            var methodKeysCacheKey = GetMethodKeysCacheKey(methodKey);
            HashSet<string> methodKeyVariants;

            try
            {
                var cacheEntry = await DistributedCache.GetAsync<HashSet<string>>(methodKeysCacheKey);
                methodKeyVariants = cacheEntry.Value ?? new HashSet<string>();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while reading value with '{Key}' from distributed cache", methodKeysCacheKey);
                return;
            }

            if (!methodKeyVariants.Add(invocationKey)) return;

            try
            {
                await DistributedCache.SetAsync(methodKeysCacheKey, methodKeyVariants);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while writing value with key '{Key}' to distributed cache", methodKeysCacheKey);
            }
        }       

        private string GetMethodKeysCacheKey(string key)
        {
            return $"{key}_keys";
        }
    }
}