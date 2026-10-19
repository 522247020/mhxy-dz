using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MeditationMini
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            // 先声明 DPI 感知，Windows 就不会把画面整体拉伸（字才会清楚）。
            try
            {
                SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException)
            {
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // 全局深色菜单渲染器：子菜单（二级菜单）也会用它，否则会是浅底浅字。
            ToolStripManager.Renderer = new DarkMenuRenderer();

            string directory = AppDomain.CurrentDomain.BaseDirectory;
            OverlaySettings settings = OverlaySettings.Load(Path.Combine(directory, "meditation.ini"));
            using (OverlayForm form = new OverlayForm(settings))
            {
                Application.Run(form);
            }
        }
    }
}



