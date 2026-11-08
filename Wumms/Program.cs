using System;
using System.Threading;
using System.Windows.Forms;

namespace Wumms
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Einmalige Einrichtung (läuft mit Admin-Rechten, ohne Fenster)
            if (args.Length >= 2 && args[0] == "--setup")
                return Apo.DoSetup(args[1]);

            using var mutex = new Mutex(true, "Wumms_ErlinRamaj_EinzigeInstanz", out bool firstInstance);
            if (!firstInstance)
            {
                MessageBox.Show("Wumms läuft schon. Du findest es unten rechts neben der Uhr.", "Wumms",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }
}
