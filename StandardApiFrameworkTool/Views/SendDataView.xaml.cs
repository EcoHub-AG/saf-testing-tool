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
    /// Interaction logic for SendDataView.xaml
    /// </summary>
    public partial class SendDataView : UserControl
    {
        public SendDataView()
        {
            InitializeComponent();
            this.DataContext = new SendDataViewModel();
        }

        private void InputEditor_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = (SendDataViewModel)this.DataContext;
            if (sender is TextEditor editor && string.IsNullOrEmpty(vm.InputContent))
            {
                TextEditorHelper.SetBoundText(editor, string.Empty);
            }
        }

        private void EncryptedContentEditor_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = (SendDataViewModel)this.DataContext;
            if (sender is TextEditor editor && string.IsNullOrEmpty(vm.EncryptedContent))
            {
                TextEditorHelper.SetBoundText(editor, string.Empty);
            }
        }

        private void EncryptedAESKeyEditor_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = (SendDataViewModel)this.DataContext;
            if (sender is TextEditor editor && string.IsNullOrEmpty(vm.EncryptedAESKey))
            {
                TextEditorHelper.SetBoundText(editor, string.Empty);
            }
        }

        private void Payload_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = (SendDataViewModel)this.DataContext;
            if (sender is TextEditor editor && string.IsNullOrEmpty(vm.PayloadContent))
            {
                TextEditorHelper.SetBoundText(editor, string.Empty);
            }
        }
    }
}
