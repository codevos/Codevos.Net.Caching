using System;

namespace Codevos.Net.Caching.Tests.Infrastructure
{
    public interface IBookService
    {
        string GetBookTitle(int id);

        string GetAuthor(int id);

        DateTime GetPublishedDate(int id);
    }
}
