using Codevos.Net.Caching.Attributes;

namespace Codevos.Net.Caching.Tests.Infrastructure
{
    public class CacheableUserService
    {
        private readonly CallCounter<CacheableUserService> CallCounter;

        public CacheableUserService(
            CallCounter<CacheableUserService> callCounter
        )
        {
            CallCounter = callCounter;
        }

        [Cache]
        public virtual string GetFullName(int userId)
        {
            CallCounter.Increment();
            return Constants.FullName;
        }
    }
}