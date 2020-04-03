using System.Linq;

namespace Codevos.Net.Caching.Tests.Infrastructure
{
    public class UserService : IUserService
    {
        private readonly CallCounter<UserService> CallCounter;

        public UserService(
            CallCounter<UserService> callCounter
        )
        {
            CallCounter = callCounter;
        }

        public string GetFullName(int userId)
        {
            CallCounter.Increment();
            return Constants.FullName;
        }

        public string GetFirstName(int userId)
        {
            return $"{Constants.FirstName}{userId}";
        }

        public string GetLastName(int userId)
        {
            return Constants.LastName;
        }
    }
}