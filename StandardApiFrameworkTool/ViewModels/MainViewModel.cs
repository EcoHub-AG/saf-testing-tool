namespace StandardApiFrameworkTool.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public HomeViewModel Home { get; } = new HomeViewModel();
        public GeneralSettingsViewModel GeneralSettings { get; } = new GeneralSettingsViewModel();
        public SendDataViewModel SendData { get; } = new SendDataViewModel();
        public ReceiveDataViewModel ReceiveData { get; } = new ReceiveDataViewModel();

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
