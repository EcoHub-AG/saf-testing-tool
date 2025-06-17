using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using StandardApiFrameworkTool.Helpers;
using StandardApiFrameworkTool.Services;

namespace StandardApiFrameworkTool.ViewModels
{
    public class GeneralSettingsViewModel : INotifyPropertyChanged
    {
        private readonly AppDbContext _context;

        #region props
        private string _selectedEnvironment;
        private string _csmUrl;
        private string _servicesApiUrl;
        private string _idpNumber;
        private string _licenseKey;
        private string _password;
        private string _iak;
        private string _outTopic;

        public ObservableCollection<string> Environments { get; set; }

        public string SelectedEnvironment
        {
            get => _selectedEnvironment;
            set
            {
                _selectedEnvironment = value;
                OnPropertyChanged(nameof(SelectedEnvironment));
                UpdateEnvironmentDetails();
            }
        }

        public string CsmUrl
        {
            get => _csmUrl;
            set { _csmUrl = value; OnPropertyChanged(nameof(CsmUrl)); }
        }

        public string ServicesApiUrl
        {
            get => _servicesApiUrl;
            set { _servicesApiUrl = value; OnPropertyChanged(nameof(ServicesApiUrl)); }
        }

        public string IdpNumber
        {
            get => _idpNumber;
            set { _idpNumber = value; OnPropertyChanged(nameof(IdpNumber)); }
        }

        public string LicenseKey
        {
            get => _licenseKey;
            set { _licenseKey = value; OnPropertyChanged(nameof(LicenseKey)); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(nameof(Password)); }
        }

        public string Iak
        {
            get => _iak;
            set { _iak = value; OnPropertyChanged(nameof(Iak)); }
        }

        public string OutTopic
        {
            get => _outTopic;
            set { _outTopic = value; OnPropertyChanged(nameof(OutTopic)); }
        }

        #endregion

        public ICommand SaveCommand { get; }

        public GeneralSettingsViewModel()
        {
            _context = new AppDbContext();

            // Initialize the SaveCommand
            SaveCommand = new RelayCommand(async () => await SaveSettingsAsync());

            // Load the environments and settings
            LoadEnvironmentsAsync();
            LoadSettingsAsync();
        }

        private async void LoadEnvironmentsAsync()
        {
            var environments = await _context.EnvironmentSettings
                                             .Select(e => e.EnvironmentName)
                                             .ToListAsync();

            Environments = new ObservableCollection<string>(environments);
            OnPropertyChanged(nameof(Environments));
        }

        private async void LoadSettingsAsync()
        {
            var profile = await _context.Profiles.FirstOrDefaultAsync();
            if (profile != null)
            {
                SelectedEnvironment = profile.SelectedEnvironment;
                IdpNumber = profile.IdpNumber;
                LicenseKey = profile.LicenseKey;
                Password = profile.Password;
                Iak = profile.Iak;
                OutTopic = profile.OutTopic;
            }
        }

        private async Task SaveSettingsAsync()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = true;
            });

            var environment = _context.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == SelectedEnvironment);
            if (environment != null)
            {
                var response = await TechUserService.EnrolTechUser(Iak, Password, IdpNumber, LicenseKey, environment.ServicesApiUrl);

                if (response != null)
                {
                    var allTechUsers = _context.TechUsers.ToList();
                    foreach (var user in allTechUsers)
                    {
                        _context.TechUsers.Remove(user);
                    }
                    await _context.SaveChangesAsync();

                    _context.TechUsers.Add(new TechUser
                    {
                        OAuthClientId = response.oAuth2.clientId,
                        OAuthClientPassword = response.oAuth2.clientSecret,
                        TechUserCert = response.techUserCert
                    });
                    await _context.SaveChangesAsync();

                    var profile = await _context.Profiles.FirstOrDefaultAsync();
                    if (profile == null)
                    {
                        profile = new Profile();
                        _context.Profiles.Add(profile);
                    }

                    profile.SelectedEnvironment = SelectedEnvironment;
                    profile.IdpNumber = IdpNumber;
                    profile.LicenseKey = LicenseKey;
                    profile.Password = Password;
                    profile.Iak = Iak;
                    profile.OutTopic = OutTopic;

                    await _context.SaveChangesAsync();

                    MessageBox.Show("Settings saved and Tech User enrolled successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to enroll Tech User. Check your settings and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Environment not found. Please select a valid environment.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            LoadSettingsAsync();

            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = false;
            });
        }

        private void UpdateEnvironmentDetails()
        {
            var environment = _context.EnvironmentSettings
                                      .FirstOrDefault(e => e.EnvironmentName == SelectedEnvironment);
            if (environment != null)
            {
                CsmUrl = environment.CsmHost;
                ServicesApiUrl = environment.ServicesApiUrl;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
