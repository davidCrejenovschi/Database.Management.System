using System.Text;
using Npgsql;

namespace BibliotecaApp.Test
{
    public class TransactionDemos
    {
        private readonly string _connectionString = "Host=localhost;Username=postgres;Password=Davidcrj008;Database=library";
        private readonly int _insertCount = 5000;
        private readonly int _testAuthorId = 3;

        /// <summary>
        /// Verifies if a transaction can read uncommitted data from another concurrent transaction.
        /// Note: PostgreSQL treats READ UNCOMMITTED as READ COMMITTED, inherently preventing dirty reads.
        /// </summary>
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

        /// <summary>
        /// Verifies if a transaction reading the same row twice gets different values 
        /// because another transaction updated and committed it in between the reads.
        /// </summary>
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

        /// <summary>
        /// Verifies if a transaction executing the same aggregate query (e.g., COUNT) twice 
        /// gets different results because another transaction inserted a new matching row in between.
        /// </summary>
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

        /// <summary>
        /// Verifies the scenario where two concurrent transactions read the same value 
        /// and update it based on that initial read, causing one of the updates to be overwritten and lost.
        /// </summary>
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


        /// <summary>
        /// Forces a Deadlock scenario where two transactions lock rows in reverse order.
        /// PostgreSQL will detect the deadlock and terminate one of the transactions.
        /// </summary>
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

                    // Lock Row 12
                    using var cmd1 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2000 WHERE Id = 12", connA, txA);
                    await cmd1.ExecuteNonQueryAsync();
                    log.AppendLine("Transaction A: Locked Book Id=12. Waiting 2 seconds...");

                    await Task.Delay(2000); // Wait 2 seconds

                    // Try to lock Row 13
                    log.AppendLine("Transaction A: Attempting to lock Book Id=13...");
                    using var cmd2 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2000 WHERE Id = 13", connA, txA);
                    await cmd2.ExecuteNonQueryAsync();

                    await txA.CommitAsync();
                    log.AppendLine("Transaction A: COMMIT successful.");
                }
                catch (PostgresException ex) when (ex.SqlState == "40P01") // 40P01 is the PostgreSQL error code for deadlock
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

                    // Lock Row 13 (Reverse order)
                    using var cmd1 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2001 WHERE Id = 13", connB, txB);
                    await cmd1.ExecuteNonQueryAsync();
                    log.AppendLine("Transaction B: Locked Book Id=13. Waiting 2 seconds...");

                    await Task.Delay(2000); // Wait 2 seconds

                    // Try to lock Row 12
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

        /// <summary>
        /// Resolves the Deadlock scenario by ensuring both transactions lock the resources 
        /// in the exact same order (Id 12 first, then Id 13).
        /// </summary>
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

                // Lock Row 12
                using var cmd1 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2002 WHERE Id = 12", connA, txA);
                await cmd1.ExecuteNonQueryAsync();
                log.AppendLine("Transaction A: Locked Book Id=12. Waiting 2 seconds...");

                await Task.Delay(2000);

                // Lock Row 13
                log.AppendLine("Transaction A: Attempting to lock Book Id=13...");
                using var cmd2 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2002 WHERE Id = 13", connA, txA);
                await cmd2.ExecuteNonQueryAsync();

                await txA.CommitAsync();
                log.AppendLine("Transaction A: COMMIT successful.");
            });

            var taskB = Task.Run(async () =>
            {
                // Small delay to ensure TxA starts locking first
                await Task.Delay(500);

                using var connB = new NpgsqlConnection(_connectionString);
                await connB.OpenAsync();
                using var txB = await connB.BeginTransactionAsync();

                log.AppendLine("Transaction B: BEGIN TRANSACTION");

                // Lock Row 12 first (SAME ORDER AS TX A)
                log.AppendLine("Transaction B: Attempting to lock Book Id=12 (will wait for TxA)...");
                using var cmd1 = new NpgsqlCommand("UPDATE Books SET PublicationYear = 2003 WHERE Id = 12", connB, txB);
                await cmd1.ExecuteNonQueryAsync();
                log.AppendLine("Transaction B: Locked Book Id=12.");

                // Lock Row 13
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


        /// <summary>
        /// Cleans up any leftover test data from previous benchmark runs.
        /// </summary>
        public async Task CleanupPerformanceTestDataAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("DELETE FROM Books WHERE Title LIKE 'PerfTest_%'", conn);
            int deleted = await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Approach 1: Auto-commit (One transaction per insert). Slowest method.
        /// </summary>
        public async Task<long> RunAutoCommitInsertAsync()
        {
            await CleanupPerformanceTestDataAsync(); // Clean before starting

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var watch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < _insertCount; i++)
            {
                using var cmd = new NpgsqlCommand("INSERT INTO Books (Title, PublicationYear, AuthorId) VALUES (@title, 2026, @authorId)", conn);
                cmd.Parameters.AddWithValue("title", $"PerfTest_Auto_{i}");
                cmd.Parameters.AddWithValue("authorId", _testAuthorId);
                await cmd.ExecuteNonQueryAsync(); // Implicit auto-commit on every query
            }

            watch.Stop();
            await CleanupPerformanceTestDataAsync(); // Clean after test
            return watch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Approach 2: Batch Commits (Commit every 100 inserts).
        /// </summary>
        public async Task<long> RunBatchCommitInsertAsync()
        {
            await CleanupPerformanceTestDataAsync();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var watch = System.Diagnostics.Stopwatch.StartNew();

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

                    // Start a new transaction for the next 100 if we are not done
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

        /// <summary>
        /// Approach 3: Single Transaction & Statement Batching (Execute every 50 inserts). Fastest method.
        /// Uses NpgsqlBatch which perfectly maps to the Java addBatch() / executeBatch() from the assignment.
        /// </summary>
        public async Task<long> RunSingleTransactionBatchAsync()
        {
            await CleanupPerformanceTestDataAsync();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var watch = System.Diagnostics.Stopwatch.StartNew();

            using var tx = await conn.BeginTransactionAsync();
            var batch = new NpgsqlBatch(conn, tx);

            for (int i = 0; i < _insertCount; i++)
            {
                var batchCmd = new NpgsqlBatchCommand("INSERT INTO Books (Title, PublicationYear, AuthorId) VALUES ($1, 2026, $2)");
                batchCmd.Parameters.Add(new NpgsqlParameter { Value = $"PerfTest_FullBatch_{i}" });
                batchCmd.Parameters.Add(new NpgsqlParameter { Value = _testAuthorId });

                batch.BatchCommands.Add(batchCmd);

                // executeBatch() every 50 records as requested in the PDF
                if ((i + 1) % 50 == 0)
                {
                    await batch.ExecuteNonQueryAsync();
                    batch.BatchCommands.Clear();
                }
            }

            // Execute any remaining commands
            if (batch.BatchCommands.Count > 0)
            {
                await batch.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync(); // Final single commit

            watch.Stop();
            await CleanupPerformanceTestDataAsync();
            return watch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Runs the full benchmark automatically (3 runs per approach) and returns the formatted report.
        /// </summary>
        public async Task<string> RunFullBenchmarkAsync()
        {
            var log = new StringBuilder();
            log.AppendLine($"--- PERFORMANCE BENCHMARK ({_insertCount} INSERTS) ---");
            log.AppendLine("Running each approach 3 times. Please wait, this will take some time...\n");

            // 1. Auto Commit
            log.AppendLine("1. Testing Auto-Commit (1 tx per insert)...");
            long auto1 = await RunAutoCommitInsertAsync();
            long auto2 = await RunAutoCommitInsertAsync();
            long auto3 = await RunAutoCommitInsertAsync();
            long autoAvg = (auto1 + auto2 + auto3) / 3;
            log.AppendLine($"   Runs: {auto1}ms, {auto2}ms, {auto3}ms | AVERAGE: {autoAvg}ms\n");

            // 2. Batch Commits (100)
            log.AppendLine("2. Testing Batch Commits (Commit every 100 inserts)...");
            long batch1 = await RunBatchCommitInsertAsync();
            long batch2 = await RunBatchCommitInsertAsync();
            long batch3 = await RunBatchCommitInsertAsync();
            long batchAvg = (batch1 + batch2 + batch3) / 3;
            log.AppendLine($"   Runs: {batch1}ms, {batch2}ms, {batch3}ms | AVERAGE: {batchAvg}ms\n");

            // 3. Single Tx Batch
            log.AppendLine("3. Testing Single Transaction + ExecuteBatch(50)...");
            long single1 = await RunSingleTransactionBatchAsync();
            long single2 = await RunSingleTransactionBatchAsync();
            long single3 = await RunSingleTransactionBatchAsync();
            long singleAvg = (single1 + single2 + single3) / 3;
            log.AppendLine($"   Runs: {single1}ms, {single2}ms, {single3}ms | AVERAGE: {singleAvg}ms\n");

            log.AppendLine("--- BENCHMARK COMPLETE ---");
            log.AppendLine("Copy these results to your lab report!");
            log.AppendLine("----------------------------------------\n");

            return log.ToString();
        }
    }
}