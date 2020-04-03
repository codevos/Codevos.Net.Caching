using System;
using System.Threading.Tasks;
using Castle.DynamicProxy;

namespace Codevos.Net.Caching.Interceptors
{
    /// <summary>
    /// Cache interceptor.
    /// </summary>
    public class CacheInterceptor : AsyncInterceptorBase
    {
        private readonly MethodResultCache MethodResultCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheInterceptor"/> class.
        /// </summary>
        /// <param name="methodResultCache">The <see cref="Caching.MethodResultCache"/>.</param>
        public CacheInterceptor(
            MethodResultCache methodResultCache
        )
        {
            MethodResultCache = methodResultCache ?? throw new ArgumentNullException(nameof(methodResultCache));
        }

        /// <summary>
        /// Intercepts the given <paramref name="invocation"/>.
        /// </summary>
        /// <param name="invocation">The method invocation.</param>
        /// <param name="proceed">The function to proceed the invocation with.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
        protected override async Task InterceptAsync(IInvocation invocation, Func<IInvocation, Task> proceed)
        {
            await proceed(invocation);
        }

        /// <summary>
        /// Intercepts the given <paramref name="invocation"/> and checks if the invocation result should be added to / loaded from cache or not.
        /// </summary>
        /// <typeparam name="T">The return type.</typeparam>
        /// <param name="invocation">The method invocation.</param>
        /// <param name="proceed">The function to proceed the invocation with.</param>
        /// <returns>A <see cref="Task{T}"/> that represents the asynchronous operation.</returns>
        protected override async Task<T> InterceptAsync<T>(IInvocation invocation, Func<IInvocation, Task<T>> proceed)
        {
            var methodCacheOptions = MethodResultCache.GetMethodCacheOptions(invocation.Method);
            if (methodCacheOptions == null) return await proceed(invocation);

            var returnValue = await MethodResultCache.GetOrCreate(
                invocation.Method,
                invocation.Arguments,
                methodCacheOptions,
                () => proceed(invocation)
            );

            if (invocation.ReturnValue == null)
            {
                invocation.ReturnValue = returnValue;
            }
            
            return returnValue;
        }
    }
}