using Npgsql;

namespace BibliotecaApp.DataBase
{
    public class LibraryRepository
    {
  
        public List<Author> GetAllAuthors()
        {
            var authors = new List<Author>();

            // Create a new connection and ensure it is automatically closed and disposed
            using (var conn = DatabaseManager.Instance.GetConnection())
            {
                conn.Open();

                // Define the manual SQL query to fetch author details
                string sql = "SELECT Id, Name, Nationality FROM Authors";

                // Use a command and a data reader within using blocks for proper resource management
                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    // Read the data row by row
                    while (reader.Read())
                    {
                        // Map the current database row to a new Author object
                        authors.Add(new Author
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            // Check for database NULL values before attempting to read the string
                            Nationality = reader.IsDBNull(2) ? null : reader.GetString(2)
                        });
                    }
                }
            }
            return authors;
        }

        public List<Category> GetAllCategories()
        {
            var categories = new List<Category>();

            // Create and open a database connection, ensuring it is safely disposed after use
            using (var conn = DatabaseManager.Instance.GetConnection())
            {
                conn.Open();

                // Define the raw SQL query to fetch all category records
                string sql = "SELECT Id, Name FROM Categories";

                // Execute the command and read the results within using blocks to prevent resource leaks
                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    // Iterate through each database row returned by the query
                    while (reader.Read())
                    {
                        // Map the current row values to a new Category object and append it to the list
                        categories.Add(new Category
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }
            return categories;
        }

        public List<Book> GetBooksByAuthor(int authorId)
        {
            // Dictionary to group multiple result rows (categories) into unique Book objects
            var booksDict = new Dictionary<int, Book>();

            using (var conn = DatabaseManager.Instance.GetConnection())
            {
                conn.Open();

                // SQL query with LEFT JOIN to fetch books alongside their M:N category relations
                string sql = @"
                SELECT b.Id, b.Title, b.PublicationYear, b.AuthorId, c.Id as CatId, c.Name as CatName
                FROM Books b
                LEFT JOIN Books_Categories bc ON b.Id = bc.BookId
                LEFT JOIN Categories c ON bc.CategoryId = c.Id
                WHERE b.AuthorId = @authorId";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    // Parameterized query to safely bind the author ID
                    cmd.Parameters.AddWithValue("authorId", authorId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int bookId = reader.GetInt32(0);

                            // If the book is not yet tracked, instantiate it and add it to the dictionary
                            if (!booksDict.TryGetValue(bookId, out Book book))
                            {
                                book = new Book
                                {
                                    Id = bookId,
                                    Title = reader.GetString(1),
                                    PublicationYear = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                    AuthorId = reader.GetInt32(3)
                                };
                                booksDict.Add(bookId, book);
                            }

                            // Append the category to the book's list if a valid category record exists
                            if (!reader.IsDBNull(4))
                            {
                                book.Categories.Add(new Category
                                {
                                    Id = reader.GetInt32(4),
                                    Name = reader.GetString(5)
                                });
                            }
                        }
                    }
                }
            }

            // Return the collection of fully populated unique books
            return new List<Book>(booksDict.Values);
        }

        public void AddBookWithCategories(Book book, List<int> categoryIds)
        {
            using (var conn = DatabaseManager.Instance.GetConnection())
            {
                conn.Open();

                // Ensure atomicity for the multiple inserts (Book and its Categories)
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        int newBookId;

                        // Insert the child record and immediately retrieve its auto-generated ID
                        string insertBookSql = "INSERT INTO Books (Title, PublicationYear, AuthorId) VALUES (@title, @year, @authorId) RETURNING Id";

                        using (var insertBookCmd = new NpgsqlCommand(insertBookSql, conn))
                        {
                            // Use parameters to prevent SQL injection vulnerabilities
                            insertBookCmd.Parameters.AddWithValue("title", book.Title);
                            insertBookCmd.Parameters.AddWithValue("year", book.PublicationYear);
                            insertBookCmd.Parameters.AddWithValue("authorId", book.AuthorId);
                            newBookId = Convert.ToInt32(insertBookCmd.ExecuteScalar());
                        }

                        // Check if there are any categories to link in the M:N table
                        if (categoryIds != null && categoryIds.Count > 0)
                        {
                            string insertRelSql = "INSERT INTO Books_Categories (BookId, CategoryId) VALUES (@bookId, @categoryId)";
                            using (var insertRelCmd = new NpgsqlCommand(insertRelSql, conn))
                            {
                                // Pre-define parameters for efficiency during the loop
                                insertRelCmd.Parameters.Add("bookId", NpgsqlTypes.NpgsqlDbType.Integer);
                                insertRelCmd.Parameters.Add("categoryId", NpgsqlTypes.NpgsqlDbType.Integer);

                                // Insert a relationship record for each selected category
                                foreach (int catId in categoryIds)
                                {
                                    insertRelCmd.Parameters["bookId"].Value = newBookId;
                                    insertRelCmd.Parameters["categoryId"].Value = catId;
                                    insertRelCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // Commit all changes to the database if no errors occurred
                        transaction.Commit();
                    }
                    catch
                    {
                        // Revert all changes if any part of the process fails
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public void UpdateBookWithCategories(Book book, List<int> newCategoryIds)
        {
            using (var conn = DatabaseManager.Instance.GetConnection())
            {
                conn.Open();

                // Ensure atomicity across the update and the relationship reconstruction
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Update the core attributes of the existing child record
                        string updateBookSql = "UPDATE Books SET Title = @title, PublicationYear = @year WHERE Id = @id";
                        using (var updateCmd = new NpgsqlCommand(updateBookSql, conn))
                        {
                            updateCmd.Parameters.AddWithValue("id", book.Id);
                            updateCmd.Parameters.AddWithValue("title", book.Title);
                            updateCmd.Parameters.AddWithValue("year", book.PublicationYear);
                            updateCmd.ExecuteNonQuery();
                        }

                        // Clear existing M:N relationships to prepare for the updated state
                        string deleteRelSql = "DELETE FROM Books_Categories WHERE BookId = @bookId";
                        using (var deleteRelCmd = new NpgsqlCommand(deleteRelSql, conn))
                        {
                            deleteRelCmd.Parameters.AddWithValue("bookId", book.Id);
                            deleteRelCmd.ExecuteNonQuery();
                        }

                        // Insert the new set of category relationships if any are provided
                        if (newCategoryIds != null && newCategoryIds.Count > 0)
                        {
                            string insertRelSql = "INSERT INTO Books_Categories (BookId, CategoryId) VALUES (@bookId, @categoryId)";
                            using (var insertRelCmd = new NpgsqlCommand(insertRelSql, conn))
                            {
                                insertRelCmd.Parameters.Add("bookId", NpgsqlTypes.NpgsqlDbType.Integer);
                                insertRelCmd.Parameters.Add("categoryId", NpgsqlTypes.NpgsqlDbType.Integer);

                                foreach (int catId in newCategoryIds)
                                {
                                    insertRelCmd.Parameters["bookId"].Value = book.Id;
                                    insertRelCmd.Parameters["categoryId"].Value = catId;
                                    insertRelCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // Commit all changes if the entire update process succeeds
                        transaction.Commit();
                    }
                    catch
                    {
                        // Rollback to the previous state to prevent orphaned records or partial updates
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public void DeleteBook(int bookId)
        {
            // Open a managed database connection ensuring automatic disposal after the operation
            using (var conn = DatabaseManager.Instance.GetConnection())
            {
                conn.Open();

                // Define the SQL command to remove the child record based on its primary key
                string sql = "DELETE FROM Books WHERE Id = @id";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    // Bind the ID parameter safely to prevent SQL injection vulnerabilities
                    cmd.Parameters.AddWithValue("id", bookId);

                    // Execute the deletion query against the database
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}