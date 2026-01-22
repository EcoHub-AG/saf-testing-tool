using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StandardApiFrameworkTool.Views
{
    public partial class GeneralSettingsView : UserControl
    {
        public GeneralSettingsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
