using ICSharpCode.AvalonEdit;
using StandardApiFrameworkTool.Helpers;
using StandardApiFrameworkTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StandardApiFrameworkTool.Views
{
    /// <summary>
    /// Interaction logic for ReceiveDataView.xaml
    /// </summary>
    public partial class ReceiveDataView : UserControl
    {
        public ReceiveDataView()
        {
            InitializeComponent();
            this.DataContext = new ReceiveDataViewModel();
        }

        private void Payload_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = (SendDataViewModel)this.DataContext;
            if (sender is TextEditor editor && string.IsNullOrEmpty(vm.PayloadContent))
            {
                TextEditorHelper.SetBoundText(editor, string.Empty);
            }
        }

        private void PayloadEditor_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = (ReceiveDataViewModel)this.DataContext;
            if (sender is TextEditor editor && string.IsNullOrEmpty(vm.SelectedThreadPayload))
            {
                TextEditorHelper.SetBoundText(editor, string.Empty);
            }
        }
    }
}
