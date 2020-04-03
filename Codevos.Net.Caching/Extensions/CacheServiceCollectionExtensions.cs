using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Codevos.Net.Caching;
using Codevos.Net.Caching.Attributes;
using Codevos.Net.Caching.Interceptors;
using Castle.DynamicProxy;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides <see cref="IServiceCollection"/> extension methods for registering method result caching services.
    /// </summary>
    public static class CacheServiceCollectionExtensions
    {
        /// <summary>
        /// Adds method result caching services to the <see cref="IServiceCollection"/>.
        /// This call should be made after all the cacheable services are registered.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the method result caching services to.</param>
        /// <param name="configure">Optional cache configuration.</param>
        /// <param name="cacheKeySuffixFactory">An optional factory to add an extra suffix to the cacke keys. E.g. to make the cached values environment specific.</param>
        /// <param name="registerDedicatedDistributedCache">An optional handler to register an <see cref="IDistributedCache"/> on a dedicated <see cref="IServiceCollection"/>.</param>
        /// <param name="defaultToInMemoryDistributedCache">A boolean indicating whether to register the <see cref="MemoryDistributedCache"/> by default when not using a dedicated cache.</param>
        /// <returns>The <see cref="IServiceCollection"/> to use for further configuration.</returns>
        public static IServiceCollection AddMethodResultCaching(
            this IServiceCollection services,
            Action<CacheOptionsConfiguration> configure = null,
            Func<IServiceProvider, string> cacheKeySuffixFactory = null,
            Action<IServiceCollection> registerDedicatedDistributedCache = null,
            bool defaultToInMemoryDistributedCache = true
        )
        {
            var cacheOptionsConfiguration = new CacheOptionsConfiguration();
            configure?.Invoke(cacheOptionsConfiguration);

            var distributedCacheServiceProvider = GetDistributedCacheServiceProvider(registerDedicatedDistributedCache);

            var cacheableServices = new Dictionary<int, ServiceDescriptor>();
            var distributedCacheRegistered = distributedCacheServiceProvider != null;
            var i = -1;

            foreach (var serviceDescriptor in services)
            {
                i++;

                if (!distributedCacheRegistered && serviceDescriptor.ServiceType == typeof(IDistributedCache))
                {
                    distributedCacheRegistered = true;
                    continue;
                }
                
                if (!IsCacheableServiceType(serviceDescriptor.ServiceType, cacheOptionsConfiguration.Configurations)) continue;

                cacheableServices.Add(i, serviceDescriptor);
            }

            ReplaceCacheableServices(services, cacheableServices);

            if (!distributedCacheRegistered && defaultToInMemoryDistributedCache)
            {
                services
                    .AddDistributedMemoryCache();
            }

            return services
                .AddSingleton<ProxyGenerator>()
                .AddSingleton<HashCalculator>()
                .AddSingleton(x => new CacheOptions
                (
                    cacheOptionsConfiguration.Configurations,
                    cacheOptionsConfiguration.CacheKeyIgnoreTypes,
                    cacheOptionsConfiguration.CacheKeyArgumentResolver,
                    () => cacheKeySuffixFactory?.Invoke(x)
                ))
                .AddSingleton<CacheKeyProvider>()
                .AddSingleton(x =>
                {
                    if (distributedCacheServiceProvider == null)
                    {
                        var distributedCache = x.GetService<IDistributedCache>();
                        if (distributedCache == null) throw new ArgumentException($"Method result caching needs a registered {nameof(IDistributedCache)} implementation.", nameof(distributedCache));
                        
                        return new MethodResultCache(
                            x.GetRequiredService<CacheOptions>(),
                            x.GetRequiredService<CacheKeyProvider>(),
                            distributedCache,
                            x.GetRequiredService<ILoggerFactory>()
                        );
                    }

                    return new MethodResultCache(
                        x.GetRequiredService<CacheOptions>(),
                        x.GetRequiredService<CacheKeyProvider>(),
                        distributedCacheServiceProvider.GetRequiredService<IDistributedCache>(),
                        x.GetRequiredService<ILoggerFactory>()
                    );
                })
                .AddSingleton<CacheInterceptor>()
                .AddSingleton<CacheProxyFactory>()
                .AddSingleton<CacheInvalidator>();
        }

        private static IServiceProvider GetDistributedCacheServiceProvider(Action<IServiceCollection> registerDedicatedDistributedCache)
        {
            if (registerDedicatedDistributedCache == null) return null;

            var distributedCacheServiceCollection = new ServiceCollection();
            registerDedicatedDistributedCache.Invoke(distributedCacheServiceCollection);

            var distributedCacheServiceInfo = distributedCacheServiceCollection.FirstOrDefault(x => x.ServiceType == typeof(IDistributedCache));
            if (distributedCacheServiceInfo == null) throw new ArgumentException($"The {nameof(IDistributedCache)} was not registered.", nameof(registerDedicatedDistributedCache));

            return distributedCacheServiceCollection.BuildServiceProvider();
        }

        private static void ReplaceCacheableServices(IServiceCollection services, IDictionary<int, ServiceDescriptor> cacheableServices)
        {
            foreach (var pair in cacheableServices)
            {
                services.RemoveAt(pair.Key);

                var serviceDescriptor = pair.Value;
                ServiceDescriptor proxyServiceDescriptor;

                if (serviceDescriptor.ImplementationInstance != null)
                {
                    proxyServiceDescriptor = new ServiceDescriptor(
                        serviceDescriptor.ServiceType,
                        serviceProvider => CreateCacheableServiceProxy(
                            serviceProvider,
                            serviceDescriptor.ServiceType,
                            serviceDescriptor.ImplementationInstance
                        ),
                        serviceDescriptor.Lifetime
                    );
                }
                else if (serviceDescriptor.ImplementationFactory != null)
                {
                    proxyServiceDescriptor = new ServiceDescriptor(
                        serviceDescriptor.ServiceType,
                        serviceProvider => CreateCacheableServiceProxy(
                            serviceProvider,
                            serviceDescriptor.ServiceType,
                            serviceDescriptor.ImplementationFactory(serviceProvider)
                        ),
                        serviceDescriptor.Lifetime
                    );
                }
                else
                {
                    proxyServiceDescriptor = new ServiceDescriptor(
                        serviceDescriptor.ServiceType,
                        serviceProvider => CreateCacheableServiceProxy(
                            serviceProvider,
                            serviceDescriptor.ServiceType,
                            ActivatorUtilities.CreateInstance(serviceProvider, serviceDescriptor.ImplementationType)
                        ),
                        serviceDescriptor.Lifetime
                    );
                }

                services.Insert(pair.Key, proxyServiceDescriptor);
            }
        }

        private static bool IsCacheableServiceType(Type type, IDictionary<Type, IDictionary<string, IMethodCacheOptions>> serviceMethodCacheOptions)
        {
            if (serviceMethodCacheOptions.ContainsKey(type)) return true;

            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);

            if (type.IsInterface)
            {
                return methods.Any(x => IsCacheableMethod(x));
            }

            var cacheable = false;

            foreach (var method in methods)
            {
                if (IsCacheableMethod(method))
                {
                    if (!method.IsVirtual) throw new ArgumentException($"The method '{type.FullName}.{method.Name} has a cache attribute, but it is not declared as virtual.'", nameof(method));
                    cacheable = true;
                }
            }

            return cacheable;
        }

        private static bool IsCacheableMethod(MethodInfo method)
        {
            return method.GetCustomAttribute<CacheAttribute>() != null;
        }

        private static object CreateCacheableServiceProxy(IServiceProvider serviceProvider, Type serviceType, object instance)
        {
            var cacheProxyFactory = serviceProvider.GetRequiredService<CacheProxyFactory>();
            return cacheProxyFactory.Create(
                serviceType,
                instance,
                parameterType => serviceProvider.GetService(parameterType)
            );
        }
    }
}