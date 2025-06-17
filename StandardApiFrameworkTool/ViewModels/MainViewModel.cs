using StandardApiFrameworkTool.ViewModels;
using StandardApiFrameworkTool.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StandardApiFrameworkTool.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public object CurrentHomeView { get; set; } = new HomeView();
        public object CurrentGeneralSettingsView { get; set; } = new GeneralSettingsView();
        public object CurrentSendDataView { get; set; } = new SendDataView();
        public object CurrentReceiveDataView { get; set; } = new ReceiveDataView();


        #region Props

        private string _statusMessage;
        private bool _isProcessing;

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                OnPropertyChanged(nameof(IsProcessing));
            }
        }

        #endregion

        public MainViewModel()
        {
            StatusMessage = "Ready";
            IsProcessing = false;
        }
    }

}
