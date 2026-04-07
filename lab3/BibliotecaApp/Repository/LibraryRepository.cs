using Microsoft.EntityFrameworkCore;

namespace BibliotecaApp.DataBase
{
    public class LibraryRepository
    {
        public List<Author> GetAllAuthors()
        {
            using var context = new LibraryContext();
            return context.Authors.ToList();
        }

        public List<Category> GetAllCategories()
        {
            using var context = new LibraryContext();
            return context.Categories.ToList();
        }

        public List<Book> GetBooksByAuthor(int authorId)
        {
            using var context = new LibraryContext();

            // Folosim .Include() pentru a demonstra EAGER LOADING cerut în document
            // Asta aduce cartea și categoriile ei printr-un singur query SQL (JOIN pe fundal)
            return context.Books
                .Include(b => b.Categories)
                .Where(b => b.AuthorId == authorId)
                .ToList();
        }

        public void AddBookWithCategories(Book book, List<int> categoryIds)
        {
            using var context = new LibraryContext();

            // Folosim tranzacții ORM explicite pentru operațiile de scriere
            using var transaction = context.Database.BeginTransaction();

            try
            {
                // Preluăm categoriile din baza de date pentru a le lega de noua carte
                var selectedCategories = context.Categories
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToList();

                book.Categories = selectedCategories;

                context.Books.Add(book);
                context.SaveChanges(); // Execută INSERT-urile

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
                // Căutăm cartea existentă, inclusiv categoriile ei curente
                var bookToUpdate = context.Books
                    .Include(b => b.Categories)
                    .FirstOrDefault(b => b.Id == bookInfo.Id);

                if (bookToUpdate != null)
                {
                    // Actualizăm câmpurile simple
                    bookToUpdate.Title = bookInfo.Title;
                    bookToUpdate.PublicationYear = bookInfo.PublicationYear;

                    // Actualizăm relația M:N (ștergem categoriile vechi și le punem pe cele noi)
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

                    context.SaveChanges(); // Execută UPDATE și DELETE/INSERT în tabelul de legătură
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
                context.SaveChanges(); // Șterge cartea (și legăturile M:N datorită cascade delete-ului)
            }
        }
    }
}