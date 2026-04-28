using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Linq;

namespace BibliotecaApp.DataBase
{
   
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
    }

    public class CursorPagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageSize { get; set; }
    }

    public class LibraryRepository
    {

        // Static pentru a persista între instanțele de Repository
        private static readonly MemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        private static int _cacheHits = 0;
        private static int _cacheMisses = 0;

        public (Author? author, string status) GetAuthorByIdCached(int id)
        {
            string cacheKey = $"author_{id}";

            if (_cache.TryGetValue(cacheKey, out Author? cachedAuthor))
            {
                _cacheHits++;
                return (cachedAuthor, "CACHE HIT");
            }

            _cacheMisses++;
            using var context = new LibraryContext();
            var author = context.Authors.Find(id);

            if (author != null)
            {
                _cache.Set(cacheKey, author, TimeSpan.FromMinutes(5)); // TTL 5 minute [cite: 126]
            }

            return (author, "CACHE MISS");
        }

        public void InvalidateAuthorCache(int id)
        {
            _cache.Remove($"author_{id}"); // Implementează invalidarea [cite: 129]
        }

        public string GetCacheStats()
        {
            int total = _cacheHits + _cacheMisses;
            double hitRate = total > 0 ? (double)_cacheHits / total * 100 : 0;
            return $"Cache Stats -> Hits: {_cacheHits} | Misses: {_cacheMisses} | Hit Rate: {hitRate:F1}%";
        }

 

        public List<Author> GetAllAuthors()
        {
            using var context = new LibraryContext();
            return context.Authors.ToList();
        }

        // --- LAB 4: STRATEGY A (OFFSET PAGINATION) ---
        public PagedResult<Author> GetAuthorsPage(int pageNumber, int pageSize)
        {
            using var context = new LibraryContext();
            int offset = (pageNumber - 1) * pageSize;

            var authors = context.Authors
                .OrderBy(a => a.Id)
                .Skip(offset)
                .Take(pageSize)
                .ToList();

            int total = context.Authors.Count();

            return new PagedResult<Author>
            {
                Items = authors,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = total
            };
        }

        public List<Category> GetAllCategories()
        {
            using var context = new LibraryContext();
            return context.Categories.ToList();
        }

        public List<Book> GetBooksByAuthor(int authorId)
        {
            using var context = new LibraryContext();

            return context.Books
                .Include(b => b.Categories)
                .Where(b => b.AuthorId == authorId)
                .ToList();
        }

        // --- LAB 4: STRATEGY B (KEYSET / CURSOR PAGINATION) ---
        public CursorPagedResult<Book> GetBooksByAuthorAfter(int authorId, int lastId, int pageSize)
        {
            using var context = new LibraryContext();

            var books = context.Books
                .Include(b => b.Categories)
                .Where(b => b.AuthorId == authorId && b.Id > lastId)
                .OrderBy(b => b.Id)
                .Take(pageSize)
                .ToList();

            return new CursorPagedResult<Book>
            {
                Items = books,
                PageSize = pageSize
            };
        }

        public void AddBookWithCategories(Book book, List<int> categoryIds)
        {
            using var context = new LibraryContext();

            using var transaction = context.Database.BeginTransaction();

            try
            {
                var selectedCategories = context.Categories
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToList();

                book.Categories = selectedCategories;

                context.Books.Add(book);
                context.SaveChanges();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void UpdateBookWithCategories(Book bookInfo, List<int> newCategoryIds)
        {
            using var context = new LibraryContext();
            using var transaction = context.Database.BeginTransaction();

            try
            {
                // Find the existing book, including its current categories
                var bookToUpdate = context.Books
                    .Include(b => b.Categories)
                    .FirstOrDefault(b => b.Id == bookInfo.Id);

                if (bookToUpdate != null)
                {
                    // Update simple fields
                    bookToUpdate.Title = bookInfo.Title;
                    bookToUpdate.PublicationYear = bookInfo.PublicationYear;

                    // Update M:N relationship (clear old categories and add the new ones)
                    bookToUpdate.Categories.Clear();

                    if (newCategoryIds != null && newCategoryIds.Any())
                    {
                        var selectedCategories = context.Categories
                            .Where(c => newCategoryIds.Contains(c.Id))
                            .ToList();

                        foreach (var cat in selectedCategories)
                        {
                            bookToUpdate.Categories.Add(cat);
                        }
                    }

                    context.SaveChanges(); // Executes UPDATE and DELETE/INSERT in the junction table
                    transaction.Commit();
                }
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void DeleteBook(int bookId)
        {
            using var context = new LibraryContext();

            var book = context.Books.Find(bookId);
            if (book != null)
            {
                context.Books.Remove(book);
                context.SaveChanges(); // Deletes the book (and M:N links due to cascade delete)
            }
        }
    }
}