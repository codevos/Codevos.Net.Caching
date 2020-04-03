using System;
using System.Collections.Generic;
using System.Text;

namespace Codevos.Net.Caching.Tests.Infrastructure
{
    public class BookService : IBookService
    {
        private readonly CallCounter<BookService> CallCounter;

        public BookService(
            CallCounter<BookService> callCounter
        )
        {
            CallCounter = callCounter ?? throw new ArgumentNullException(nameof(callCounter));
        }

        public virtual string GetBookTitle(int id)
        {
            CallCounter.Increment();
            if (id == 1) return "Twenty thousand leagues under the sea";
            if (id == 2) return "Harry Potter and the philosopher's stone";
            return null;
        }

        public virtual string GetAuthor(int id)
        {
            if (id == 1) return "Jules Verne";
            if (id == 2) return "J.K. Rowling";
            return null;
        }

        public virtual DateTime GetPublishedDate(int id)
        {
            if (id == 1) return new DateTime(1870, 1, 1);
            if (id == 2) return new DateTime(1997, 26, 6);
            return DateTime.MinValue;
        }
    }
}