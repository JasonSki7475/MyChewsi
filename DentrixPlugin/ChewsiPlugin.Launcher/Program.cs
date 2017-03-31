using System;
using System.Threading;
using System.Windows.Forms;

namespace ChewsiPlugin.Launcher
{
    internal static class Program
    {
        private static Mutex _mutex;
        private const string MutexName = "553C8D4E-C505-41A9-B9F4-7404036C3D77";

        [STAThread]
        public static void Main(string[] args)
        {
            // Check if another instance of application is already running
            bool created;
            _mutex = new Mutex(true, MutexName, out created);
            if (created)
            {
                Application.Run(new App());
            }
            else
            {
                MessageBox.Show("Another instance of application is already running", "Chewsi Launcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}