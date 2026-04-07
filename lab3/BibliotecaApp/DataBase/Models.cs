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

        // Navigation property for 1:N relationship (Un autor are mai multe cărți)
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

        // Navigation property for M:N relationship (O categorie are mai multe cărți)
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

        // Foreign key referencing the parent Author. 
        public int AuthorId { get; set; }

        // Navigation property for Parent (1:N)
        [ForeignKey("AuthorId")]
        public virtual Author Author { get; set; }

        // Collection for the M:N relationship with Categories. 
        public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

        /// <summary>
        /// Formats the list of categories into a single string for UI display.
        /// </summary>
        [NotMapped] // Îi spunem lui EF Core să nu caute această coloană în baza de date
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