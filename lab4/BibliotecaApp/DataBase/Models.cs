using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BibliotecaApp.DataBase
{
    /// <summary>
    /// Represents the Parent entity in the 1:N relationship.
    /// </summary>
    public class Author
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public required string Name { get; set; }

        public required string Nationality { get; set; }

        public virtual ICollection<Book> Books { get; set; } = new List<Book>();
    }

    /// <summary>
    /// Represents categories for the M:N relationship with Books. 
    /// </summary>
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public required string Name { get; set; }

        public virtual ICollection<Book> Books { get; set; } = new List<Book>();
    }

    /// <summary>
    /// Represents the Child entity in the 1:N relationship with Authors.
    /// </summary>
    public class Book
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public required string Title { get; set; }

        public int PublicationYear { get; set; }

        public int AuthorId { get; set; }
        [ForeignKey("AuthorId")]
        public virtual Author Author { get; set; }

        public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

        /// <summary>
        /// Formats the list of categories into a single string for UI display.
        /// </summary>
        [NotMapped]
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