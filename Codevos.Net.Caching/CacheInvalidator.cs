using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Codevos.Net.Caching
{
    /// <summary>
    /// Cache invalidator.
    /// </summary>
    public class CacheInvalidator
    {
        private readonly MethodResultCache MethodResultCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheInvalidator"/> class.
        /// </summary>
        /// <param name="methodResultCache">The <see cref="Caching.MethodResultCache"/>.</param>
        public CacheInvalidator(
            MethodResultCache methodResultCache
        )
        {
            MethodResultCache = methodResultCache ?? throw new ArgumentNullException(nameof(methodResultCache));
        }

        /// <summary>
        /// Invalidates all cached method results for the given type.
        /// </summary>
        /// <typeparam name="T">The type to invalidate all cached method results for.</typeparam>
        public async Task Invalidate<T>()
        {
            var tasks = new List<Task>();

            foreach (var method in typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                tasks.Add(MethodResultCache.Remove(method));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Invalidates all cached method results for the given method name on the given type.
        /// </summary>
        /// <typeparam name="T">The type to invalidate the cached method results for.</typeparam>
        /// <param name="methodNameFactory">The method name factory. Example usage: CacheInvalidator.Invalidate&lt;IMyService&gt;(x =&gt; nameof(x.GetAll))</param>
        public async Task Invalidate<T>(Func<T, string> methodNameFactory)
        {
            var methodName = methodNameFactory.Invoke(default);

            var method = typeof(T).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            if (method == null) return;

            await MethodResultCache.Remove(method);
        }
    }
}