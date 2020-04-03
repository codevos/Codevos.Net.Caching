using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Codevos.Net.Caching
{
    /// <summary>
    /// Cache options.
    /// </summary>
    public class CacheOptions
    {
        internal IDictionary<Type, IDictionary<string, IMethodCacheOptions>> Configurations { get; }
        internal IEnumerable<Type> CacheKeyIgnoreTypes { get; }
        internal Func<Type, object, object> CacheKeyArgumentResolver { get; }
        internal Func<string> CacheKeySuffixFactory { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheOptions"/> class.
        /// </summary>
        public CacheOptions(
            IDictionary<Type, IDictionary<string, IMethodCacheOptions>> configurations,
            IEnumerable<Type> cacheKeyIgnoreTypes,
            Func<Type, object, object> cacheKeyArgumentResolver,
            Func<string> cacheKeySuffixFactory
        )
        {
            Configurations = configurations;
            

            var ignoreTypes = (cacheKeyIgnoreTypes ?? Enumerable.Empty<Type>())
                .Concat(new[]
                {
                    typeof(CancellationToken)
                });

            CacheKeyIgnoreTypes = new HashSet<Type>(ignoreTypes);
            CacheKeyArgumentResolver = cacheKeyArgumentResolver;
            CacheKeySuffixFactory = cacheKeySuffixFactory;
        }
    }
}