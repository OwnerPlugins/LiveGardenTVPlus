using System;
using System.Windows;

namespace LiveGardenTVPlus
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            /* MessageBox.Show("Program.Main called!");*/
            var app = new App();
            app.Run(new MainWindow());
        }
    }
}