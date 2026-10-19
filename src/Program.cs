using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

#if DISCORD
[assembly: AssemblyTitle("Low Consuming Discord")]
#elif STEAM
[assembly: AssemblyTitle("Low Consuming Steam")]
#else
[assembly: AssemblyTitle("Low Consuming Chrome")]
#endif
[assembly: AssemblyDescription("Reversible client resource patches and a Chrome performance profile.")]
[assembly: AssemblyCompany("Low Consuming community")]
[assembly: AssemblyProduct("Low Consuming Tools")]
[assembly: AssemblyVersion("0.5.0.0")]
[assembly: AssemblyFileVersion("0.5.0.0")]
namespace Tawop
{
    internal static class Program
    {
#if DISCORD
        internal static readonly Product SelectedProduct = Product.Discord;
#elif STEAM
        internal static readonly Product SelectedProduct = Product.Steam;
#else
        internal static readonly Product SelectedProduct = Product.Chrome;
#endif
        internal static string PackageRoot
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/'); }
        }
        internal static string AssetsPath
        {
            get { return AssetStore.ResolveDirectory(PackageRoot); }
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint processId);

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0) { return RunCommand(args); }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ModForm(SelectedProduct));
            return 0;
        }
        private static int RunCommand(string[] args)
        {
            AttachConsole(UInt32.MaxValue);
            string reportPath = null;
            bool reportValidated = false;
            try
            {
                if (args.Length == 1 && args[0] == "--version")
                {
                    Console.WriteLine("Low Consuming " + SelectedProduct + " 0.5.0 (local patch tool)");
                    return 0;
                }
                if (args.Length < 2 || (args[0] != "--inspect" && args[0] != "--apply" && args[0] != "--restore"))
                {
                    throw new ArgumentException("Usage: AppTawop.exe --inspect|--apply|--restore FOLDER [--report NEW-FILE.json]");
                }
                if (args.Length != 2 && (args.Length != 4 || args[2] != "--report"))
                {
                    throw new ArgumentException("Invalid arguments.");
                }
                string root = AutoSetup.Resolve(SelectedProduct, args[1]);
                if (args.Length == 4)
                {
                    reportPath = Path.GetFullPath(args[3]);
                    Paths.RejectReparsePoints(reportPath);
                    if (Paths.IsInside(root, reportPath) || String.Equals(root, reportPath, StringComparison.OrdinalIgnoreCase)
                        || File.Exists(reportPath) || Directory.Exists(reportPath))
                    {
                        throw new ArgumentException("The report must be a new file outside the selected installation.");
                    }
                    reportValidated = true;
                }
                PatchInspection result;
                if (args[0] == "--inspect") { result = ModCore.Inspect(SelectedProduct, root, AssetsPath); }
                else if (args[0] == "--apply") { result = ModCore.Apply(SelectedProduct, root, AssetsPath); }
                else { result = ModCore.Restore(SelectedProduct, root); }
                string json = ModCore.Json(result);
                if (reportValidated) { WriteNewReport(reportPath, json); }
                Console.WriteLine(json);
                return 0;
            }
            catch (Exception error)
            {
                string json = ModCore.Json(new { Product = SelectedProduct.ToString(), Status = "Error", Error = error.Message });
                Console.Error.WriteLine(json);
                if (reportValidated)
                {
                    try { WriteNewReport(reportPath, json); }
                    catch { }
                }
                return 1;
            }
        }
        private static void WriteNewReport(string path, string json)
        {
            Paths.RejectReparsePoints(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] content = new UTF8Encoding(false).GetBytes(json);
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content, 0, content.Length);
                stream.Flush(true);
            }
        }
    }

    internal sealed class ModForm : Form
    {
        private readonly Product product;
        private readonly Button start = new Button();
        private readonly Label found = new Label();
        private readonly Label status = new Label();
        private readonly TextBox details = new TextBox();
        private readonly LinkLabel choose = new LinkLabel();
        private readonly LinkLabel undo = new LinkLabel();
        private readonly LinkLabel showDetails = new LinkLabel();
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private string selectedRoot;
        private bool busy;
        private bool finished;
        private bool detecting;
        private bool stopping;
        private bool closeWhenFinished;
        private CancellationTokenSource operationCancellation;

        internal string SelectedRoot
        {
            get { return selectedRoot; }
            set { selectedRoot = value; UpdateReadyState(); }
        }

        public ModForm(Product selectedProduct) : this(selectedProduct, true)
        {
        }

        public ModForm(Product selectedProduct, bool discoverInstallation)
        {
            product = selectedProduct;
            Text = "Low Consuming " + product;
            AccessibleName = Text;
            Size = new Size(650, 510);
            MinimumSize = new Size(500, 460);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 11);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            TableLayoutPanel content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, Padding = new Padding(24)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int row = 0; row < 9; row++)
            {
                content.RowStyles.Add(new RowStyle(row == 7 ? SizeType.Percent : SizeType.AutoSize, row == 7 ? 100 : 0));
            }
            Controls.Add(content);
            Label title = MakeLabel("Low Consuming " + product);
            title.Font = new Font("Segoe UI", 24, FontStyle.Bold);
            title.Margin = new Padding(0, 0, 0, 12);
            content.Controls.Add(title, 0, 0);
            content.Controls.Add(MakeLabel(product == Product.Chrome
                ? "Turn on Chrome Maximum Memory Saver and reduce preloading and background work. Your tabs stay open."
                : "Reduce decorative work in " + product + ". Click once to check, back up and apply the aggressive profile."), 0, 1);
            Label steps = MakeLabel(product == Product.Chrome
                ? "Inactive tabs may reload. Changes can need a policy refresh or browser restart."
                : product == Product.Discord
                    ? "Animated images become stills. Spellcheck is off. Finish calls first."
                    : "Low performance and bandwidth modes are on. Community content is reduced.");
            steps.Font = new Font(Font.FontFamily, 10);
            content.Controls.Add(steps, 0, 2);
            ConfigureLabel(found);
            found.AccessibleName = "Installation detection";
            content.Controls.Add(found, 0, 3);
            start.Dock = DockStyle.Fill;
            start.Height = 50;
            start.Margin = new Padding(0, 6, 0, 14);
            start.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            start.AccessibleName = "Start automatic setup";
            content.Controls.Add(start, 0, 4);
            ConfigureLabel(status);
            status.MinimumSize = new Size(0, 52);
            status.AccessibleName = "Setup status";
            content.Controls.Add(status, 0, 5);
            FlowLayoutPanel links = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 8)
            };
            ConfigureLink(choose, "Choose a different folder");
            ConfigureLink(undo, "Undo changes");
            ConfigureLink(showDetails, "Details");
            links.Controls.Add(choose);
            links.Controls.Add(undo);
            links.Controls.Add(showDetails);
            content.Controls.Add(links, 0, 6);
            details.Dock = DockStyle.Fill;
            details.Multiline = true;
            details.ReadOnly = true;
            details.ScrollBars = ScrollBars.Vertical;
            details.Font = new Font("Consolas", 9);
            details.AccessibleName = "Technical details";
            details.Visible = false;
            details.Text = "Version 0.5.0. Local, reversible patches. No account or password is needed.\r\n"
                + "Resource savings still need measurement. No data is uploaded by this tool.";
            content.Controls.Add(details, 0, 7);
            Label note = MakeLabel("Experimental · Resource savings are not verified.");
            note.Font = new Font(Font.FontFamily, 9);
            note.Margin = new Padding(0, 10, 0, 0);
            content.Controls.Add(note, 0, 8);
            if (!SystemInformation.HighContrast)
            {
                BackColor = Color.FromArgb(19, 25, 24);
                ForeColor = Color.FromArgb(235, 242, 239);
                title.ForeColor = Color.FromArgb(147, 236, 184);
                start.BackColor = Color.FromArgb(147, 236, 184);
                start.ForeColor = Color.FromArgb(13, 29, 20);
                start.FlatStyle = FlatStyle.Flat;
                start.FlatAppearance.BorderSize = 0;
                details.BackColor = Color.FromArgb(26, 34, 31);
                details.ForeColor = ForeColor;
                foreach (LinkLabel link in new LinkLabel[] { choose, undo, showDetails })
                {
                    link.LinkColor = Color.FromArgb(166, 213, 240);
                    link.ActiveLinkColor = Color.White;
                    link.VisitedLinkColor = link.LinkColor;
                }
            }
            worker.WorkerReportsProgress = true;
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                object[] request = (object[])e.Argument;
                CancellationToken cancellation = (CancellationToken)request[3];
                try
                {
                    if ((bool)request[2])
                    {
                        e.Result = product == Product.Chrome ? ChromeRuntimeProfile.Detect(cancellation) : AutoSetup.Detect(product, cancellation);
                        cancellation.ThrowIfCancellationRequested();
                        return;
                    }
                    bool restore = (bool)request[1];
                    if (product == Product.Chrome)
                    {
                        e.Result = ChromeRuntimeProfile.Run((string)request[0], restore,
                            delegate(string message) { worker.ReportProgress(0, message); }, cancellation);
                        return;
                    }
                    string assets = restore ? null : AssetStore.ResolveDirectory(Program.PackageRoot, cancellation);
                    e.Result = AutoSetup.Run(product, (string)request[0], assets, restore,
                        delegate(string message) { worker.ReportProgress(0, message); }, cancellation);
                }
                catch (OperationCanceledException)
                {
                    if (!cancellation.IsCancellationRequested) { throw; }
                    e.Cancel = true;
                }
            };
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
            {
                string message = (string)e.UserState;
                status.Text = stopping && !message.StartsWith("Restoring", StringComparison.Ordinal) && !message.StartsWith("Recovering", StringComparison.Ordinal)
                    ? "Stopping safely. Finishing the current change or restoring the backup..." : message;
            };
            worker.RunWorkerCompleted += Complete;
            start.Click += delegate
            {
                if (busy) { RequestStop(false); return; }
                if (finished) { Close(); return; }
                if (String.IsNullOrEmpty(selectedRoot) && !ChooseFolder()) { return; }
                Begin(false);
            };
            choose.LinkClicked += delegate { ChooseFolder(); };
            undo.LinkClicked += delegate { Begin(true); };
            showDetails.LinkClicked += delegate
            {
                details.Visible = !details.Visible;
                showDetails.Text = details.Visible ? "Hide details" : "Details";
                if (details.Visible && Height < 650) { Height = 650; }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (busy) { e.Cancel = true; RequestStop(true); }
            };
            UpdateReadyState();
            if (discoverInstallation)
            {
                found.Text = "Looking for " + product + "...";
                start.Enabled = false;
                Shown += delegate { BeginDetection(); };
            }
        }

        private static Label MakeLabel(string text)
        {
            Label label = new Label { Text = text };
            ConfigureLabel(label);
            return label;
        }
        private static void ConfigureLabel(Label label)
        {
            label.Dock = DockStyle.Fill;
            label.AutoSize = true;
            label.Margin = new Padding(0, 0, 0, 10);
        }
        private static void ConfigureLink(LinkLabel link, string text)
        {
            link.Text = text;
            link.AutoSize = true;
            link.Margin = new Padding(0, 3, 18, 3);
        }
        private bool HasBackup()
        {
            if (product == Product.Chrome) { return ChromeRuntimeProfile.HasBackup(); }
            return !String.IsNullOrEmpty(selectedRoot)
                && File.Exists(Path.Combine(selectedRoot, ".tawop", product.ToString().ToLowerInvariant() + ".json"));
        }
        private void UpdateReadyState()
        {
            finished = false;
            found.Text = String.IsNullOrEmpty(selectedRoot)
                ? product + " was not found automatically."
                : product + " found. No folder setup needed.";
            start.Text = String.IsNullOrEmpty(selectedRoot)
                ? "Choose " + product + " folder" : "Optimize " + product;
            status.Text = product == Product.Chrome ? "Ready. Undo will remove only this tool's performance policies." : "Ready. Your original files will be backed up.";
            undo.Enabled = HasBackup();
        }
        private bool ChooseFolder()
        {
            if (busy) { return false; }
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = product == Product.Chrome
                    ? "Choose the Google Chrome Application folder containing chrome.exe."
                    : "Choose the " + product + " folder. Its main folder or a version folder both work.";
                if (!String.IsNullOrEmpty(selectedRoot) && Directory.Exists(selectedRoot)) { dialog.SelectedPath = selectedRoot; }
                if (dialog.ShowDialog(this) != DialogResult.OK) { return false; }
                try
                {
                    selectedRoot = product == Product.Chrome
                        ? ChromeRuntimeProfile.Resolve(dialog.SelectedPath, CancellationToken.None)
                        : AutoSetup.Resolve(product, dialog.SelectedPath);
                    UpdateReadyState();
                    return true;
                }
                catch (Exception error) { ShowFailure(error); return false; }
            }
        }
        private void Begin(bool restore)
        {
            if (busy) { return; }
            finished = false;
            detecting = false;
            stopping = false;
            closeWhenFinished = false;
            operationCancellation = new CancellationTokenSource();
            SetBusy(true);
            start.Text = "Stop";
            status.Text = "Checking the installation. Please wait.";
            worker.RunWorkerAsync(new object[] { selectedRoot, restore, false, operationCancellation.Token });
        }
        private void BeginDetection()
        {
            if (busy) { return; }
            detecting = true;
            stopping = false;
            closeWhenFinished = false;
            operationCancellation = new CancellationTokenSource();
            SetBusy(true);
            start.Text = "Stop";
            status.Text = "Finding the installation. You can stop this search.";
            worker.RunWorkerAsync(new object[] { null, false, true, operationCancellation.Token });
        }
        private void RequestStop(bool closeAfter)
        {
            closeWhenFinished = closeWhenFinished || closeAfter;
            if (!busy || stopping) { return; }
            stopping = true;
            start.Text = "Stopping safely...";
            start.Enabled = false;
            status.Text = "Stopping safely. Finishing the current change or restoring the backup...";
            operationCancellation.Cancel();
        }
        private void Complete(object sender, RunWorkerCompletedEventArgs e)
        {
            bool wasDetecting = detecting;
            detecting = false;
            stopping = false;
            if (operationCancellation != null)
            {
                operationCancellation.Dispose();
                operationCancellation = null;
            }
            SetBusy(false);
            if (e.Error != null)
            {
                closeWhenFinished = false;
                ShowFailure(e.Error);
                return;
            }
            if (e.Cancelled)
            {
                UpdateReadyState();
                status.Text = "Stopped. Any remaining backup is kept for Undo changes.";
                details.Text = "The operation stopped at a safe file boundary. No worker is still running.\r\n"
                    + "If Undo was stopped partway through, use Undo changes again to finish restoration.";
                if (closeWhenFinished) { Close(); }
                return;
            }
            if (wasDetecting)
            {
                selectedRoot = e.Result as string;
                UpdateReadyState();
                if (closeWhenFinished) { Close(); }
                return;
            }
            AutoSetupResult result = (AutoSetupResult)e.Result;
            selectedRoot = result.Root;
            finished = true;
            start.Text = "Done — close";
            status.Text = result.Message;
            undo.Enabled = HasBackup();
            StringBuilder text = new StringBuilder();
            text.AppendLine(result.Message);
            text.AppendLine("Installation: " + result.Root);
            if (result.Inspection != null)
            {
                text.AppendLine(result.Inspection.Kind);
                foreach (PatchRecord file in result.Inspection.Files)
                {
                    text.AppendLine(file.RelativePath);
                    text.AppendLine("SHA256: " + file.AfterHash);
                }
                if (!String.IsNullOrEmpty(result.Inspection.BackupPath)) { text.AppendLine("Backup: " + result.Inspection.BackupPath); }
            }
            details.Text = text.ToString();
            if (closeWhenFinished) { Close(); }
        }
        private void ShowFailure(Exception error)
        {
            finished = false;
            start.Text = "Try again";
            details.Text = error.Message;
            string message = error.Message ?? "";
            if (message.IndexOf("fuse", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("integrity", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                status.Text = "This version cannot be changed safely. See Details for compatibility information.";
            }
            else if (IsPermissionError(error))
            {
                status.Text = "Windows denied access. Run this tool as administrator only if this folder requires it.";
            }
            else if (IsMissingFileError(error))
            {
                status.Text = "A required file is missing. Choose the complete "
                    + product + " folder and try again.";
            }
            else if (product == Product.Chrome && message.IndexOf("polic", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                status.Text = "Chrome's performance policies could not be changed. Open Details for the reason.";
            }
            else if (message.IndexOf("tray", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("Quit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                status.Text = message;
            }
            else { status.Text = "Setup could not finish. Open Details for the reason, then try again."; }
            undo.Enabled = HasBackup();
        }
        private static bool IsPermissionError(Exception error)
        {
            for (Exception current = error; current != null; current = current.InnerException)
            {
                Win32Exception native = current as Win32Exception;
                if (current is UnauthorizedAccessException || (native != null && native.NativeErrorCode == 5))
                {
                    return true;
                }
            }
            return false;
        }
        private static bool IsMissingFileError(Exception error)
        {
            for (Exception current = error; current != null; current = current.InnerException)
            {
                if (current is DirectoryNotFoundException || current is FileNotFoundException) { return true; }
            }
            return false;
        }
        private void SetBusy(bool value)
        {
            busy = value;
            start.Enabled = !stopping;
            start.AccessibleName = value ? "Stop the current operation" : "Start automatic setup";
            choose.Enabled = !value;
            undo.Enabled = !value && HasBackup();
            UseWaitCursor = false;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (operationCancellation != null) { operationCancellation.Dispose(); }
                worker.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
