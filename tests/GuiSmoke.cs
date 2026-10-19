using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

public static class GuiSmoke
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Assembly assembly = Assembly.LoadFile(Path.GetFullPath(args[0]));
            Type product = assembly.GetType("Tawop.Product", true);
            string name = Path.GetFileNameWithoutExtension(args[0]).Replace("Tawop", "");
            using (Form form = (Form)Activator.CreateInstance(assembly.GetType("Tawop.ModForm", true), new object[] { Enum.Parse(product, name) }))
            {
                TextBox selectedPath = FindPathBox(form);
                if (selectedPath == null)
                {
                    throw new Exception("Missing labelled installation field.");
                }
                selectedPath.Text = name == "Discord"
                    ? @"C:\Users\Usuario\AppData\Local\Discord\app-VERSION"
                    : name == "Steam" ? @"C:\Program Files (x86)\Steam" : @"C:\src\chromium\src";
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-32000, -32000);
                form.Show();
                Application.DoEvents();
                foreach (Size size in new Size[]
                {
                    new Size(900, 730),
                    new Size(740, 640),
                    new Size(580, 450)
                }

                )
                {
                    form.Size = size;
                    form.PerformLayout();
                    Application.DoEvents();
                    TableLayoutPanel frame = (TableLayoutPanel)form.Controls[0];
                    frame.PerformLayout();
                    Panel content = (Panel)frame.GetControlFromPosition(0, 0);
                    content.PerformLayout();
                    content.Controls[0].PerformLayout();
                    Control status = frame.GetControlFromPosition(0, 1);
                    if (!status.Visible || !content.Visible || content.Controls[0].Height < 100 || status.Height < 38 || status.Bottom > frame.ClientSize.Height || content.HorizontalScroll.Visible)
                    {
                        throw new Exception("Layout overflow: " + name + " " + size);
                    }
                }

                form.Size = new Size(900, 730);
                form.PerformLayout();
                Application.DoEvents();
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                }
            }

            Console.WriteLine(name + " GUI rendered off-screen without applying patches.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static TextBox FindPathBox(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            TextBox textBox = child as TextBox;
            if (textBox != null && textBox.AccessibleName == "Carpeta a modificar")
            {
                return textBox;
            }
            TextBox nested = FindPathBox(child);
            if (nested != null)
            {
                return nested;
            }
        }
        return null;
    }
}
