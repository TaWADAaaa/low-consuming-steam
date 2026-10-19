using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

public static class GuiSmoke
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2)
            {
                throw new ArgumentException("Expected an executable and a screenshot output path.");
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Assembly assembly = Assembly.LoadFile(Path.GetFullPath(args[0]));
            Type formType = assembly.GetType("Tawop.ModForm", true);
            Type programType = assembly.GetType("Tawop.Program", true);
            object product = programType.GetField("SelectedProduct", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            string name = product.ToString();
            using (Form form = (Form)Activator.CreateInstance(formType, new object[] { product, false }))
            {
                PropertyInfo selectedRoot = formType.GetProperty("SelectedRoot", PrivateInstance);
                if (selectedRoot == null || !selectedRoot.CanWrite)
                {
                    throw new Exception("The installation selection property is missing.");
                }

                string exampleRoot = name == "Discord"
                    ? @"C:\Users\Example\AppData\Local\Discord\app-1.2.3"
                    : name == "Steam" ? @"C:\Program Files (x86)\Steam" : @"C:\Program Files\Google\Chrome\Application";
                selectedRoot.SetValue(form, exampleRoot, null);
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-32000, -32000);
                form.Show();
                Settle(form);

                List<Control> controls = Descendants(form);
                Button primary = FindSingleButton(controls);
                Control status = FindAccessible(controls, "Setup status");
                Control detection = FindAccessible(controls, "Installation detection");
                TextBox details = FindAccessible(controls, "Technical details") as TextBox;
                LinkLabel detailLink = FindLink(controls, "Details");
                Assert(primary.AccessibleName == "Start automatic setup", "The primary action needs an accessible name.");
                Assert(primary.Text == "Optimize " + name, "The primary action must describe the selected product.");
                Assert(form.Text == "Low Consuming " + name, "The window title must be in English.");
                Assert(detection.Text == name + " found. No folder setup needed.", "Automatic detection needs a simple English result.");
                Assert(details != null && details.ReadOnly && !details.Visible, "Technical details must be read-only and hidden initially.");
                Assert(primary.Enabled, "The primary action must be ready after a folder is selected.");
                foreach (Control control in controls)
                {
                    TextBox textBox = control as TextBox;
                    Assert(textBox == null || textBox.ReadOnly, "The simple interface must not require typing a path.");
                }

                Size[] sizes = new Size[] { new Size(650, 510), new Size(500, 460), new Size(900, 740) };
                foreach (Size size in sizes)
                {
                    SetSize(form, size);
                    VerifyLayout(form, primary, status, name + " " + size);
                    VerifyEnglishAndPrivacy(form);
                }

                MethodInfo showFailure = formType.GetMethod("ShowFailure", PrivateInstance);
                Assert(showFailure != null, "The error display handler is missing.");
                Exception[] errors = new Exception[]
                {
                    new InvalidOperationException("This Discord version has an unsupported Electron fuse wire."),
                    new UnauthorizedAccessException("Access to the selected application folder was denied."),
                    new DirectoryNotFoundException("The required application files are missing."),
                    new InvalidOperationException("Please right-click Discord in the Windows tray and choose Quit Discord, then try again."),
                    new InvalidOperationException("The application changed during setup. Your existing files have been preserved.")
                };
                foreach (Exception error in errors)
                {
                    showFailure.Invoke(form, new object[] { error });
                    Assert(primary.Text == "Try again", "A failed setup must offer a retry.");
                    Assert(!String.IsNullOrWhiteSpace(status.Text), "A failed setup needs a visible explanation.");
                    foreach (Size size in sizes)
                    {
                        SetSize(form, size);
                        VerifyLayout(form, primary, status, name + " error " + size);
                        VerifyEnglishAndPrivacy(form);
                    }
                }

                selectedRoot.SetValue(form, exampleRoot, null);
                details.Text = "Version 0.5.0. Local, reversible patches. No account or password is needed.\r\n"
                    + "Resource savings still need measurement. No data is uploaded by this tool.";
                SetSize(form, new Size(650, 510));
                SaveScreenshot(form, args[1]);

                ToggleDetails(detailLink);
                Settle(form);
                Assert(details.Visible && detailLink.Text == "Hide details", "The Details link must expand technical information.");
                foreach (Size size in new Size[] { new Size(500, 650), new Size(650, 650), new Size(900, 740) })
                {
                    SetSize(form, size);
                    VerifyLayout(form, primary, status, name + " expanded " + size);
                    Assert(details.Height >= 48, "Expanded details must have room for readable text.");
                }

                SetSize(form, new Size(650, 650));
                SaveScreenshot(form, SiblingScreenshot(args[1], "-details"));
                ToggleDetails(detailLink);
                Settle(form);
                Assert(!details.Visible && detailLink.Text == "Details", "The Details link must collapse technical information.");
                form.Scale(new SizeF(1.5f, 1.5f));
                SetSize(form, new Size(975, 765));
                VerifyLayout(form, primary, status, name + " 1.5x control scaling simulation");
                VerifyEnglishAndPrivacy(form);
            }

            VerifyStop(formType, product);

            Console.WriteLine(name + " GUI: one action, Stop and safe window close, English text, compact and expanded layouts, 1.5x layout simulation; no client files changed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void VerifyStop(Type formType, object product)
    {
        using (Form form = (Form)Activator.CreateInstance(formType, new object[] { product, false }))
        {
            string missing = Path.Combine(Path.GetTempPath(), "LowConsuming-Missing-" + Guid.NewGuid().ToString("N"));
            formType.GetProperty("SelectedRoot", PrivateInstance).SetValue(form, missing, null);
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-32000, -32000);
            form.Show();
            Settle(form);
            Button primary = FindSingleButton(Descendants(form));
            var source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            formType.GetField("operationCancellation", PrivateInstance).SetValue(form, source);
            formType.GetMethod("SetBusy", PrivateInstance).Invoke(form, new object[] { true });
            primary.Text = "Stop";
            Assert(primary.Enabled && primary.Text == "Stop", "The running operation must expose an enabled Stop action.");
            Assert(primary.AccessibleName == "Stop the current operation", "The running action needs an accessible Stop name.");
            primary.PerformClick();
            Assert(token.IsCancellationRequested, "Stop must signal the operation's cancellation token.");
            Assert(primary.Text == "Stopping safely..." && !primary.Enabled, "The tool must acknowledge the Stop request.");
            form.Close();
            Assert(!form.IsDisposed, "The X button must wait until the worker reaches a safe boundary.");
            Assert((bool)formType.GetField("closeWhenFinished", PrivateInstance).GetValue(form), "The X button must request close after the worker stops.");
            formType.GetMethod("Complete", PrivateInstance).Invoke(form,
                new object[] { null, new RunWorkerCompletedEventArgs(null, null, true) });
            Assert(form.IsDisposed, "The X button must close the tool only after cancellation completion has been handled.");
        }
    }

    private static void SetSize(Form form, Size size)
    {
        form.Size = size;
        Settle(form);
    }

    private static void Settle(Form form)
    {
        form.PerformLayout();
        foreach (Control control in Descendants(form))
        {
            control.PerformLayout();
        }

        Application.DoEvents();
        form.PerformLayout();
        Application.DoEvents();
    }

    private static void VerifyLayout(Form form, Button primary, Control status, string context)
    {
        Assert(primary.Visible && status.Visible, "The action and status must be visible: " + context);
        bool footer = false;
        foreach (Control control in Descendants(form))
        {
            if (!control.Visible)
            {
                continue;
            }

            Assert(control.Width > 0 && control.Height > 0, "A visible control has no room: " + control.GetType().Name + " " + context);
            Rectangle available = control.Parent.ClientRectangle;
            Assert(available.Contains(control.Bounds), "A control overflows its container: " + control.GetType().Name + " '" + control.Text + "' " + control.Bounds + " inside " + available + " " + context);
            Label label = control as Label;
            if (label != null && label.Text.Length > 0)
            {
                Size preferred = label.GetPreferredSize(new Size(label.ClientSize.Width, 0));
                Assert(preferred.Height <= label.ClientSize.Height, "A label clips its text: '" + label.Text + "' " + context);
                if (label.Text == "Experimental · Resource savings are not verified.")
                {
                    footer = true;
                }
            }

            ScrollableControl scrollable = control as ScrollableControl;
            Assert(scrollable == null || !scrollable.HorizontalScroll.Visible, "The simple interface must not scroll horizontally: " + context);
        }

        Size buttonText = TextRenderer.MeasureText(primary.Text, primary.Font);
        Assert(buttonText.Width + 16 <= primary.ClientSize.Width && buttonText.Height + 4 <= primary.ClientSize.Height, "The primary action clips its text: " + context);
        Assert(footer, "The experimental status must stay visible: " + context);
    }

    private static void VerifyEnglishAndPrivacy(Form form)
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] oldLabels = new string[] { "Modificador", "Carpeta a", "Elegir carpeta", "Analizar archivos", "Aplicar parches", "Restaurar originales", "Abrir fuentes", "No se completó", "Cerrá ", "Primero analizá" };
        foreach (Control control in Descendants(form))
        {
            foreach (string oldLabel in oldLabels)
            {
                Assert(control.Text.IndexOf(oldLabel, StringComparison.OrdinalIgnoreCase) < 0, "A legacy Spanish message is still visible.");
            }

            Assert(String.IsNullOrEmpty(profile) || control.Text.IndexOf(profile, StringComparison.OrdinalIgnoreCase) < 0, "A screenshot would contain the real user profile path.");
        }
    }

    private static Button FindSingleButton(List<Control> controls)
    {
        Button result = null;
        foreach (Control control in controls)
        {
            Button candidate = control as Button;
            if (candidate != null)
            {
                Assert(result == null, "The simple interface must have only one action button.");
                result = candidate;
            }
        }

        Assert(result != null, "The primary action button is missing.");
        return result;
    }

    private static Control FindAccessible(List<Control> controls, string name)
    {
        foreach (Control control in controls)
        {
            if (control.AccessibleName == name)
            {
                return control;
            }
        }

        throw new Exception("A labelled interface control is missing: " + name);
    }

    private static LinkLabel FindLink(List<Control> controls, string text)
    {
        foreach (Control control in controls)
        {
            LinkLabel link = control as LinkLabel;
            if (link != null && link.Text == text)
            {
                return link;
            }
        }

        throw new Exception("A navigation link is missing: " + text);
    }

    private static void ToggleDetails(LinkLabel link)
    {
        MethodInfo click = typeof(LinkLabel).GetMethod("OnLinkClicked", PrivateInstance);
        Assert(click != null && link.Links.Count > 0, "The Details link cannot be activated.");
        click.Invoke(link, new object[] { new LinkLabelLinkClickedEventArgs(link.Links[0]) });
    }

    private static List<Control> Descendants(Control parent)
    {
        List<Control> result = new List<Control>();
        foreach (Control child in parent.Controls)
        {
            result.Add(child);
            result.AddRange(Descendants(child));
        }

        return result;
    }

    private static string SiblingScreenshot(string path, string suffix)
    {
        return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + suffix + ".png");
    }

    private static void SaveScreenshot(Form form, string path)
    {
        VerifyEnglishAndPrivacy(form);
        using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
