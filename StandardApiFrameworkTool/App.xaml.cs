using System.Configuration;
using System.Data;
using System.Windows;

namespace StandardApiFrameworkTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize the database
            using (var context = new AppDbContext())
            {
                DBInitializer.Initialize(context);
            }
        }
    }

}
