using BibliotecaApp.DataBase;
using BibliotecaApp.Test;
using System.Diagnostics;
using System.Windows;

namespace BibliotecaApp
{
    public partial class TestWindow : Window
    {
        private readonly TransactionDemos _demos;
        private readonly LibraryRepository _repository;

        public TestWindow()
        {
            InitializeComponent();
            _demos = new TransactionDemos();
            this.Loaded += async (s, e) => await RefreshDataButtonsState();
            _repository = new LibraryRepository();
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {

            await _demos.CleanupIndexingDataAsync();
            base.OnClosing(e);
        }

        private async Task RefreshDataButtonsState()
        {
            bool exists = await _demos.CheckTestDataExistsAsync();
            btnSetupIndexingData.IsEnabled = !exists;
        }

        private async void btnDirtyRead_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Demo A (Dirty Read). Please wait (~4 seconds)...\n");

            string result = await _demos.DemoDirtyReadAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnNonRepeatable_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Demo B (Non-Repeatable Read). Please wait...\n");

            string result = await _demos.DemoNonRepeatableReadAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnPhantom_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Demo C (Phantom Read). Please wait...\n");

            string result = await _demos.DemoPhantomReadAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnLostUpdate_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Demo D (Lost Update). Please wait...\n");

            string result = await _demos.DemoLostUpdateAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private void btnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
        }

        private async void btnDeadlock_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Demo E1 (Deadlock Error). Please wait...\n");

            string result = await _demos.DemoDeadlockErrorAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnDeadlockResolved_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Demo E2 (Deadlock Resolved). Please wait...\n");

            string result = await _demos.DemoDeadlockResolvedAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnPerfAuto_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Auto-Commit Insert (5000 rows). This test takes the longest...\n");

            long ms = await _demos.RunAutoCommitInsertAsync();

            txtLog.AppendText($"[RESULT] Auto-Commit took: {ms} ms.\n\n");
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnPerfBatch100_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Batch Insert (Commit every 100 rows)...\n");

            long ms = await _demos.RunBatchCommitInsertAsync();

            txtLog.AppendText($"[RESULT] Commit every 100 took: {ms} ms.\n\n");
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnPerfSingle_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Single Transaction Insert with NpgsqlBatch...\n");

            long ms = await _demos.RunSingleTransactionBatchAsync();

            txtLog.AppendText($"[RESULT] Single Transaction took: {ms} ms.\n\n");
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnRunFullBenchmark_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("STARTING FULL BENCHMARK! This operation may take 1-2 minutes...\n");

            string result = await _demos.RunFullBenchmarkAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnPoolBenchmark_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Task A (Connection Pooling Overhead Benchmark)...\n");

            string result = await _demos.RunConnectionPoolBenchmarkAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnLeak_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Task B (Simulate Connection Leak). Warning: Watch for timeout error...\n");

            string result = await _demos.SimulateConnectionLeakAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnNPlusOne_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running N+1 query demo...\n");

            // Added await here
            string result = await _demos.RunNPlusOneDemoAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnEagerLoading_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Eager Loading demo...\n");

            string result = await _demos.RunEagerLoadingDemoAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnSetupIndexingData_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Initializing 10,000 records...\n");

            string result = await _demos.SetupIndexingDataAsync();

            txtLog.AppendText(result);
            await RefreshDataButtonsState(); // Va dezactiva butonul
            SetButtonsState(true);
        }

        private async void btnCleanupIndexingData_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Cleaning up test data...\n");

            string result = await _demos.CleanupIndexingDataAsync();

            txtLog.AppendText(result);
            await RefreshDataButtonsState(); // Va reactiva butonul
            SetButtonsState(true);
        }

        private async void btnGenerateReport_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Generating full benchmark report and table. Please wait...\n");

            string result = await _demos.RunFullIndexingReportAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnPaginationBenchmark_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Rulăm benchmark-urile pentru paginare. Durează câteva secunde...\n");

            string result = await _demos.RunPaginationBenchmarkAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnGetAuthorCached_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Se rulează testul de performanță pentru Cache...\n");

            // Apelează metoda din TransactionDemos
            string result = await _demos.RunCacheBenchmarkAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private void btnUpdateAuthorInvalidate_Click(object sender, RoutedEventArgs e)
        {
            txtLog.AppendText(_demos.InvalidateCacheDemo());
            txtLog.ScrollToEnd();
        }

        private void btnShowCacheStats_Click(object sender, RoutedEventArgs e)
        {
            txtLog.AppendText(_demos.GetCacheStatsDemo());
            txtLog.ScrollToEnd();
        }


        private void SetButtonsState(bool isEnabled)
        {
            btnDirtyRead.IsEnabled = isEnabled;
            btnNonRepeatable.IsEnabled = isEnabled;
            btnPhantom.IsEnabled = isEnabled;
            btnLostUpdate.IsEnabled = isEnabled;
            btnDeadlock.IsEnabled = isEnabled;
            btnDeadlockResolved.IsEnabled = isEnabled;
            btnPerfAuto.IsEnabled = isEnabled;
            btnPerfBatch100.IsEnabled = isEnabled;
            btnPerfSingle.IsEnabled = isEnabled;
            btnRunFullBenchmark.IsEnabled = isEnabled;
            btnSetupIndexingData.IsEnabled = isEnabled;
            btnCleanupIndexingData.IsEnabled = isEnabled;
            btnBenchmarkNoIndex.IsEnabled = isEnabled;
            btnBenchmarkWithIndex.IsEnabled = isEnabled;
            btnPaginationBenchmark.IsEnabled = isEnabled;

            if (isEnabled)
            {
                _ = RefreshDataButtonsState();
            }

            if (FindName("btnPoolBenchmark") is System.Windows.Controls.Button btnPool)
                btnPool.IsEnabled = isEnabled;

            if (FindName("btnLeak") is System.Windows.Controls.Button btnL)
                btnL.IsEnabled = isEnabled;

            if (FindName("btnNPlusOne") is System.Windows.Controls.Button btnA)
                btnA.IsEnabled = isEnabled;

            if (FindName("btnEagerLoading") is System.Windows.Controls.Button btnB)
                btnB.IsEnabled = isEnabled;

            if (FindName("btnGenerateReport") is System.Windows.Controls.Button btnC)
                btnC.IsEnabled = isEnabled;

            if (FindName("btnGetAuthorCached") is System.Windows.Controls.Button btnCache1)
                btnCache1.IsEnabled = isEnabled;

            if (FindName("btnUpdateAuthorInvalidate") is System.Windows.Controls.Button btnCache2)
                btnCache2.IsEnabled = isEnabled;

            if (FindName("btnShowCacheStats") is System.Windows.Controls.Button btnCache3)
                btnCache3.IsEnabled = isEnabled;

            if (FindName("btnBulkUpdateTest") is System.Windows.Controls.Button btnBulk)
                btnBulk.IsEnabled = isEnabled;

            if (FindName("btnPreparedStatements") is System.Windows.Controls.Button btnPrep) 
                btnPrep.IsEnabled = isEnabled;
        }

        private async void btnBenchmarkNoIndex_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText(await _demos.RunIndexingBenchmarkAsync(false));
            SetButtonsState(true);
        }

        private async void btnBenchmarkWithIndex_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText(await _demos.RunIndexingBenchmarkAsync(true));
            SetButtonsState(true);
        }

        private async void btnBulkUpdateTest_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Bulk Update tests on 10,000 records. Please wait...\n");

            string result = await _demos.RunBulkUpdateBenchmarkAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }

        private async void btnPreparedStatements_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            txtLog.AppendText("Running Prepared Statements benchmark. Please wait...\n");

            string result = await _demos.RunPreparedStatementBenchmarkAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            SetButtonsState(true);
        }
    }
}