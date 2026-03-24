
namespace BibliotecaApp.DataBase
{
    /// <summary>
    /// Represents the Parent entity in the 1:N relationship.
    /// </summary>
    public class Author
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Nationality { get; set; }
    }

    /// <summary>
    /// Represents categories for the M:N relationship with Books. 
    /// </summary>
    public class Category
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }

    /// <summary>
    /// Represents the Child entity in the 1:N relationship with Authors.
    /// </summary>
    public class Book
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public int PublicationYear { get; set; }

        // Foreign key referencing the parent Author. 
        public int AuthorId { get; set; }

        // Collection for the M:N relationship with Categories. 
        public List<Category> Categories { get; set; } = new List<Category>();

        /// <summary>
        /// Formats the list of categories into a single string for UI display.
        /// </summary>
        public string CategoriesDisplay
        {
            get
            {
                var names = new List<string>();

                foreach (var cat in Categories)
                {
                    names.Add(cat.Name);
                }

                return string.Join(", ", names);
            }
        }
    }
}