using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

#if DISCORD
[assembly: AssemblyTitle("Low Consuming Discord")]
#elif STEAM
[assembly: AssemblyTitle("Low Consuming Steam")]
#else
[assembly: AssemblyTitle("Low Consuming Chrome")]
#endif
[assembly: AssemblyDescription("Modificador local / Local patcher. Requiere el cliente original o fuentes Chromium / Original client or Chromium sources required.")]
[assembly: AssemblyCompany("Low Consuming community")]
[assembly: AssemblyProduct("Low Consuming Tools")]
[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]
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
            get
            {
                return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            }
        }

        internal static string AssetsPath
        {
            get
            {
                return AssetStore.ResolveDirectory(PackageRoot);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint processId);
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0)
            {
                AttachConsole(UInt32.MaxValue);
                string reportPath = null;
                bool reportValidated = false;
                try
                {
                    if (args.Length == 1 && args[0] == "--version")
                    {
                        Console.WriteLine("Low Consuming " + SelectedProduct + " 0.3.0 (modificador / patcher)");
                        return 0;
                    }

                    if (args.Length < 2 || (args[0] != "--inspect" && args[0] != "--apply" && args[0] != "--restore"))
                    {
                        throw new ArgumentException("Uso: AppTawop.exe --inspect|--apply|--restore CARPETA [--report ARCHIVO.json]");
                    }

                    if (args.Length != 2 && (args.Length != 4 || args[2] != "--report"))
                    {
                        throw new ArgumentException("Argumentos inválidos.");
                    }

                    if (args.Length == 4)
                    {
                        reportPath = Path.GetFullPath(args[3]);
                        string selectedRoot = Path.GetFullPath(args[1]).TrimEnd('\\', '/');
                        Paths.RejectReparsePoints(reportPath);
                        if (Paths.IsInside(selectedRoot, reportPath) || String.Equals(selectedRoot, reportPath, StringComparison.OrdinalIgnoreCase) || File.Exists(reportPath) || Directory.Exists(reportPath))
                        {
                            throw new ArgumentException("El informe debe ser un archivo nuevo fuera de la instalación modificada.");
                        }

                        reportValidated = true;
                    }

                    PatchInspection result;
                    if (args[0] == "--inspect")
                    {
                        result = ModCore.Inspect(SelectedProduct, args[1], AssetsPath);
                    }
                    else if (args[0] == "--apply")
                    {
                        result = ModCore.Apply(SelectedProduct, args[1], AssetsPath);
                    }
                    else
                    {
                        result = ModCore.Restore(SelectedProduct, args[1]);
                    }
                    string json = ModCore.Json(result);
                    if (reportValidated)
                    {
                        WriteNewReport(reportPath, json);
                    }

                    Console.WriteLine(json);
                    return 0;
                }
                catch (Exception error)
                {
                    string json = ModCore.Json(new { Product = SelectedProduct.ToString(), Status = "Error", Error = error.Message });
                    Console.Error.WriteLine(json);
                    if (reportValidated)
                    {
                        try
                        {
                            WriteNewReport(reportPath, json);
                        }
                        catch
                        {
                        }
                    }

                    return 1;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ModForm(SelectedProduct));
            return 0;
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

        internal static string DetectInstall(Product product)
        {
            try
            {
                if (product == Product.Discord)
                {
                    string parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord");
                    if (Directory.Exists(parent))
                    {
                        string[] apps = Directory.GetDirectories(parent, "app-*");
                        Array.Sort(apps, delegate (string left, string right)
                        {
                            return Directory.GetLastWriteTimeUtc(right).CompareTo(Directory.GetLastWriteTimeUtc(left));
                        });
                        foreach (string app in apps)
                        {
                            if (File.Exists(Path.Combine(app, "Discord.exe")))
                            {
                                return app;
                            }
                        }
                    }
                }
                else if (product == Product.Steam)
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    {
                        string path = key == null ? null : key.GetValue("SteamPath") as string;
                        if (!String.IsNullOrEmpty(path) && File.Exists(Path.Combine(path, "steam.exe")))
                        {
                            return Path.GetFullPath(path);
                        }
                    }

                    string standard = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
                    if (File.Exists(Path.Combine(standard, "steam.exe")))
                    {
                        return standard;
                    }
                }
            }
            catch
            {
            }

            return "";
        }
    }

    internal sealed class ModForm : Form
    {
        private readonly Product product;
        private readonly TextBox path = new TextBox();
        private readonly TextBox details = new TextBox();
        private readonly Label status = new Label();
        private readonly Button inspect = new Button();
        private readonly Button apply = new Button();
        private readonly Button restore = new Button();
        private readonly Button browse = new Button();
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private PatchInspection lastInspection;
        private bool busy;
        public ModForm(Product selectedProduct)
        {
            product = selectedProduct;
            Text = "Low Consuming " + product + " — Modificador / Patcher";
            Size = new Size(900, 730);
            MinimumSize = new Size(580, 450);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10);
            AccessibleName = Text;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            TableLayoutPanel frame = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            frame.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(frame);
            Panel scroller = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            frame.Controls.Add(scroller, 0, 0);
            TableLayoutPanel content = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(20),
                ColumnCount = 1,
                RowCount = 8
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 8; i++)
            {
                content.RowStyles.Add(i == 6 ? new RowStyle(SizeType.Absolute, 220) : new RowStyle(SizeType.AutoSize));
            }

            scroller.Controls.Add(content);
            Label title = LabelFor("Low Consuming " + product, true);
            title.Font = new Font("Segoe UI", 23, FontStyle.Bold);
            content.Controls.Add(title, 0, 0);
            content.Controls.Add(LabelFor("MOD KIT 0.3 · Código y parches propios · Interfaz de bajo costo", false), 0, 1);
            string description;
            if (product == Product.Chrome)
            {
                description = "Modifica las fuentes de Chromium con la marca ChromeTawop y ahorro de memoria agresivo. "
                    + "Después hay que compilar el navegador con C++ (ver README_CHROMIUM.md).";
            }
            else if (product == Product.Discord)
            {
                description = "Reconstruye los ASAR con estilos simples y un preload que pausa vídeos decorativos al ocultar la ventana. "
                    + "Conserva el motor original y verifica la integridad de los recursos.";
            }
            else
            {
                description = "Modifica los CSS de Steam para reducir movimiento, fondos e imágenes decorativas. "
                    + "Mantiene los textos y controles del cliente original.";
            }
            content.Controls.Add(LabelFor(description, false), 0, 2);
            content.Controls.Add(LabelFor(product == Product.Chrome ? "Carpeta src del checkout de Chromium:" : "Carpeta de la instalación que vas a modificar:", false), 0, 3);
            TableLayoutPanel location = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true
            };
            location.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            location.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            path.Dock = DockStyle.Fill;
            path.Text = Program.DetectInstall(product);
            path.AccessibleName = "Carpeta a modificar";
            path.Margin = new Padding(0, 4, 8, 8);
            browse.Text = "&Elegir carpeta";
            browse.AutoSize = true;
            location.Controls.Add(path, 0, 0);
            location.Controls.Add(browse, 1, 0);
            content.Controls.Add(location, 0, 4);
            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            SetButton(inspect, "&Analizar archivos");
            SetButton(apply, "Aplicar &parches");
            SetButton(restore, "&Restaurar originales");
            Button sources = new Button();
            SetButton(sources, "Abrir &fuentes");
            actions.Controls.Add(inspect);
            actions.Controls.Add(apply);
            actions.Controls.Add(restore);
            actions.Controls.Add(sources);
            content.Controls.Add(actions, 0, 5);
            details.Dock = DockStyle.Fill;
            details.Multiline = true;
            details.ReadOnly = true;
            details.ScrollBars = ScrollBars.Both;
            details.WordWrap = false;
            details.Font = new Font("Consolas", 9);
            details.AccessibleName = "Resultado y archivos del parche";
            details.Text = "Analizá la carpeta para ver los archivos y hashes antes de aplicar.\r\n\r\n"
                + "Este ejecutable es un modificador, no una app reescrita ni un navegador.\r\n"
                + "El ahorro de RAM/CPU todavía requiere mediciones comparables.";
            content.Controls.Add(details, 0, 6);
            content.Controls.Add(LabelFor(
                "Cerrá el cliente desde su bandeja antes de aplicar o restaurar. "
                + "Se guardan originales en .tawop dentro de la instalación. "
                + "Las actualizaciones pueden reemplazar los parches: el restaurador conserva cambios externos.", false), 0, 7);
            status.Dock = DockStyle.Fill;
            status.AutoSize = true;
            status.MinimumSize = new Size(0, 38);
            status.Padding = new Padding(20, 8, 20, 8);
            status.Text = "Listo. Ningún archivo fue modificado.";
            status.AccessibleName = "Estado de modificación";
            frame.Controls.Add(status, 0, 1);
            if (!SystemInformation.HighContrast)
            {
                BackColor = Color.FromArgb(17, 26, 23);
                ForeColor = Color.FromArgb(223, 247, 233);
                title.ForeColor = Color.FromArgb(121, 242, 180);
                details.BackColor = Color.FromArgb(12, 20, 17);
                details.ForeColor = ForeColor;
                foreach (Button button in new Button[]
                {
                    inspect,
                    apply,
                    restore,
                    browse,
                    sources
                }

                )
                {
                    button.BackColor = Color.FromArgb(37, 62, 49);
                    button.ForeColor = ForeColor;
                    button.FlatStyle = FlatStyle.Flat;
                }
            }

            path.TextChanged += delegate
            {
                lastInspection = null;
            };
            browse.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = product == Product.Chrome ? "Seleccioná src de Chromium (no Google Chrome instalado)" : "Seleccioná la carpeta del cliente";
                    if (Directory.Exists(path.Text))
                    {
                        dialog.SelectedPath = path.Text;
                    }

                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        path.Text = dialog.SelectedPath;
                    }
                }
            };
            sources.Click += delegate
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = Path.Combine(Program.PackageRoot, "README.md"), UseShellExecute = true });
                }
                catch (Exception e)
                {
                    ShowError(e);
                }
            };
            inspect.Click += delegate
            {
                BeginOperation("inspect");
            };
            apply.Click += delegate
            {
                if (lastInspection == null || lastInspection.Status != "Preparado; todavía no aplicado")
                {
                    MessageBox.Show(this, "Primero analizá la carpeta para revisar los archivos.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string message = "Se modificarán " + lastInspection.Files.Count + " archivos en:\r\n"
                    + lastInspection.Root + "\r\n\r\n" + description
                    + "\r\n\r\nSe guardará respaldo local. No hay un ahorro de RAM demostrado. ¿Aplicar?";
                if (MessageBox.Show(this, message, "Aplicar parches Tawop", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    BeginOperation("apply");
                }
            };
            restore.Click += delegate
            {
                BeginOperation("restore");
            };
            worker.DoWork += delegate (object sender, DoWorkEventArgs e)
            {
                string[] request = (string[])e.Argument;
                if (request[0] == "inspect")
                {
                    e.Result = ModCore.Inspect(product, request[1], Program.AssetsPath);
                }
                else if (request[0] == "apply")
                {
                    e.Result = ModCore.Apply(product, request[1], Program.AssetsPath);
                }
                else
                {
                    e.Result = ModCore.Restore(product, request[1]);
                }
            };
            worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
            {
                SetBusy(false);
                if (e.Error != null)
                {
                    status.Text = "No se completó la operación.";
                    details.Text = e.Error.Message;
                    ShowError(e.Error);
                    return;
                }

                lastInspection = (PatchInspection)e.Result;
                StringBuilder output = new StringBuilder();
                output.AppendLine(lastInspection.Status);
                output.AppendLine(lastInspection.Kind);
                output.AppendLine(lastInspection.MemoryStatus);
                output.AppendLine(lastInspection.Root);
                output.AppendLine();
                foreach (PatchRecord file in lastInspection.Files)
                {
                    output.AppendLine(file.RelativePath);
                    output.AppendLine("  " + file.BeforeBytes + " → " + file.AfterBytes + " bytes");
                    output.AppendLine("  SHA256 antes: " + file.BeforeHash);
                    output.AppendLine("  SHA256 parche: " + file.AfterHash);
                }

                if (lastInspection.BackupPath != null)
                {
                    output.AppendLine("\r\nRespaldo: " + lastInspection.BackupPath);
                }

                details.Text = output.ToString();
                status.Text = lastInspection.Status;
            };
            FormClosing += delegate (object sender, FormClosingEventArgs e)
            {
                if (busy)
                {
                    e.Cancel = true;
                    status.Text = "Esperá a que termine la operación antes de cerrar.";
                }
            };
        }

        private static Label LabelFor(string text, bool heading)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, heading ? 5 : 12)
            };
        }

        private static void SetButton(Button button, string text)
        {
            button.Text = text;
            button.AutoSize = true;
            button.Padding = new Padding(5);
            button.Margin = new Padding(0, 0, 8, 8);
        }

        private void SetBusy(bool value)
        {
            busy = value;
            inspect.Enabled = !value;
            apply.Enabled = !value;
            restore.Enabled = !value;
            browse.Enabled = !value;
            path.Enabled = !value;
        }

        private void BeginOperation(string action)
        {
            if (busy)
            {
                return;
            }

            SetBusy(true);
            status.Text = action == "inspect" ? "Analizando archivos sin escribir..." : action == "apply" ? "Aplicando con respaldo..." : "Restaurando archivos verificados...";
            worker.RunWorkerAsync(new string[] { action, path.Text });
        }

        private void ShowError(Exception error)
        {
            MessageBox.Show(this, error.Message + "\r\n\r\nSi Windows deniega acceso a Program Files, abrí este modificador como administrador.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                worker.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
