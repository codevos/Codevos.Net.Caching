using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codevos.Net.Caching.Interceptors;
using Codevos.Net.Caching.Tests.Infrastructure;
using Castle.DynamicProxy;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xbehave;

namespace Codevos.Net.Caching.Tests
{
    public class ServiceCollectionExtensionTests
    {
        [Scenario]
        public void AddMethodResultCachingRegistersExpectedServices()
        {
            IServiceCollection serviceCollection = null;

            "Given a new service collection".x(() =>
            {
                serviceCollection = new ServiceCollection();
            });

            "When adding method result caching".x(() =>
            {
                serviceCollection
                    .AddMethodResultCaching();
            });

            "Then all expected services are registered".x(() =>
            {
                var expectedServiceTypes = new[]
                {
                    typeof(IDistributedCache),
                    typeof(ProxyGenerator),
                    typeof(HashCalculator),
                    typeof(CacheOptions),
                    typeof(CacheKeyProvider),
                    typeof(MethodResultCache),
                    typeof(CacheInterceptor),
                    typeof(CacheProxyFactory),
                    typeof(CacheInvalidator)
                };

                var actualServiceTypes = serviceCollection.ToDictionary(x => x.ServiceType, y => y.Lifetime);

                foreach (var serviceType in expectedServiceTypes)
                {
                    var contains = actualServiceTypes.TryGetValue(serviceType, out ServiceLifetime serviceLifetime);
                    contains.Should().BeTrue();
                    serviceLifetime.Should().Be(ServiceLifetime.Singleton);
                }
            });
        }

        [Scenario]
        public void AddMethodResultCachingCreatesInterfaceProxy()
        {
            IServiceProvider serviceProvider = null;
            IBookService bookService = null;
            IUserService userService = null; 

            "Given a service provider".x(() =>
            {
                serviceProvider = new ServiceCollection()
                    .AddSingleton<CallCounter<BookService>>()
                    .AddSingleton<CallCounter<UserService>>()
                    .AddSingleton<IBookService, BookService>()
                    .AddSingleton<IUserService, UserService>()
                    .AddLogging()
                    .AddMethodResultCaching(options => options.AddCache<IBookService>())
                    .BuildServiceProvider();
            });

            "When getting the cache enabled services from the service provider".x(() =>
            {
                bookService = serviceProvider.GetService<IBookService>();
                userService = serviceProvider.GetService<IUserService>();
            });

            "Then the services are replaced with proxies".x(() =>
            {
                bookService.Should().NotBeNull();

                var bookServiceType = bookService.GetType();
                bookServiceType.FullName.Should().Be($"Castle.Proxies.{nameof(IBookService)}Proxy");

                userService.Should().NotBeNull();

                var userServiceType = userService.GetType();
                userServiceType.FullName.Should().Be($"Castle.Proxies.{nameof(IUserService)}Proxy");
            });
        }

        [Scenario]
        public void AddMethodResultCachingWithNonVirtualMethodThrowsException()
        {
            IServiceCollection serviceCollection = null;

            "Given a service collection".x(() =>
            {
                serviceCollection = new ServiceCollection();
            });

            "And a service implementation with a non virtual cachable method".x(() =>
            {
                serviceCollection
                    .AddSingleton<FailingUserService>();
            });

            ArgumentException argumentException = null;

            "When registering method result caching".x(() =>
            {
                try
                {
                    serviceCollection
                        .AddMethodResultCaching();
                }
                catch (ArgumentException argEx)
                {
                    argumentException = argEx;
                }
            });

            "Then an argument exception occurs".x(() =>
            {
                argumentException.Should().NotBeNull();
            });
        }

        [Scenario]
        public void AddMethodResultCachingCreatesClassProxy()
        {
            IServiceProvider serviceProvider = null;
            BookService bookService = null;
            CacheableUserService cacheableUserService = null;

            "Given a service provider".x(() =>
            {
                serviceProvider = new ServiceCollection()
                    .AddSingleton<CallCounter<BookService>>()
                    .AddSingleton<CallCounter<CacheableUserService>>()
                    .AddSingleton<BookService>()
                    .AddSingleton<CacheableUserService>()
                    .AddLogging()
                    .AddMethodResultCaching(options => options.AddCache<BookService>())
                    .BuildServiceProvider();
            });

            "When getting the cache enabled services from the service provider".x(() =>
            {
                bookService = serviceProvider.GetService<BookService>();
                cacheableUserService = serviceProvider.GetService<CacheableUserService>();
            });

            "Then the service is replaced with a proxy".x(() =>
            {
                bookService.Should().NotBeNull();
                var bookServiceType = bookService.GetType();
                bookServiceType.FullName.Should().Be($"Castle.Proxies.{nameof(BookService)}Proxy");

                cacheableUserService.Should().NotBeNull();
                var cacheableUserServiceType = cacheableUserService.GetType();
                cacheableUserServiceType.FullName.Should().Be($"Castle.Proxies.{nameof(CacheableUserService)}Proxy");
            });
        }

        [Scenario]
        public void CacheEnabledMethodHitsCacheAtSecondCall()
        {
            IServiceProvider serviceProvider = null;
            IBookService bookService = null;
            IUserService userService = null;

            var argumentResolverHits = new List<KeyValuePair<Type, object>>();
            var testKeySuffix = "testkeysuffix";

            "Given a service with a cache enabled method".x(() =>
            {
                serviceProvider = new ServiceCollection()
                    .AddSingleton<CallCounter<BookService>>()
                    .AddSingleton<CallCounter<UserService>>()
                    .AddSingleton<IBookService, BookService>()
                    .AddSingleton<IUserService, UserService>()
                    .AddLogging()
                    .AddMethodResultCaching(
                        configure: options =>
                        {
                            options
                                .AddCache<IBookService>()
                                .SetCacheKeyArgumentResolver((type, obj) =>
                                {
                                    argumentResolverHits.Add(new KeyValuePair<Type, object>(type, obj));
                                    return obj;
                                });
                        },
                        cacheKeySuffixFactory: x => testKeySuffix
                    )
                    .BuildServiceProvider();

                bookService = serviceProvider.GetRequiredService<IBookService>();
                userService = serviceProvider.GetRequiredService<IUserService>();
            });

            "When calling the cache enabled method twice".x(() =>
            {
                bookService.GetBookTitle(1);
                bookService.GetBookTitle(1);
                userService.GetFullName(2);
                userService.GetFullName(2);
            });

            "Then the cache should be hit once".x(() =>
            {
                var bookCallCounter = serviceProvider.GetRequiredService<CallCounter<BookService>>();
                bookCallCounter.Count.Should().Be(1);

                var userCallCounter = serviceProvider.GetRequiredService<CallCounter<UserService>>();
                userCallCounter.Count.Should().Be(1);
            });

            "And the cache key argument resolver should be hit twice".x(() =>
            {
                argumentResolverHits.Count.Should().Be(4);
                
                argumentResolverHits[0].Key.Should().Be(typeof(int));
                argumentResolverHits[0].Value.Should().Be(1);
                argumentResolverHits[1].Should().BeEquivalentTo(argumentResolverHits[0]);

                argumentResolverHits[2].Key.Should().Be(typeof(int));
                argumentResolverHits[2].Value.Should().Be(2);
                argumentResolverHits[3].Should().BeEquivalentTo(argumentResolverHits[2]);
            });

            "And the distributed cache should contain the expected cache entry".x(async () =>
            {
                var cacheKeyProvider = serviceProvider.GetRequiredService<CacheKeyProvider>();
                var distributedCache = serviceProvider.GetRequiredService<IDistributedCache>();

                var cacheKey = await cacheKeyProvider.GetCacheKey(typeof(IBookService).GetMethod(nameof(IBookService.GetBookTitle)), new object[] { 1 });
                cacheKey.Should().Contain(testKeySuffix);
                
                var cacheEntry = await distributedCache.GetAsync<string>(cacheKey);
                cacheEntry.Loaded.Should().BeTrue();
                cacheEntry.Value.Should().Be("Twenty thousand leagues under the sea");

                cacheKey = await cacheKeyProvider.GetCacheKey(typeof(IUserService).GetMethod(nameof(IUserService.GetFullName)), new object[] { 2 });
                cacheKey.Should().Contain(testKeySuffix);

                cacheEntry = await distributedCache.GetAsync<string>(cacheKey);
                cacheEntry.Loaded.Should().BeTrue();
                cacheEntry.Value.Should().Be(Constants.FullName);
            });
        }

        [Scenario]
        public void DedicatedDistributedCacheEnabledMethodHitsCacheAtSecondCall()
        {
            IServiceProvider serviceProvider = null;
            IBookService bookService = null;
            IUserService userService = null;

            "Given a service with a cache enabled method on a dedicated cache".x(() =>
            {
                serviceProvider = new ServiceCollection()
                    .AddSingleton<CallCounter<BookService>>()
                    .AddSingleton<CallCounter<UserService>>()
                    .AddSingleton<IBookService, BookService>()
                    .AddSingleton<IUserService, UserService>()
                    .AddLogging()
                    .AddMethodResultCaching(
                        configure: options => options.AddCache<IBookService>(),
                        registerDedicatedDistributedCache: services => services.AddDistributedMemoryCache()
                    )
                    .BuildServiceProvider();

                bookService = serviceProvider.GetRequiredService<IBookService>();
                userService = serviceProvider.GetRequiredService<IUserService>();
            });

            "When calling the cache enabled method twice".x(() =>
            {
                bookService.GetBookTitle(1);
                bookService.GetBookTitle(1);
                userService.GetFullName(1);
                userService.GetFullName(1);
            });

            "Then the cache should be hit once".x(() =>
            {
                var bookCallCounter = serviceProvider.GetRequiredService<CallCounter<BookService>>();
                bookCallCounter.Count.Should().Be(1);

                var userCallCounter = serviceProvider.GetRequiredService<CallCounter<UserService>>();
                userCallCounter.Count.Should().Be(1);
            });

            $"And the {nameof(IDistributedCache)} should not exist in the service provider".x(() =>
            {
                serviceProvider.GetService<IDistributedCache>().Should().BeNull();
            });
        }

        [Scenario]
        public void CacheEnabledMethodSetsCorrectSlidingExpiration()
        {
            IServiceProvider serviceProvider = null;
            IUserService userService = null;

            var distributedCacheMock = new Mock<IDistributedCache>();
            string cacheKey = null;
            DistributedCacheEntryOptions distributedCacheEntryOptions = null;
    
            distributedCacheMock.Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()
                 ))
                 .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((key, b, cacheEntryOptions, c) =>
                 {
                     if (!string.Equals(key, cacheKey)) return;
                     distributedCacheEntryOptions = cacheEntryOptions;
                 })
                 .Returns(Task.CompletedTask);

            "Given a service with a cache enabled method with an expiry".x(() =>
            {
                serviceProvider = new ServiceCollection()
                    .AddSingleton<CallCounter<UserService>>()
                    .AddSingleton<IUserService, UserService>()
                    .AddSingleton(distributedCacheMock.Object)
                    .AddLogging()
                    .AddMethodResultCaching()
                    .BuildServiceProvider();

                userService = serviceProvider.GetRequiredService<IUserService>();
            });

            "When calling the cache enabled method".x(async () =>
            {
                var cacheKeyProvider = serviceProvider.GetRequiredService<CacheKeyProvider>();
                cacheKey = await cacheKeyProvider.GetCacheKey(typeof(IUserService).GetMethod(nameof(IUserService.GetFirstName)), new object[] { 1 });

                userService.GetFirstName(1);
            });

            "Then the created cache entry should have the expected absolute expiration".x(() =>
            {
                distributedCacheEntryOptions.Should().NotBeNull();

                distributedCacheEntryOptions.AbsoluteExpirationRelativeToNow.Should().Be(
                    new TimeSpan(
                        Constants.ExpirationHours,
                        Constants.ExpirationMinutes,
                        Constants.ExpirationSeconds
                    )
                );
            });
        }

        [Scenario]
        public void CacheEnabledMethodSetsCorrectAbsoluteExpiration()
        {
            IServiceProvider serviceProvider = null;
            IUserService userService = null;

            var distributedCacheMock = new Mock<IDistributedCache>();
            string cacheKey = null;
            DistributedCacheEntryOptions distributedCacheEntryOptions = null;

            distributedCacheMock.Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()
                 ))
                 .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((key, b, cacheEntryOptions, c) =>
                 {
                     if (!string.Equals(key, cacheKey)) return;
                     distributedCacheEntryOptions = cacheEntryOptions;
                 })
                 .Returns(Task.CompletedTask);

            "Given a service with a cache enabled method with an expiry".x(() =>
            {
                serviceProvider = new ServiceCollection()
                    .AddSingleton<CallCounter<UserService>>()
                    .AddSingleton<IUserService, UserService>()
                    .AddSingleton(distributedCacheMock.Object)
                    .AddLogging()
                    .AddMethodResultCaching()
                    .BuildServiceProvider();

                userService = serviceProvider.GetRequiredService<IUserService>();
            });

            "When calling the cache enabled method".x(async () =>
            {
                var cacheKeyProvider = serviceProvider.GetRequiredService<CacheKeyProvider>();
                cacheKey = await cacheKeyProvider.GetCacheKey(typeof(IUserService).GetMethod(nameof(IUserService.GetLastName)), new object[] { 1 });

                userService.GetLastName(1);
            });

            "Then the created cache entry should have the expected absolute expiration".x(() =>
            {
                distributedCacheEntryOptions.Should().NotBeNull();

                distributedCacheEntryOptions.SlidingExpiration.Should().Be(
                    new TimeSpan(
                        Constants.ExpirationHours,
                        Constants.ExpirationMinutes,
                        Constants.ExpirationSeconds
                    )
                );
            });
        }

        [Scenario]
        public void CacheEnabledMethodWithDifferentParametersAddMultipleCacheEntries()
        {
            IServiceProvider serviceProvider = null;
            IUserService userService = null;

            "Given a service with a cache enabled method with an expiry".x(() =>
            {
                serviceProvider = new ServiceCollection()
                    .AddSingleton<CallCounter<UserService>>()
                    .AddSingleton<IUserService, UserService>()
                    .AddLogging()
                    .AddMethodResultCaching()
                    .BuildServiceProvider();

                userService = serviceProvider.GetRequiredService<IUserService>();
            });

            "When calling the cache enabled method with three different arguments".x(() =>
            {
                userService.GetFirstName(1);
                userService.GetFirstName(2);
                userService.GetFirstName(3);
            });

            "Then all expected cache entries should exist".x(async () =>
            {
                var cacheKeyProvider = serviceProvider.GetRequiredService<CacheKeyProvider>();
                var distributedCache = serviceProvider.GetRequiredService<IDistributedCache>();

                var methodInfo = typeof(IUserService).GetMethod(nameof(userService.GetFirstName));

                var cacheEntries = new[]
                {
                    await distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(methodInfo, new object[] { 1 }), default),
                    await distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(methodInfo, new object[] { 2 }), default),
                    await distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(methodInfo, new object[] { 3 }), default)
                };

                var i = 1;
                foreach (var cacheEntry in cacheEntries)
                {
                    cacheEntry.Loaded.Should().BeTrue();
                    cacheEntry.Value.Should().Be($"{Constants.FirstName}{i}");
                    i++;
                }
            });
        }

        [Scenario]
        public void CacheInvalidatorClearsAllCacheEntriesForService()
        {
            IServiceProvider serviceProvider = null;
            IUserService userService = null;

            "Given a service with cache enabled methods".x(() =>
            {
                serviceProvider = new ServiceCollection()
                    .AddSingleton<CallCounter<UserService>>()
                    .AddSingleton<IUserService, UserService>()
                    .AddLogging()
                    .AddMethodResultCaching()
                    .BuildServiceProvider();

                userService = serviceProvider.GetRequiredService<IUserService>();
            });

            "When calling the cache enabled methods times".x(() =>
            {
                userService.GetFullName(1);
                userService.GetFullName(2);
                userService.GetFirstName(1);
                userService.GetFirstName(2);
                userService.GetLastName(1);
                userService.GetLastName(2);
            });

            "And invalidating the cache for the whole service".x(async () =>
            {
                var cacheInvalidator = serviceProvider.GetRequiredService<CacheInvalidator>();
                await cacheInvalidator.Invalidate<IUserService>();
            });

            "Then the cache should be empty".x(async () =>
            {
                var cacheKeyProvider = serviceProvider.GetRequiredService<CacheKeyProvider>();
                var distributedCache = serviceProvider.GetRequiredService<IDistributedCache>();

                var fullNameMethodInfo = typeof(IUserService).GetMethod(nameof(userService.GetFullName));
                var firstNameMethodInfo = typeof(IUserService).GetMethod(nameof(userService.GetFirstName));
                var lastNameMethodInfo = typeof(IUserService).GetMethod(nameof(userService.GetLastName));

                var cacheEntries = await Task.WhenAll(
                    distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(fullNameMethodInfo, new object[] { 1 }), default),
                    distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(fullNameMethodInfo, new object[] { 2 }), default),
                    distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(firstNameMethodInfo, new object[] { 1 }), default),
                    distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(firstNameMethodInfo, new object[] { 2 }), default),
                    distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(lastNameMethodInfo, new object[] { 1 }), default),
                    distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(lastNameMethodInfo, new object[] { 2 }), default)
                );

                foreach (var cacheEntry in cacheEntries)
                {
                    cacheEntry.Loaded.Should().BeFalse();
                }
            });
        }

        [Scenario]
        public void CacheInvalidatorClearsAllCacheEntriesForMethod()
        {
            IServiceProvider serviceProvider = null;
            IUserService userService = null;

            "Given a service with cache enabled methods".x(() =>
            {
                serviceProvider = new ServiceCollection()
                    .AddSingleton<CallCounter<UserService>>()
                    .AddSingleton<IUserService, UserService>()
                    .AddLogging()
                    .AddMethodResultCaching()
                    .BuildServiceProvider();

                userService = serviceProvider.GetRequiredService<IUserService>();
            });

            "When calling the cache enabled methods multiple times".x(() =>
            {
                userService.GetFullName(1);
                userService.GetFullName(2);
                userService.GetFirstName(1);
                userService.GetFirstName(2);
                userService.GetLastName(1);
                userService.GetLastName(2);
            });

            "And invalidating the cache for one method".x(async () =>
            {
                var cacheInvalidator = serviceProvider.GetRequiredService<CacheInvalidator>();
                await cacheInvalidator.Invalidate<IUserService>(x => nameof(x.GetFullName));
            });

            "Then the cache should be empty".x(async () =>
            {
                var cacheKeyProvider = serviceProvider.GetRequiredService<CacheKeyProvider>();
                var distributedCache = serviceProvider.GetRequiredService<IDistributedCache>();

                var fullNameMethodInfo = typeof(IUserService).GetMethod(nameof(userService.GetFullName));
                var firstNameMethodInfo = typeof(IUserService).GetMethod(nameof(userService.GetFirstName));
                var lastNameMethodInfo = typeof(IUserService).GetMethod(nameof(userService.GetLastName));

                var fullNameCacheEntries = await Task.WhenAll(
                    distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(fullNameMethodInfo, new object[] { 1 }), default),
                    distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(fullNameMethodInfo, new object[] { 2 }), default)
                );

                var remainingCacheEntries = await Task.WhenAll(
                    distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(firstNameMethodInfo, new object[] { 1 }), default),
                    distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(firstNameMethodInfo, new object[] { 2 }), default),
                    distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(lastNameMethodInfo, new object[] { 1 }), default),
                    distributedCache.GetAsync<string>(await cacheKeyProvider.GetCacheKey(lastNameMethodInfo, new object[] { 2 }), default)
                );

                foreach (var cacheEntry in fullNameCacheEntries)
                {
                    cacheEntry.Loaded.Should().BeFalse();
                }

                foreach (var cacheEntry in remainingCacheEntries)
                {
                    cacheEntry.Loaded.Should().BeTrue();
                }
            });
        }
    }
}