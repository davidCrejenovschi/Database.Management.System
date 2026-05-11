using Microsoft.EntityFrameworkCore;

namespace BibliotecaApp.DataBase
{
    public class DemoSimulator
    {
        public static void RunFullDemonstration()
        {
            Console.WriteLine("--- STARTING LAB 5 DEMONSTRATION ---");
            var repo = new LibraryRepository();

            // 1. SOFT DELETE DEMO
            Console.WriteLine("\n[1] Demonstrating Soft Delete...");

            int demoBookId = 0;

            using (var context = new LibraryContext())
            {
                // Create a dummy author first to satisfy the Foreign Key constraint
                var demoAuthor = new Author
                {
                    Name = "John Doe",
                    Nationality = "Test"
                };
                context.Authors.Add(demoAuthor);
                context.SaveChanges();

                var demoBook = new Book
                {
                    Title = "The Disappearing Act",
                    PublicationYear = 2026,
                    ISBN = "123-DEMO",
                    AuthorId = demoAuthor.Id, // Link to the valid author
                    Version = 1
                };

                context.Books.Add(demoBook);
                context.SaveChanges();

                demoBookId = demoBook.Id;
                Console.WriteLine($"Book added with ID: {demoBook.Id} and Author ID: {demoAuthor.Id}");
            }

            // Delete the book using standard repository method
            repo.DeleteBook(demoBookId);
            Console.WriteLine("Book deleted via standard method.");

            // Try to find it normally (should be hidden)
            using (var context = new LibraryContext())
            {
                var isVisible = context.Books.Any(b => b.Id == demoBookId);
                Console.WriteLine($"Is book visible in standard query? {isVisible}");
            }

            // Admin finds it and restores it
            var deletedBooks = repo.GetAllBooksIncludingDeleted().Where(b => b.IsDeleted).ToList();
            Console.WriteLine($"Admin found {deletedBooks.Count} soft-deleted books.");

            repo.RestoreBook(demoBookId);
            Console.WriteLine("Admin restored the book.");

            // 2. OPTIMISTIC LOCKING DEMO
            Console.WriteLine("\n[2] Demonstrating Optimistic Locking...");

            using (var contextA = new LibraryContext())
            using (var contextB = new LibraryContext())
            {
                // User A and User B load the exact same book at the same time
                var bookForUserA = contextA.Books.First(b => b.Id == demoBookId);
                var bookForUserB = contextB.Books.First(b => b.Id == demoBookId);

                Console.WriteLine($"Both users loaded Version: {bookForUserA.Version}");

                // User A modifies and saves first
                bookForUserA.Title = "Updated by User A";
                bookForUserA.Version++; // Manual increment
                contextA.SaveChanges();
                Console.WriteLine("User A saved successfully. Database version is now updated.");

                // User B tries to modify and save the stale data
                bookForUserB.Title = "Updated by User B";
                bookForUserB.Version++;

                try
                {
                    contextB.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    Console.WriteLine("SUCCESSFUL CONCURRENCY EXCEPTION CAUGHT!");
                    Console.WriteLine("User B was prevented from overwriting User A's changes.");
                }
            }

            Console.WriteLine("\n--- DEMONSTRATION COMPLETE ---");
        }
    }
}