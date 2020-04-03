namespace Codevos.Net.Caching.Tests.Infrastructure
{
    public class CallCounter<T>
    {
        public int Count { get; private set; }

        public void Increment()
        {
            Count++;
        }
    }
}
