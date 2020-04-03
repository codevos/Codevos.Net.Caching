using System;
using System.Linq;
using System.Reflection;
using Codevos.Net.Caching.Interceptors;
using Castle.DynamicProxy;

namespace Codevos.Net.Caching
{
    /// <summary>
    /// Cache proxy factory.
    /// </summary>
    public class CacheProxyFactory
    {
        private readonly ProxyGenerator ProxyGenerator;
        private readonly CacheInterceptor CacheInterceptor;

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheProxyFactory"/>.
        /// </summary>
        /// <param name="proxyGenerator">The <see cref="Castle.DynamicProxy.ProxyGenerator"/>.</param>
        /// <param name="cacheInterceptor">The <see cref="Interceptors.CacheInterceptor"/>.</param>
        public CacheProxyFactory(
            ProxyGenerator proxyGenerator,
            CacheInterceptor cacheInterceptor)
        {
            ProxyGenerator = proxyGenerator ?? throw new ArgumentNullException(nameof(proxyGenerator));
            CacheInterceptor = cacheInterceptor ?? throw new ArgumentNullException(nameof(cacheInterceptor));
        }

        /// <summary>
        /// Creates a cacheable proxy of the <paramref name="serviceType"/>, based on the given <paramref name="implementation"/>.
        /// </summary>
        /// <param name="serviceType">The service type to proxy.</param>
        /// <param name="implementation">The service implementation to use as a base for the proxy.</param>
        /// <param name="constructorArgumentFactory">A factory to invoke for getting the <paramref name="implementation"/> constructor arguments.</param>
        /// <param name="constructorParameterFactory">An optional factory to invoke for getting the <paramref name="implementation"/> constructor parameters.</param>
        /// <returns>A cacheable proxy of the <paramref name="serviceType"/>, based on the <paramref name="implementation"/>.</returns>
        public object Create(Type serviceType, object implementation, Func<Type, object> constructorArgumentFactory, Func<Type, ParameterInfo[]> constructorParameterFactory = null)
        {
            if (serviceType.IsInterface) return ProxyGenerator.CreateInterfaceProxyWithTarget(serviceType, implementation, CacheInterceptor);

            var implementationType = implementation.GetType();
            
            var constructorParameters = constructorParameterFactory != null
                ? constructorParameterFactory.Invoke(implementationType)
                : implementationType.GetConstructors()[0].GetParameters();

            var constructorArguments = constructorParameters
                    .Select(x => constructorArgumentFactory.Invoke(x.ParameterType))
                    .ToArray();

            return ProxyGenerator.CreateClassProxyWithTarget(serviceType, implementation, constructorArguments, CacheInterceptor);
        }

        /// <summary>
        /// Creates a cacheable proxy of the <typeparamref name="TService"/>, based on the given <paramref name="implementation"/>.
        /// </summary>
        /// <typeparam name="TService">The service type to proxy.</typeparam>
        /// <typeparam name="TImplementation">The service implementation type.</typeparam>
        /// <param name="implementation">The service implementation to use as a base for the proxy.</param>
        /// <param name="constructorArgumentFactory">A factory to invoke for getting the <paramref name="implementation"/> constructor arguments.</param>
        /// <param name="constructorParameterFactory">An optional factory to invoke for getting the <paramref name="implementation"/> constructor parameters.</param>
        /// <returns>A cacheable proxy of the <typeparamref name="TService"/>, based on the <paramref name="implementation"/>.</returns>
        public TService Create<TService, TImplementation>(TImplementation implementation, Func<Type, object> constructorArgumentFactory, Func<Type, ParameterInfo[]> constructorParameterFactory = null)
            where TService : class
            where TImplementation : TService
        {
            return (TService)Create(typeof(TService), implementation, constructorArgumentFactory, constructorParameterFactory);
        }

        /// <summary>
        /// Creates a cacheable proxy of the given <paramref name="service"/>.
        /// </summary>
        /// <typeparam name="TService">The service type to proxy.</typeparam>
        /// <param name="service">The service implementation to use as a base for the proxy.</param>
        /// <param name="constructorArgumentFactory">A factory to invoke for getting the <paramref name="service"/> constructor arguments.</param>
        /// <param name="constructorParameterFactory">An optional factory to invoke for getting the <paramref name="service"/> constructor parameters.</param>
        /// <returns>A cacheable proxy of the <typeparamref name="TService"/>, based on the <paramref name="service"/>.</returns>
        public TService Create<TService>(TService service, Func<Type, object> constructorArgumentFactory, Func<Type, ParameterInfo[]> constructorParameterFactory = null)
            where TService : class
        {
            return (TService)Create(typeof(TService), service, constructorArgumentFactory, constructorParameterFactory);
        }
    }
}
