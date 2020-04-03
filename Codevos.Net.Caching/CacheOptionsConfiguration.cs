using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Codevos.Net.Caching.Attributes;

namespace Codevos.Net.Caching
{
    /// <summary>
    /// Cache options configuration.
    /// </summary>
    public class CacheOptionsConfiguration
    {
        internal IDictionary<Type, IDictionary<string, IMethodCacheOptions>> Configurations { get; }
        internal IEnumerable<Type> CacheKeyIgnoreTypes { get; private set; }
        internal Func<Type, object, object> CacheKeyArgumentResolver { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheOptionsConfiguration"/> class.
        /// </summary>
        public CacheOptionsConfiguration()
        {
            Configurations = new Dictionary<Type, IDictionary<string, IMethodCacheOptions>>();
        }

        /// <summary>
        /// Adds caching capabilities for each method of the given service type. 
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="configure">Optional method cache options.</param>
        /// <returns>The <see cref="CacheOptionsConfiguration"/> to use for further configuration.</returns>
        public CacheOptionsConfiguration AddCache<TService>(Action<IMethodCacheOptions> configure = null)
        {
            var serviceType = typeof(TService);
            var methodCacheOptions = GetOrAddMethodCacheOptions(serviceType);

            var cacheAttribute = new CacheAttribute();
            configure?.Invoke(cacheAttribute);

            IEnumerable<MethodInfo> methods = serviceType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            if (serviceType.IsClass)
            {
                methods = methods.Where(x => x.IsVirtual);
            }

            foreach (var method in methods)
            {
                methodCacheOptions[method.Name] = cacheAttribute;
            }

            return this;
        }

        /// <summary>
        /// Adds caching capabilities for the method with the given name of the given service type. 
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="methodNameFactory">The method name factory. Example usage: CacheOptions.AddCache&lt;IMyService&gt;(x =&gt; nameof(x.GetAll))</param>
        /// <param name="configure">Optional method cache options.</param>
        /// <returns>The <see cref="CacheOptionsConfiguration"/> to use for further configuration.</returns>
        public CacheOptionsConfiguration AddCache<TService>(Func<TService, string> methodNameFactory, Action<IMethodCacheOptions> configure)
        {
            var serviceType = typeof(TService);
            var methodCacheOptions = GetOrAddMethodCacheOptions(serviceType);

            var cacheAttribute = new CacheAttribute();
            configure.Invoke(cacheAttribute);

            var methodName = methodNameFactory.Invoke(default);

            var method = serviceType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            if (method == null) throw new ArgumentException($"The method '{methodName}' does not exist on the type '{serviceType.FullName}'.", nameof(methodName));
            if (serviceType.IsClass && !method.IsVirtual) throw new ArgumentException($"The method '{serviceType.FullName}.{method.Name} is not declared as virtual.'", nameof(methodName));

            methodCacheOptions[methodName] = cacheAttribute;

            return this;
        }

        /// <summary>
        /// Sets the cache key ignore types.
        /// </summary>
        /// <param name="types">The types to ignore.</param>
        /// <returns>The <see cref="CacheOptionsConfiguration"/> to use for further configuration.</returns>
        public CacheOptionsConfiguration SetCacheKeyIgnoreTypes(IEnumerable<Type> types)
        {
            CacheKeyIgnoreTypes = types;
            return this;
        }

        /// <summary>
        /// Set the cache key argument resolver.
        /// </summary>
        /// <param name="resolver">The cache key argument resolver.</param>
        /// <returns>The <see cref="CacheOptionsConfiguration"/> to use for further configuration.</returns>
        public CacheOptionsConfiguration SetCacheKeyArgumentResolver(Func<Type, object, object> resolver)
        {
            CacheKeyArgumentResolver = resolver;
            return this;
        }

        private IDictionary<string, IMethodCacheOptions> GetOrAddMethodCacheOptions(Type serviceType)
        {
            if (!Configurations.TryGetValue(serviceType, out IDictionary<string, IMethodCacheOptions> methodCacheOptions))
            {
                methodCacheOptions = new Dictionary<string, IMethodCacheOptions>();
                Configurations.Add(serviceType, methodCacheOptions);
            }

            return methodCacheOptions;
        }
    }
}