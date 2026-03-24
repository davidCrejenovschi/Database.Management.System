using Npgsql;

namespace BibliotecaApp
{
    /// <summary>
    /// Manages the database connection lifecycle and configuration.
    /// Implements the Singleton pattern to provide a global point of access.
    /// </summary>
    public sealed class DatabaseManager
    {
        // Thread-safe singleton instance using Lazy initialization
        private static readonly Lazy<DatabaseManager> _instance =
            new Lazy<DatabaseManager>(() => new DatabaseManager());

        // Global access point for the manager instance
        public static DatabaseManager Instance => _instance.Value;

        // Centralized connection string for the PostgreSQL server
        private readonly string _connectionString =
            "Host=localhost;Username=postgres;Password=Davidcrj008;Database=library";

        // Private constructor to prevent external instantiation
        private DatabaseManager() { }

        /// <summary>
        /// Creates and returns a new NpgsqlConnection object.
        /// The connection is not opened here to allow the Repository to manage state.
        /// Ensure to wrap the usage in a 'using' block for proper resource disposal.
        /// </summary>
        /// <returns>A new instance of NpgsqlConnection.</returns>
        public NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }
    }
}