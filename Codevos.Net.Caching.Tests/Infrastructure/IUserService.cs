using Codevos.Net.Caching.Attributes;

namespace Codevos.Net.Caching.Tests.Infrastructure
{
    public interface IUserService
    {
        [Cache]
        string GetFullName(int userId);

        [Cache(ExpirationHours = Constants.ExpirationHours, ExpirationMinutes = Constants.ExpirationMinutes, ExpirationSeconds = Constants.ExpirationSeconds)]
        string GetFirstName(int userId);

        [Cache(ExpirationHours = Constants.ExpirationHours, ExpirationMinutes = Constants.ExpirationMinutes, ExpirationSeconds = Constants.ExpirationSeconds, SlidingExpiration = true)]
        string GetLastName(int userId);
    }
}
