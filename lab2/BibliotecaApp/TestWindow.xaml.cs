using BibliotecaApp.Test;
using System.Windows;


namespace BibliotecaApp
{
    public partial class TestWindow : Window
    {
        private TransactionDemos _demos;

        public TestWindow()
        {
            InitializeComponent();
            _demos = new TransactionDemos();
        }

        private async void btnDirtyRead_Click(object sender, RoutedEventArgs e)
        {
            btnDirtyRead.IsEnabled = false;
            txtLog.AppendText("Rulare Demo A (Dirty Read). Vă rugăm așteptați (~4 secunde)...\n");

            string result = await _demos.DemoDirtyReadAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd(); // Deruleaza automat textul in jos
            btnDirtyRead.IsEnabled = true;
        }

        private async void btnNonRepeatable_Click(object sender, RoutedEventArgs e)
        {
            btnNonRepeatable.IsEnabled = false;
            txtLog.AppendText("Rulare Demo B (Non-Repeatable Read). Vă rugăm așteptați...\n");

            string result = await _demos.DemoNonRepeatableReadAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            btnNonRepeatable.IsEnabled = true;
        }

        private async void btnPhantom_Click(object sender, RoutedEventArgs e)
        {
            btnPhantom.IsEnabled = false;
            txtLog.AppendText("Rulare Demo C (Phantom Read). Vă rugăm așteptați...\n");

            string result = await _demos.DemoPhantomReadAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            btnPhantom.IsEnabled = true;
        }

        private async void btnLostUpdate_Click(object sender, RoutedEventArgs e)
        {
            btnLostUpdate.IsEnabled = false;
            txtLog.AppendText("Rulare Demo D (Lost Update). Vă rugăm așteptați...\n");

            string result = await _demos.DemoLostUpdateAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            btnLostUpdate.IsEnabled = true;
        }

        private void btnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
        }

        private async void btnDeadlock_Click(object sender, RoutedEventArgs e)
        {
            btnDeadlock.IsEnabled = false;
            txtLog.AppendText("Rulare Demo E1 (Deadlock). Please wait...\n");

            string result = await _demos.DemoDeadlockErrorAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            btnDeadlock.IsEnabled = true;
        }

        private async void btnDeadlockResolved_Click(object sender, RoutedEventArgs e)
        {
            btnDeadlockResolved.IsEnabled = false;
            txtLog.AppendText("Rulare Demo E2 (Deadlock Resolved). Please wait...\n");

            string result = await _demos.DemoDeadlockResolvedAsync();

            txtLog.AppendText(result);
            txtLog.ScrollToEnd();
            btnDeadlockResolved.IsEnabled = true;
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
        }
    }
}
