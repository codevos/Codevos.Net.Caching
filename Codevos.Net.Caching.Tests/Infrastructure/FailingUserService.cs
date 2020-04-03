using Codevos.Net.Caching.Attributes;

namespace Codevos.Net.Caching.Tests.Infrastructure
{
    public class FailingUserService
    {
        [Cache]
        public string GetFullName(int userId)
        {
            return Constants.FullName;
        }
    }
}