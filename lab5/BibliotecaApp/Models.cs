using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public abstract class AuditableEntity
{
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; } 
}

namespace BibliotecaApp
{
    /// <summary>
    /// Represents the Parent entity in the 1:N relationship.
    /// </summary>
    public class Author : AuditableEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public required string Name { get; set; }

        [MaxLength(50)]
        public string Nationality { get; set; }

        public virtual ICollection<Book> Books { get; set; } = new List<Book>();
    }

    /// <summary>
    /// Represents categories for the M:N relationship with Books. 
    /// </summary>
    public class Category : AuditableEntity
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
    public class Book : AuditableEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public required string Title { get; set; }

        public int PublicationYear { get; set; }

        // Adăugat la Pasul 2
        public string ISBN { get; set; }
        public int? PublisherId { get; set; }
        [ForeignKey("PublisherId")]
        public virtual Publisher Publisher { get; set; }

        public int AuthorId { get; set; }
        [ForeignKey("AuthorId")]
        public virtual Author Author { get; set; }

        public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

        // PASUL 3: Concurrency Token
        [ConcurrencyCheck]
        public int Version { get; set; } // Adăugăm coloana de versiune [cite: 61]

        [NotMapped]
        public string CategoriesDisplay
        {
            get { return string.Join(", ", Categories.Select(c => c.Name)); }
        }
    }

    public class Publisher : AuditableEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        public virtual ICollection<Book> Books { get; set; } = new List<Book>();
    }
}