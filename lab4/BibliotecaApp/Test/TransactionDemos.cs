using BibliotecaApp.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Diagnostics;
using System.Text;

namespace BibliotecaApp.Test
{
    public class TransactionDemos
    {
        private readonly string _connectionString;
        private readonly string _noPoolConnStr;
        private readonly int _insertCount = 5000;
        private readonly int _testAuthorId = 3;
        private readonly LibraryRepository _repository;

        public TransactionDemos()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            _connectionString = config.GetConnectionString("LibraryDbWithPooling") ?? "";
            _noPoolConnStr = config.GetConnectionString("LibraryDbNoPooling") ?? "";

            _repository = new LibraryRepository();
        }

        public async Task<string> DemoDirtyReadAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- A. DEMO DIRTY READ ---");

            var taskA = Task.Run(async () =>
            {
                using var connA = new NpgsqlConnection(_connectionString);
                await connA.OpenAsync();
                using var txA = await connA.BeginTransactionAsync();
                log.AppendLine("Transaction A: BEGIN TRANSACTION");

                using var cmdA = new NpgsqlCommand("UPDATE Books SET PublicationYear = 9999 WHERE Id = 12", connA, txA);
                await cmdA.ExecuteNonQueryAsync();
                log.AppendLine("Transaction A: Year updated to 9999 (uncommitted)");

                await Task.Delay(3000);
                await txA.RollbackAsync();
                log.AppendLine("Transaction A: ROLLBACK executed!");
            });

            var taskB = Task.Run(async () =>
            {
                await Task.Delay(1000);
                using var connB = new NpgsqlConnection(_connectionString);
                await connB.OpenAsync();
                using var txB = await connB.BeginTransactionAsync(System.Data.IsolationLevel.ReadUncommitted);
                log.AppendLine("Transaction B: BEGIN TRANSACTION (READ UNCOMMITTED)");

                using var cmdB = new NpgsqlCommand("SELECT PublicationYear FROM Books WHERE Id = 12", connB, txB);
                var year = await cmdB.ExecuteScalarAsync();

                log.AppendLine($"Transaction B: Read value is {year} (Postgres prevents reading 9999)");
                await txB.CommitAsync();
            });

            await Task.WhenAll(taskA, taskB);
            log.AppendLine("--------------------------\n");
            return log.ToString();
        }

        public async Task<string> DemoNonRepeatableReadAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- B. DEMO NON-REPEATABLE READ ---");

            var taskA = Task.Run(async () =>
            {
                using var connA = new NpgsqlConnection(_connectionString);
                await connA.OpenAsync();
                using var txA = await connA.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                log.AppendLine("Transaction A: BEGIN TRANSACTION (READ COMMITTED)");

                using var cmdA = new NpgsqlCommand("SELECT PublicationYear FROM Books WHERE Id = 12", connA, txA);
                var year1 = await cmdA.ExecuteScalarAsync();
                log.AppendLine($"Transaction A: First read: {year1}");

                await Task.Delay(3000);

                var year2 = await cmdA.ExecuteScalarAsync();
                log.AppendLine($"Transaction A: Second read: {year2}");

                await txA.CommitAsync();
            });

            var taskB = Task.Run(async () =>
            {
                await Task.Delay(1000);
                using var connB = new NpgsqlConnection(_connectionString);
                await connB.OpenAsync();
                using var txB = await connB.BeginTransactionAsync();
                using var cmdB = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2025 WHERE Id = 12", connB, txB);
                await cmdB.ExecuteNonQueryAsync();

                await txB.CommitAsync();
                log.AppendLine("Transaction B: Updated and committed to 2025");
            });

            await Task.WhenAll(taskA, taskB);
            log.AppendLine("-----------------------------------\n");
            return log.ToString();
        }

        public async Task<string> DemoPhantomReadAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- C. DEMO PHANTOM READ ---");

            var taskA = Task.Run(async () =>
            {
                using var connA = new NpgsqlConnection(_connectionString);
                await connA.OpenAsync();
                using var txA = await connA.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                log.AppendLine("Transaction A: BEGIN TRANSACTION (READ COMMITTED)");

                using var cmdA = new NpgsqlCommand("SELECT COUNT(*) FROM Books WHERE AuthorId = 3", connA, txA);
                var count1 = await cmdA.ExecuteScalarAsync();
                log.AppendLine($"Transaction A: First count: {count1}");

                await Task.Delay(3000);

                var count2 = await cmdA.ExecuteScalarAsync();
                log.AppendLine($"Transaction A: Second count: {count2}");

                await txA.CommitAsync();
            });

            var taskB = Task.Run(async () =>
            {
                await Task.Delay(1000);
                using var connB = new NpgsqlConnection(_connectionString);
                await connB.OpenAsync();
                using var txB = await connB.BeginTransactionAsync();
                using var cmdB = new NpgsqlCommand("INSERT INTO Books (Title, PublicationYear, AuthorId) VALUES ('Phantom Book', 2024, 3)", connB, txB);
                await cmdB.ExecuteNonQueryAsync();

                await txB.CommitAsync();
                log.AppendLine("Transaction B: New book inserted for AuthorId=3");
            });

            await Task.WhenAll(taskA, taskB);

            using (var connClean = new NpgsqlConnection(_connectionString))
            {
                await connClean.OpenAsync();
                using var cmdClean = new NpgsqlCommand("DELETE FROM Books WHERE Title = 'Phantom Book'", connClean);
                await cmdClean.ExecuteNonQueryAsync();
            }

            log.AppendLine("----------------------------\n");
            return log.ToString();
        }

        public async Task<string> DemoLostUpdateAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- D. DEMO LOST UPDATE ---");

            var taskA = Task.Run(async () =>
            {
                using var connA = new NpgsqlConnection(_connectionString);
                await connA.OpenAsync();
                using var txA = await connA.BeginTransactionAsync();
                log.AppendLine("Transaction A: BEGIN TRANSACTION");

                using var cmdRead = new NpgsqlCommand("SELECT PublicationYear FROM Books WHERE Id = 12", connA, txA);
                int year = Convert.ToInt32(await cmdRead.ExecuteScalarAsync());
                log.AppendLine($"Transaction A: Reads year {year}");

                int newYear = year + 10;
                log.AppendLine($"Transaction A: Calculates new year -> {newYear}");

                await Task.Delay(3000);

                using var cmdUpdate = new NpgsqlCommand($"UPDATE Books SET PublicationYear = {newYear} WHERE Id = 12", connA, txA);
                await cmdUpdate.ExecuteNonQueryAsync();

                await txA.CommitAsync();
                log.AppendLine("Transaction A: COMMIT executed");
            });

            var taskB = Task.Run(async () =>
            {
                await Task.Delay(1000);
                using var connB = new NpgsqlConnection(_connectionString);
                await connB.OpenAsync();
                using var txB = await connB.BeginTransactionAsync();
                log.AppendLine("Transaction B: BEGIN TRANSACTION");

                using var cmdRead = new NpgsqlCommand("SELECT PublicationYear FROM Books WHERE Id = 12", connB, txB);
                int year = Convert.ToInt32(await cmdRead.ExecuteScalarAsync());
                log.AppendLine($"Transaction B: Reads year {year}");

                int newYear = year + 5;
                log.AppendLine($"Transaction B: Calculates new year -> {newYear}");

                using var cmdUpdate = new NpgsqlCommand($"UPDATE Books SET PublicationYear = {newYear} WHERE Id = 12", connB, txB);
                await cmdUpdate.ExecuteNonQueryAsync();

                await txB.CommitAsync();
                log.AppendLine("Transaction B: COMMIT executed");
            });

            await Task.WhenAll(taskA, taskB);

            using (var connFinal = new NpgsqlConnection(_connectionString))
            {
                await connFinal.OpenAsync();
                using var cmdFinal = new NpgsqlCommand("SELECT PublicationYear FROM Books WHERE Id = 12", connFinal);
                var finalYear = await cmdFinal.ExecuteScalarAsync();
                log.AppendLine($"\nFinal value in database: {finalYear} (Transaction B's update was lost!)");
            }

            log.AppendLine("---------------------------\n");
            return log.ToString();
        }

        public async Task<string> DemoDeadlockErrorAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- E1. DEMO DEADLOCK (ERROR) ---");

            var taskA = Task.Run(async () =>
            {
                using var connA = new NpgsqlConnection(_connectionString);
                await connA.OpenAsync();
                using var txA = await connA.BeginTransactionAsync();

                try
                {
                    log.AppendLine("Transaction A: BEGIN TRANSACTION");
                    using var cmd1 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2000 WHERE Id = 12", connA, txA);
                    await cmd1.ExecuteNonQueryAsync();
                    log.AppendLine("Transaction A: Locked Book Id=12. Waiting 2 seconds...");

                    await Task.Delay(2000);

                    log.AppendLine("Transaction A: Attempting to lock Book Id=13...");
                    using var cmd2 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2000 WHERE Id = 13", connA, txA);
                    await cmd2.ExecuteNonQueryAsync();

                    await txA.CommitAsync();
                    log.AppendLine("Transaction A: COMMIT successful.");
                }
                catch (PostgresException ex) when (ex.SqlState == "40P01")
                {
                    await txA.RollbackAsync();
                    log.AppendLine($"Transaction A FAILED with Deadlock: {ex.MessageText}");
                }
            });

            var taskB = Task.Run(async () =>
            {
                using var connB = new NpgsqlConnection(_connectionString);
                await connB.OpenAsync();
                using var txB = await connB.BeginTransactionAsync();

                try
                {
                    log.AppendLine("Transaction B: BEGIN TRANSACTION");
                    using var cmd1 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2001 WHERE Id = 13", connB, txB);
                    await cmd1.ExecuteNonQueryAsync();
                    log.AppendLine("Transaction B: Locked Book Id=13. Waiting 2 seconds...");

                    await Task.Delay(2000);

                    log.AppendLine("Transaction B: Attempting to lock Book Id=12...");
                    using var cmd2 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2001 WHERE Id = 12", connB, txB);
                    await cmd2.ExecuteNonQueryAsync();

                    await txB.CommitAsync();
                    log.AppendLine("Transaction B: COMMIT successful.");
                }
                catch (PostgresException ex) when (ex.SqlState == "40P01")
                {
                    await txB.RollbackAsync();
                    log.AppendLine($"Transaction B FAILED with Deadlock: {ex.MessageText}");
                }
            });

            await Task.WhenAll(taskA, taskB);
            log.AppendLine("---------------------------------\n");
            return log.ToString();
        }

        public async Task<string> DemoDeadlockResolvedAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- E2. DEMO DEADLOCK (RESOLVED) ---");

            var taskA = Task.Run(async () =>
            {
                using var connA = new NpgsqlConnection(_connectionString);
                await connA.OpenAsync();
                using var txA = await connA.BeginTransactionAsync();

                log.AppendLine("Transaction A: BEGIN TRANSACTION");
                using var cmd1 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2002 WHERE Id = 12", connA, txA);
                await cmd1.ExecuteNonQueryAsync();
                log.AppendLine("Transaction A: Locked Book Id=12. Waiting 2 seconds...");

                await Task.Delay(2000);

                log.AppendLine("Transaction A: Attempting to lock Book Id=13...");
                using var cmd2 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2002 WHERE Id = 13", connA, txA);
                await cmd2.ExecuteNonQueryAsync();

                await txA.CommitAsync();
                log.AppendLine("Transaction A: COMMIT successful.");
            });

            var taskB = Task.Run(async () =>
            {
                await Task.Delay(500);

                using var connB = new NpgsqlConnection(_connectionString);
                await connB.OpenAsync();
                using var txB = await connB.BeginTransactionAsync();

                log.AppendLine("Transaction B: BEGIN TRANSACTION");
                log.AppendLine("Transaction B: Attempting to lock Book Id=12 (will wait for TxA)...");
                using var cmd1 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2003 WHERE Id = 12", connB, txB);
                await cmd1.ExecuteNonQueryAsync();
                log.AppendLine("Transaction B: Locked Book Id=12.");

                log.AppendLine("Transaction B: Attempting to lock Book Id=13...");
                using var cmd2 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2003 WHERE Id = 13", connB, txB);
                await cmd2.ExecuteNonQueryAsync();

                await txB.CommitAsync();
                log.AppendLine("Transaction B: COMMIT successful.");
            });

            await Task.WhenAll(taskA, taskB);
            log.AppendLine("------------------------------------\n");
            return log.ToString();
        }

        public async Task CleanupPerformanceTestDataAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("DELETE FROM Books WHERE Title LIKE 'PerfTest_%'", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<long> RunAutoCommitInsertAsync()
        {
            await CleanupPerformanceTestDataAsync();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var watch = Stopwatch.StartNew();

            for (int i = 0; i < _insertCount; i++)
            {
                using var cmd = new NpgsqlCommand("INSERT INTO Books (Title, PublicationYear, AuthorId) VALUES (@title, 2026, @authorId)", conn);
                cmd.Parameters.AddWithValue("title", $"PerfTest_Auto_{i}");
                cmd.Parameters.AddWithValue("authorId", _testAuthorId);
                await cmd.ExecuteNonQueryAsync();
            }

            watch.Stop();
            await CleanupPerformanceTestDataAsync();
            return watch.ElapsedMilliseconds;
        }

        public async Task<long> RunBatchCommitInsertAsync()
        {
            await CleanupPerformanceTestDataAsync();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var watch = Stopwatch.StartNew();

            NpgsqlTransaction tx = await conn.BeginTransactionAsync();
            for (int i = 0; i < _insertCount; i++)
            {
                using var cmd = new NpgsqlCommand("INSERT INTO Books (Title, PublicationYear, AuthorId) VALUES (@title, 2026, @authorId)", conn, tx);
                cmd.Parameters.AddWithValue("title", $"PerfTest_Batch100_{i}");
                cmd.Parameters.AddWithValue("authorId", _testAuthorId);
                await cmd.ExecuteNonQueryAsync();

                if ((i + 1) % 100 == 0)
                {
                    await tx.CommitAsync();
                    await tx.DisposeAsync();

                    if (i < _insertCount - 1)
                    {
                        tx = await conn.BeginTransactionAsync();
                    }
                }
            }

            watch.Stop();
            await CleanupPerformanceTestDataAsync();
            return watch.ElapsedMilliseconds;
        }

        public async Task<long> RunSingleTransactionBatchAsync()
        {
            await CleanupPerformanceTestDataAsync();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var watch = Stopwatch.StartNew();

            using var tx = await conn.BeginTransactionAsync();
            var batch = new NpgsqlBatch(conn, tx);

            for (int i = 0; i < _insertCount; i++)
            {
                var batchCmd = new NpgsqlBatchCommand("INSERT INTO Books (Title, PublicationYear, AuthorId) VALUES ($1, 2026, $2)");
                batchCmd.Parameters.Add(new NpgsqlParameter { Value = $"PerfTest_FullBatch_{i}" });
                batchCmd.Parameters.Add(new NpgsqlParameter { Value = _testAuthorId });
                batch.BatchCommands.Add(batchCmd);

                if ((i + 1) % 50 == 0)
                {
                    await batch.ExecuteNonQueryAsync();
                    batch.BatchCommands.Clear();
                }
            }

            if (batch.BatchCommands.Count > 0)
            {
                await batch.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            watch.Stop();
            await CleanupPerformanceTestDataAsync();
            return watch.ElapsedMilliseconds;
        }

        public async Task<string> RunFullBenchmarkAsync()
        {
            var log = new StringBuilder();
            log.AppendLine($"--- PERFORMANCE BENCHMARK ({_insertCount} INSERTS) ---");

            long autoAvg = ((await RunAutoCommitInsertAsync()) + (await RunAutoCommitInsertAsync()) + (await RunAutoCommitInsertAsync())) / 3;
            log.AppendLine($"1. Auto-Commit Average: {autoAvg}ms");

            long batchAvg = ((await RunBatchCommitInsertAsync()) + (await RunBatchCommitInsertAsync()) + (await RunBatchCommitInsertAsync())) / 3;
            log.AppendLine($"2. Batch(100) Average: {batchAvg}ms");

            long singleAvg = ((await RunSingleTransactionBatchAsync()) + (await RunSingleTransactionBatchAsync()) + (await RunSingleTransactionBatchAsync())) / 3;
            log.AppendLine($"3. NpgsqlBatch Average: {singleAvg}ms");

            log.AppendLine("----------------------------------------\n");
            return log.ToString();
        }

        public async Task<string> RunConnectionPoolBenchmarkAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- TASK A: CONNECTION CREATION OVERHEAD ---");

            var sw = new Stopwatch();

            // 1. Without Pooling
            sw.Start();
            for (int i = 0; i < 100; i++)
            {
                using var conn = new NpgsqlConnection(_noPoolConnStr);
                await conn.OpenAsync();
            }
            sw.Stop();
            long noPoolTime = sw.ElapsedMilliseconds;
            log.AppendLine($"100 Connections NO Pooling: {noPoolTime}ms (Avg: {(double)noPoolTime / 100}ms/conn)");

            sw.Reset();

            // 2. With Pooling
            sw.Start();
            for (int i = 0; i < 100; i++)
            {
                using var conn = new NpgsqlConnection(_connectionString); // Uses _poolConnStr
                await conn.OpenAsync();
            }
            sw.Stop();
            long poolTime = sw.ElapsedMilliseconds;
            log.AppendLine($"100 Connections WITH Pooling: {poolTime}ms (Avg: {(double)poolTime / 100}ms/conn)");

            log.AppendLine("--------------------------------------------\n");
            return log.ToString();
        }

        public async Task<string> SimulateConnectionLeakAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- TASK B: CONNECTION LEAK SIMULATION ---");
            log.AppendLine("Opening connections without closing them. Max Pool Size is 10.");

            var leakedConnections = new List<NpgsqlConnection>();

            try
            {
                // Trying to open 15 connections when the pool only allows 10
                for (int i = 0; i < 15; i++)
                {
                    var conn = new NpgsqlConnection(_connectionString);
                    await conn.OpenAsync();
                    leakedConnections.Add(conn);
                    log.AppendLine($"Connection {i + 1} successfully opened and held.");
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"\n[CRITICAL ERROR] Pool Exhausted! Reached maximum limit.");
                log.AppendLine($"Exception Message: {ex.Message}");
            }
            finally
            {
                log.AppendLine("\nExecuting proper resource management (Cleanup)...");
                foreach (var conn in leakedConnections)
                {
                    await conn.DisposeAsync();
                }
                log.AppendLine("All leaked connections have been properly disposed and returned to the pool.");
            }

            log.AppendLine("------------------------------------------\n");
            return log.ToString();
        }

        public async Task<string> RunNPlusOneDemoAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- DEMO: N+1 QUERY PROBLEM (LAZY LOADING) ---");

            var sw = Stopwatch.StartNew();

            using (var context = new LibraryContext())
            {
                // 1 query to fetch all authors
                var authors = await context.Authors.ToListAsync();
                int booksCount = 0;

                foreach (var author in authors)
                {
                    // Accessing the Books property triggers a new SQL query for EACH author
                    booksCount += author.Books.Count;
                }

                sw.Stop();
                log.AppendLine($"Total execution time: {sw.ElapsedMilliseconds} ms");
                log.AppendLine($"Authors processed: {authors.Count} | Books counted: {booksCount}");
            }

            log.AppendLine("------------------------------------------------------\n");
            return log.ToString();
        }

        public async Task<string> RunEagerLoadingDemoAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- DEMO: EAGER LOADING OPTIMIZATION (INCLUDE) ---");

            var sw = Stopwatch.StartNew();

            using (var context = new LibraryContext())
            {
                // 1 single complex query that fetches both authors and books using JOIN
                var authors = await context.Authors.Include(a => a.Books).ToListAsync();
                int booksCount = 0;

                foreach (var author in authors)
                {
                    // Data is already in memory, no more database calls here
                    booksCount += author.Books.Count;
                }

                sw.Stop();
                log.AppendLine($"Total execution time: {sw.ElapsedMilliseconds} ms");
                log.AppendLine($"Authors processed: {authors.Count} | Books counted: {booksCount}");
            }

            log.AppendLine("------------------------------------------------------\n");
            return log.ToString();
        }

        public async Task<bool> CheckTestDataExistsAsync()
        {
            using var context = new LibraryContext();
            // Verificăm dacă există deja cărțile de test pentru autorul 99
            return await context.Books.AnyAsync(b => b.AuthorId == 99);
        }

        public async Task<string> SetupIndexingDataAsync()
        {
            var log = new StringBuilder();
            using var context = new LibraryContext();

            // Asigurăm existența autorului de test
            var testAuthor = await context.Authors.FirstOrDefaultAsync(a => a.Id == 99);
            if (testAuthor == null)
            {
                context.Authors.Add(new Author { Id = 99, Name = "Test Author", Nationality = "Testland" });
                await context.SaveChangesAsync();
            }

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var watch = Stopwatch.StartNew();
            using var tx = await conn.BeginTransactionAsync();
            var batch = new NpgsqlBatch(conn, tx);

            // Inserăm 10.000 de înregistrări
            for (int i = 1; i <= 10000; i++)
            {
                var cmd = new NpgsqlBatchCommand("INSERT INTO \"Books\" (\"Title\", \"PublicationYear\", \"AuthorId\") VALUES ($1, $2, $3)");
                cmd.Parameters.Add(new NpgsqlParameter { Value = $"PerfBook_{i}" });
                cmd.Parameters.Add(new NpgsqlParameter { Value = 1900 + (i % 126) });
                cmd.Parameters.Add(new NpgsqlParameter { Value = 99 });
                batch.BatchCommands.Add(cmd);

                if (i % 1000 == 0)
                {
                    await batch.ExecuteNonQueryAsync();
                    batch.BatchCommands.Clear();
                }
            }

            await tx.CommitAsync();
            watch.Stop();
            log.AppendLine($"Successfully inserted 10,000 records in {watch.ElapsedMilliseconds}ms.");
            return log.ToString();
        }

        public async Task<string> CleanupIndexingDataAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Ștergem tot ce ține de ID-ul 99 (datele de test)
            using var cmd = new NpgsqlCommand("DELETE FROM \"Books\" WHERE \"AuthorId\" = 99; DELETE FROM \"Authors\" WHERE \"Id\" = 99;", conn);
            int affected = await cmd.ExecuteNonQueryAsync();

            return $"Cleanup complete. Deleted {affected} test-related records.\n";
        }

        private async Task ExecuteNonQueryAsync(NpgsqlConnection conn, string sql)
        {
            using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<string> RunIndexingBenchmarkAsync(bool useIndexes)
        {
            var log = new StringBuilder();
            string mode = useIndexes ? "WITH INDEXES" : "WITHOUT INDEXES";
            log.AppendLine($"--- BENCHMARK: {mode} ---");

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            try
            {
                if (useIndexes)
                {
                    log.AppendLine("Creating strategic indexes...");
                    await ExecuteNonQueryAsync(conn, "CREATE INDEX IF NOT EXISTS idx_books_title ON \"Books\" (\"Title\");");
                    await ExecuteNonQueryAsync(conn, "CREATE INDEX IF NOT EXISTS idx_books_author ON \"Books\" (\"AuthorId\");");
                    await ExecuteNonQueryAsync(conn, "CREATE INDEX IF NOT EXISTS idx_books_year ON \"Books\" (\"PublicationYear\");");
                    await ExecuteNonQueryAsync(conn, "CREATE INDEX IF NOT EXISTS idx_books_composite ON \"Books\" (\"AuthorId\", \"PublicationYear\");");
                }
                else
                {
                    log.AppendLine("Dropping indexes (if exist) for clean baseline...");
                    await ExecuteNonQueryAsync(conn, "DROP INDEX IF EXISTS idx_books_title;");
                    await ExecuteNonQueryAsync(conn, "DROP INDEX IF EXISTS idx_books_author;");
                    await ExecuteNonQueryAsync(conn, "DROP INDEX IF EXISTS idx_books_year;");
                    await ExecuteNonQueryAsync(conn, "DROP INDEX IF EXISTS idx_books_composite;");
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"Error managing indexes: {ex.Message}");
            }

            string[] queries = {
                "SELECT * FROM \"Books\" WHERE \"Title\" = 'PerfBook_5000'",
                "SELECT * FROM \"Books\" WHERE \"AuthorId\" = 99",
                "SELECT * FROM \"Books\" WHERE \"PublicationYear\" BETWEEN 2000 AND 2010",
                "SELECT * FROM \"Books\" WHERE \"AuthorId\" = 99 AND \"PublicationYear\" > 2010"
            };

            foreach (var sql in queries)
            {
                log.AppendLine($"\nQuery: {sql}");

                using var cmdExplain = new NpgsqlCommand($"EXPLAIN ANALYZE {sql}", conn);
                using var reader = await cmdExplain.ExecuteReaderAsync();
                log.AppendLine("Execution Plan:");
                while (await reader.ReadAsync()) log.AppendLine($"> {reader[0]}");
                await reader.CloseAsync();

                var sw = new Stopwatch();
                sw.Start();
                for (int i = 0; i < 100; i++)
                {
                    using var cmd = new NpgsqlCommand(sql, conn);
                    await cmd.ExecuteNonQueryAsync();
                }
                sw.Stop();
                log.AppendLine($"Average Execution Time: {sw.Elapsed.TotalMilliseconds / 100:F4} ms");
            }

            return log.ToString();
        }

        public async Task<string> RunFullIndexingReportAsync()
        {
            var log = new StringBuilder();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string[] queryNames = { "Title Search (Email)", "Author Search (Dept)", "Year Range (Salary)", "Multi-column Search" };
            string[] queries = {
                "SELECT * FROM \"Books\" WHERE \"Title\" = 'PerfBook_5000'",
                "SELECT * FROM \"Books\" WHERE \"AuthorId\" = 99",
                "SELECT * FROM \"Books\" WHERE \"PublicationYear\" BETWEEN 2000 AND 2010",
                "SELECT * FROM \"Books\" WHERE \"AuthorId\" = 99 AND \"PublicationYear\" > 2010"
            };

            double[] timesWithoutIndex = new double[4];
            double[] timesWithIndex = new double[4];

            // 1. DROP INDEXES FOR BASELINE
            await ExecuteNonQueryAsync(conn, "DROP INDEX IF EXISTS idx_books_title;");
            await ExecuteNonQueryAsync(conn, "DROP INDEX IF EXISTS idx_books_author;");
            await ExecuteNonQueryAsync(conn, "DROP INDEX IF EXISTS idx_books_year;");
            await ExecuteNonQueryAsync(conn, "DROP INDEX IF EXISTS idx_books_composite;");

            // Measure Without Index
            for (int q = 0; q < queries.Length; q++)
            {
                var sw = new Stopwatch();
                sw.Start();
                for (int i = 0; i < 100; i++)
                {
                    using var cmd = new NpgsqlCommand(queries[q], conn);
                    await cmd.ExecuteNonQueryAsync();
                }
                sw.Stop();
                timesWithoutIndex[q] = sw.Elapsed.TotalMilliseconds / 100.0;
            }

            // 2. CREATE STRATEGIC INDEXES
            await ExecuteNonQueryAsync(conn, "CREATE INDEX IF NOT EXISTS idx_books_title ON \"Books\" (\"Title\");");
            await ExecuteNonQueryAsync(conn, "CREATE INDEX IF NOT EXISTS idx_books_author ON \"Books\" (\"AuthorId\");");
            await ExecuteNonQueryAsync(conn, "CREATE INDEX IF NOT EXISTS idx_books_year ON \"Books\" (\"PublicationYear\");");
            await ExecuteNonQueryAsync(conn, "CREATE INDEX IF NOT EXISTS idx_books_composite ON \"Books\" (\"AuthorId\", \"PublicationYear\");");

            // Measure With Index
            for (int q = 0; q < queries.Length; q++)
            {
                var sw = new Stopwatch();
                sw.Start();
                for (int i = 0; i < 100; i++)
                {
                    using var cmd = new NpgsqlCommand(queries[q], conn);
                    await cmd.ExecuteNonQueryAsync();
                }
                sw.Stop();
                timesWithIndex[q] = sw.Elapsed.TotalMilliseconds / 100.0;
            }

            // 3. GENERATE FORMATTED TABLE
            log.AppendLine(String.Format("{0,-25} | {1,-15} | {2,-15} | {3,-15}", "Query", "No Index (ms)", "With Index (ms)", "Improvement"));
            log.AppendLine(new string('-', 78));

            for (int i = 0; i < 4; i++)
            {
                double diff = timesWithoutIndex[i] - timesWithIndex[i];
                double percent = (diff / timesWithoutIndex[i]) * 100;

                // Prevent negative percentages if DB caches make the first run weird
                string improvement = percent > 0 ? $"~ {percent:F1}% faster" : "No improvement";

                log.AppendLine(String.Format("{0,-25} | {1,-15:F4} | {2,-15:F4} | {3,-15}",
                    queryNames[i], timesWithoutIndex[i], timesWithIndex[i], improvement));
            }

            log.AppendLine(new string('-', 78));

            return log.ToString();
        }

        public async Task<string> RunPaginationBenchmarkAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- BENCHMARK PAGINARE (10.000 Înregistrări) ---");
            log.AppendLine("Page Size: 100 rânduri/pagină");

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Verificăm dacă avem datele
            using var cmdCheck = new NpgsqlCommand("SELECT COUNT(*) FROM \"Books\" WHERE \"AuthorId\" = 99", conn);
            long v = (long)await cmdCheck.ExecuteScalarAsync();
            long count = v;
            if (count < 10000)
            {
                return "Eroare: Nu există 10.000 de înregistrări de test. Te rog rulează 'Initialize Test Data' din secțiunea Indexing mai întâi.";
            }

            // Pentru a simula Keyset la pagina 50 și 100, trebuie să știm care era ID-ul ultimului element de pe paginile 49 și 99.
            long cursorPage1 = 0;
            long cursorPage50 = await GetCursorIdAtOffset(conn, 4899); // Ultimul ID de pe pagina 49
            long cursorPage100 = await GetCursorIdAtOffset(conn, 9899); // Ultimul ID de pe pagina 99

            string[] pageNames = { "PRIMA PAGINĂ (Pag 1)", "PAGINA DIN MIJLOC (Pag 50)", "ULTIMA PAGINĂ (Pag 100)" };

            // Interogări OFFSET (Strategia A)
            string[] offsetQueries = {
        "SELECT * FROM \"Books\" WHERE \"AuthorId\" = 99 ORDER BY \"Id\" LIMIT 100 OFFSET 0",
        "SELECT * FROM \"Books\" WHERE \"AuthorId\" = 99 ORDER BY \"Id\" LIMIT 100 OFFSET 4900",
        "SELECT * FROM \"Books\" WHERE \"AuthorId\" = 99 ORDER BY \"Id\" LIMIT 100 OFFSET 9900"
    };

            // Interogări KEYSET/CURSOR (Strategia B)
            string[] keysetQueries = {
        $"SELECT * FROM \"Books\" WHERE \"AuthorId\" = 99 AND \"Id\" > {cursorPage1} ORDER BY \"Id\" LIMIT 100",
        $"SELECT * FROM \"Books\" WHERE \"AuthorId\" = 99 AND \"Id\" > {cursorPage50} ORDER BY \"Id\" LIMIT 100",
        $"SELECT * FROM \"Books\" WHERE \"AuthorId\" = 99 AND \"Id\" > {cursorPage100} ORDER BY \"Id\" LIMIT 100"
    };

            for (int i = 0; i < 3; i++)
            {
                log.AppendLine($"\n========================================");
                log.AppendLine($"=== {pageNames[i]} ===");
                log.AppendLine($"========================================");

                // --- TEST OFFSET ---
                log.AppendLine("\n[STRATEGIA A: OFFSET]");
                log.AppendLine($"Query: {offsetQueries[i]}");
                await AppendExplainAnalyzeAsync(conn, offsetQueries[i], log);
                double timeOffset = await MeasureAverageExecutionTimeAsync(conn, offsetQueries[i]);
                log.AppendLine($"Timp Mediu (100 rulări): {timeOffset:F4} ms");

                // --- TEST KEYSET ---
                log.AppendLine("\n[STRATEGIA B: KEYSET (CURSOR)]");
                log.AppendLine($"Query: {keysetQueries[i]}");
                await AppendExplainAnalyzeAsync(conn, keysetQueries[i], log);
                double timeKeyset = await MeasureAverageExecutionTimeAsync(conn, keysetQueries[i]);
                log.AppendLine($"Timp Mediu (100 rulări): {timeKeyset:F4} ms");

                // Diferența
                double diff = timeOffset - timeKeyset;
                if (timeOffset > 0)
                {
                    double percent = (diff / timeOffset) * 100;
                    log.AppendLine($"\n-> Keyset a fost mai rapid cu {percent:F1}%");
                }
            }

            log.AppendLine("\n------------------------------------------------------\n");
            return log.ToString();
        }

        private async Task<long> GetCursorIdAtOffset(NpgsqlConnection conn, int offset)
        {
            using var cmd = new NpgsqlCommand($"SELECT \"Id\" FROM \"Books\" WHERE \"AuthorId\" = 99 ORDER BY \"Id\" LIMIT 1 OFFSET {offset}", conn);
            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt64(result) : 0;
        }

        private async Task AppendExplainAnalyzeAsync(NpgsqlConnection conn, string sql, StringBuilder log)
        {
            using var cmd = new NpgsqlCommand($"EXPLAIN ANALYZE {sql}", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            log.AppendLine("Execution Plan:");
            while (await reader.ReadAsync())
            {
                log.AppendLine($"> {reader[0]}");
            }
            await reader.CloseAsync();
        }

        private async Task<double> MeasureAverageExecutionTimeAsync(NpgsqlConnection conn, string sql)
        {
            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < 100; i++)
            {
                using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds / 100.0;
        }

        public async Task<string> RunCacheBenchmarkAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- TASK 4: CACHING PERFORMANCE ANALYSIS ---");

            var sw = Stopwatch.StartNew();

            // Primul apel: Cache Miss (va merge la baza de date)
            var (author1, status1) = _repository.GetAuthorByIdCached(99);
            sw.Stop();
            log.AppendLine($"1st Call: {status1} | Time: {sw.Elapsed.TotalMilliseconds:F4} ms");

            // Al doilea apel: Cache Hit (va lua din memorie)
            sw.Restart();
            var (author2, status2) = _repository.GetAuthorByIdCached(99);
            sw.Stop();
            log.AppendLine($"2nd Call: {status2} | Time: {sw.Elapsed.TotalMilliseconds:F4} ms");

            log.AppendLine($"\n{_repository.GetCacheStats()}"); // Afișează statisticile cerute
            log.AppendLine("--------------------------------------------\n");

            return log.ToString();
        }

        public string InvalidateCacheDemo()
        {
            _repository.InvalidateAuthorCache(99);
            return "[CACHE] Entry for Author 99 was evicted (Cache Invalidation).\n";
        }

        public string GetCacheStatsDemo()
        {
            return $"[STATS] {_repository.GetCacheStats()}\n";
        }

        public async Task<string> RunBulkUpdateBenchmarkAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- TASK 5: BULK UPDATE OPTIMIZATION ---");
            log.AppendLine("Testing updates on 10,000 records (AuthorId = 99)...");

            try
            {
                // Setup: Reset all test books to year 2000 to ensure consistent tests
                using (var setupCtx = new LibraryContext())
                {
                    await setupCtx.Books.Where(b => b.AuthorId == 99).ExecuteUpdateAsync(s => s.SetProperty(b => b.PublicationYear, 2000));
                }

                // APPROACH 1: Individual Updates (Tracking 10,000 entities in memory)
                log.AppendLine("\n[Approach 1] Individual Updates (EF Core Tracking)");
                var sw1 = Stopwatch.StartNew();
                using (var ctx1 = new LibraryContext())
                {
                    // Loads all 10k entities into memory tracking
                    var books = await ctx1.Books.Where(b => b.AuthorId == 99).ToListAsync();
                    foreach (var b in books)
                    {
                        b.PublicationYear++;
                    }
                    await ctx1.SaveChangesAsync();
                }
                sw1.Stop();
                log.AppendLine($"Execution Time: {sw1.ElapsedMilliseconds} ms");

                // Reset data
                using (var setupCtx = new LibraryContext()) { await setupCtx.Books.Where(b => b.AuthorId == 99).ExecuteUpdateAsync(s => s.SetProperty(b => b.PublicationYear, 2000)); }

                // APPROACH 2: Bulk Update Query (EF Core ExecuteUpdate)
                log.AppendLine("\n[Approach 2] Bulk Update Query (ExecuteUpdate)");
                var sw2 = Stopwatch.StartNew();
                using (var ctx2 = new LibraryContext())
                {
                    // Executes ONE single SQL UPDATE command directly on the DB
                    await ctx2.Books
                        .Where(b => b.AuthorId == 99)
                        .ExecuteUpdateAsync(s => s.SetProperty(b => b.PublicationYear, b => b.PublicationYear + 1));
                }
                sw2.Stop();
                log.AppendLine($"Execution Time: {sw2.ElapsedMilliseconds} ms");

                // Reset data
                using (var setupCtx = new LibraryContext()) { await setupCtx.Books.Where(b => b.AuthorId == 99).ExecuteUpdateAsync(s => s.SetProperty(b => b.PublicationYear, 2000)); }

                // APPROACH 3: Batch Updates (Commit every 1000 records)
                log.AppendLine("\n[Approach 3] Batch Updates (Commit every 1000)");
                var sw3 = Stopwatch.StartNew();
                using (var ctx3 = new LibraryContext())
                {
                    // Fetch fast without tracking
                    var books = await ctx3.Books.Where(b => b.AuthorId == 99).AsNoTracking().ToListAsync();
                    int batchSize = 1000;

                    for (int i = 0; i < books.Count; i += batchSize)
                    {
                        using var batchCtx = new LibraryContext();
                        var batch = books.Skip(i).Take(batchSize).ToList();

                        foreach (var b in batch)
                        {
                            b.PublicationYear++;
                            batchCtx.Books.Update(b);
                        }
                        await batchCtx.SaveChangesAsync();
                    }
                }
                sw3.Stop();
                log.AppendLine($"Execution Time: {sw3.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                log.AppendLine($"ERROR: {ex.Message}");
            }

            log.AppendLine("\n--------------------------------------------\n");
            return log.ToString();
        }

        public async Task<string> RunPreparedStatementBenchmarkAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("--- TASK 6: PREPARED STATEMENTS CACHING ---");
            log.AppendLine("Executing 1000 queries...\n");

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // TEST A: Without Reuse (Creating new commands inside the loop)
            log.AppendLine("[Test A] Without Reuse (Unprepared)");
            var sw1 = Stopwatch.StartNew();

            for (int i = 1; i <= 1000; i++)
            {
                // The database must parse the SQL string and generate an execution plan EVERY time
                using var cmd = new NpgsqlCommand("SELECT \"Id\" FROM \"Books\" WHERE \"Id\" = @id", conn);
                cmd.Parameters.AddWithValue("id", i);
                await cmd.ExecuteScalarAsync();
            }

            sw1.Stop();
            log.AppendLine($"Execution Time: {sw1.ElapsedMilliseconds} ms\n");

            // TEST B: With Reuse (Prepared Statement outside the loop)
            log.AppendLine("[Test B] With Statement Reuse (Prepared)");
            var sw2 = Stopwatch.StartNew();

            // We create the command ONCE outside the loop
            using var cmdPrepared = new NpgsqlCommand("SELECT \"Id\" FROM \"Books\" WHERE \"Id\" = @id", conn);
            var param = cmdPrepared.Parameters.Add("id", NpgsqlTypes.NpgsqlDbType.Integer);

            // This is the crucial step: The database compiles and caches the execution plan NOW
            await cmdPrepared.PrepareAsync();

            for (int i = 1; i <= 1000; i++)
            {
                // We only swap the parameter value, skipping the SQL parsing phase
                param.Value = i;
                await cmdPrepared.ExecuteScalarAsync();
            }

            sw2.Stop();
            log.AppendLine($"Execution Time: {sw2.ElapsedMilliseconds} ms");

            // Calculate Improvement
            if (sw1.ElapsedMilliseconds > 0)
            {
                double diff = sw1.ElapsedMilliseconds - sw2.ElapsedMilliseconds;
                double percent = (diff / sw1.ElapsedMilliseconds) * 100;
                log.AppendLine($"\n-> Prepared Statement was ~{percent:F1}% faster.");
            }

            log.AppendLine("\n--------------------------------------------\n");
            return log.ToString();
        }
    }
}