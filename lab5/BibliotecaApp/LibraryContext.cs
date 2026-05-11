using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BibliotecaApp
{
    public class LibraryContext : DbContext
    {
        public DbSet<Author> Authors { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Category> Categories { get; set; }

        public DbSet<Publisher> Publishers { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                optionsBuilder
                    .UseNpgsql(config.GetConnectionString("LibraryDbWithPooling"))
                    .UseLazyLoadingProxies()
                    .LogTo(Console.WriteLine, LogLevel.Information);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Book>()
                .HasMany(b => b.Categories)
                .WithMany(c => c.Books)
                .UsingEntity<Dictionary<string, object>>(
                    "Books_Categories",
                    j => j.HasOne<Category>().WithMany().HasForeignKey("CategoryId"),
                    j => j.HasOne<Book>().WithMany().HasForeignKey("BookId")
                );

            modelBuilder.Entity<Book>().HasIndex(b => b.PublicationYear);
            modelBuilder.Entity<Author>().HasIndex(a => a.Name);

            modelBuilder.Entity<Book>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Author>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Category>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Publisher>().HasQueryFilter(e => !e.IsDeleted);

        }

        public override int SaveChanges()
        {
            foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
            {
                if (entry.State == EntityState.Deleted)
                {
                    // Intercept the deletion and change it to an update
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = DateTime.UtcNow;
                    entry.Entity.DeletedBy = "Admin_User"; // Hardcoded for demo purposes
                }
            }

            return base.SaveChanges();
        }
    }
}