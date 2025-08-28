using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows;

namespace StandardApiFrameworkTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize the database
            using (var context = new AppDbContext())
            {
                DBInitializer.Initialize(context);
            }

            // open a console window
            // AllocConsole();
            // Console.WriteLine("Console attached!");
        }
    }

}
