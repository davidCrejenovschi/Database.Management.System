using System.Text;
using System.Diagnostics;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace BibliotecaApp.Test
{
    public class TransactionDemos
    {
        private readonly string _connectionString;
        private readonly string _noPoolConnStr;
        private readonly int _insertCount = 5000;
        private readonly int _testAuthorId = 3; // Ensure this author ID exists in your DB

        public TransactionDemos()
        {
            // Read connection strings from appsettings.json to satisfy external configuration requirement
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            _connectionString = config.GetConnectionString("LibraryDbWithPooling") ?? "";
            _noPoolConnStr = config.GetConnectionString("LibraryDbNoPooling") ?? "";
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

        /// <summary>
        /// Task A: Measures the time difference between creating 100 connections 
        /// from scratch (No Pooling) vs reusing them from the pool.
        /// </summary>
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

        /// <summary>
        /// Task B: Demonstrates what happens when connections are not properly disposed, 
        /// causing the pool to exhaust its maximum capacity (Max Pool Size=10).
        /// </summary>
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
                    await conn.OpenAsync(); // We open but NEVER put it in a 'using' block or call Close()
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
    }
}