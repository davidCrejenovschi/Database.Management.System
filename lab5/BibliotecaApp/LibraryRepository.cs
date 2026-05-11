using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BibliotecaApp
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
        private static readonly MemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        private static int _cacheHits = 0;
        private static int _cacheMisses = 0;

        #region Cache Stats
        public string GetCacheStats()
        {
            int total = _cacheHits + _cacheMisses;
            double hitRate = total > 0 ? (double)_cacheHits / total * 100 : 0;
            return $"Cache Stats -> Hits: {_cacheHits} | Misses: {_cacheMisses} | Hit Rate: {hitRate:F1}%";
        }
        #endregion

        #region Author CRUD
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
                _cache.Set(cacheKey, author, TimeSpan.FromMinutes(5));
            }
            return (author, "CACHE MISS");
        }

        public void InvalidateAuthorCache(int id)
        {
            _cache.Remove($"author_{id}");
        }

        public List<Author> GetAllAuthors()
        {
            using var context = new LibraryContext();
            return context.Authors.ToList();
        }

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

        public void AddAuthor(Author author)
        {
            using var context = new LibraryContext();
            context.Authors.Add(author);
            context.SaveChanges();
        }

        public void UpdateAuthor(Author author)
        {
            using var context = new LibraryContext();
            context.Authors.Update(author);
            context.SaveChanges();
            InvalidateAuthorCache(author.Id);
        }

        public void DeleteAuthor(int id)
        {
            using var context = new LibraryContext();
            var author = context.Authors.Find(id);
            if (author != null)
            {
                context.Authors.Remove(author);
                context.SaveChanges();
                InvalidateAuthorCache(id);
            }
        }
        #endregion

        #region Publisher CRUD (New Entity)
        public List<Publisher> GetAllPublishers()
        {
            using var context = new LibraryContext();
            return context.Publishers.ToList();
        }

        public Publisher? GetPublisherById(int id)
        {
            using var context = new LibraryContext();
            return context.Publishers.Find(id);
        }

        public void AddPublisher(Publisher publisher)
        {
            using var context = new LibraryContext();
            context.Publishers.Add(publisher);
            context.SaveChanges();
        }

        public void UpdatePublisher(Publisher publisher)
        {
            using var context = new LibraryContext();
            context.Publishers.Update(publisher);
            context.SaveChanges();
        }

        public void DeletePublisher(int id)
        {
            using var context = new LibraryContext();
            var publisher = context.Publishers.Find(id);
            if (publisher != null)
            {
                context.Publishers.Remove(publisher);
                context.SaveChanges();
            }
        }
        #endregion

        #region Category CRUD
        public List<Category> GetAllCategories()
        {
            using var context = new LibraryContext();
            return context.Categories.ToList();
        }

        public void AddCategory(Category category)
        {
            using var context = new LibraryContext();
            context.Categories.Add(category);
            context.SaveChanges();
        }

        public void UpdateCategory(Category category)
        {
            using var context = new LibraryContext();
            context.Categories.Update(category);
            context.SaveChanges();
        }

        public void DeleteCategory(int id)
        {
            using var context = new LibraryContext();
            var category = context.Categories.Find(id);
            if (category != null)
            {
                context.Categories.Remove(category);
                context.SaveChanges();
            }
        }
        #endregion

        #region Book CRUD & Optimistic Locking
        public List<Book> GetBooksByAuthor(int authorId)
        {
            using var context = new LibraryContext();
            return context.Books
                .Include(b => b.Categories)
                .Where(b => b.AuthorId == authorId)
                .ToList();
        }

        public CursorPagedResult<Book> GetBooksByAuthorAfter(int authorId, int lastId, int pageSize)
        {
            using var context = new LibraryContext();
            var books = context.Books
                .Include(b => b.Categories)
                .Where(b => b.AuthorId == authorId && b.Id > lastId)
                .OrderBy(b => b.Id)
                .Take(pageSize)
                .ToList();

            return new CursorPagedResult<Book> { Items = books, PageSize = pageSize };
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

                // Initialize Version for Optimistic Locking
                book.Version = 1;

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
                var bookToUpdate = context.Books
                    .Include(b => b.Categories)
                    .FirstOrDefault(b => b.Id == bookInfo.Id);

                if (bookToUpdate != null)
                {
                    // Verify the version to implement optimistic locking
                    // We tell EF Core that the original value when we loaded the form was bookInfo.Version
                    context.Entry(bookToUpdate).Property(b => b.Version).OriginalValue = bookInfo.Version;

                    bookToUpdate.Title = bookInfo.Title;
                    bookToUpdate.PublicationYear = bookInfo.PublicationYear;
                    bookToUpdate.ISBN = bookInfo.ISBN;
                    bookToUpdate.PublisherId = bookInfo.PublisherId;

                    // Increment the version for the next save
                    bookToUpdate.Version++;

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

                    context.SaveChanges();
                    transaction.Commit();
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                transaction.Rollback();
                // We rethrow a custom exception so the UI can catch it and show a friendly message
                throw new InvalidOperationException("Concurrency conflict: The book was modified by another user since you loaded it. Please refresh and try again.", ex);
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
                context.SaveChanges();
            }
        }
        #endregion


        #region Admin Features (Soft Delete Management)

        public List<Book> GetAllBooksIncludingDeleted()
        {
            using var context = new LibraryContext();
            return context.Books.IgnoreQueryFilters().ToList();
        }

        public void RestoreBook(int bookId)
        {
            using var context = new LibraryContext();
            var book = context.Books.IgnoreQueryFilters().FirstOrDefault(b => b.Id == bookId);

            if (book != null && book.IsDeleted)
            {
                book.IsDeleted = false;
                book.DeletedAt = null;
                book.DeletedBy = null;
                context.SaveChanges();
            }
        }

        public void HardDeleteBook(int bookId)
        {
            using var context = new LibraryContext();
            // Using ExecuteDelete() directly executes the SQL DELETE query, 
            // bypassing the SaveChanges() override that enforces soft delete.
            context.Books.Where(b => b.Id == bookId).ExecuteDelete();
        }
        #endregion
    }
}