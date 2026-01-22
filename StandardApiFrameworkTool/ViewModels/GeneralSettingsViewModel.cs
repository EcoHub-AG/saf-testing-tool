using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
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
        private string _orgId;

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

        public string OrgId
        {
            get => _orgId;
            set { _orgId = value; OnPropertyChanged(nameof(OrgId)); }
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
                OrgId = profile.OrgId;
            }
        }

        private async Task SaveSettingsAsync()
        {
            UiServices.SetIsProcessing(true);

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
                        TechUserCert = response.techUserCert,
                        OpenIdConfigurationEndpoint = response.oAuth2.openIdConfigurationEndpoint,
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
                    profile.OrgId = OrgId;

                    await _context.SaveChangesAsync();

                    var privateKeys = await _context.PrivateKeys.ToListAsync();
                    if (privateKeys.Count > 0)
                    {
                        _context.PrivateKeys.RemoveRange(privateKeys);
                        await _context.SaveChangesAsync();
                    }

                    var sigKeys = await _context.SignatureKeys.ToListAsync();
                    if (sigKeys.Count > 0)
                    {
                        _context.SignatureKeys.RemoveRange(sigKeys);
                        await _context.SaveChangesAsync();
                    }

                    UiServices.ShowInfo("Settings saved and Tech User enrolled successfully.", "Success");
                }
                else
                {
                    UiServices.ShowError("Failed to enroll Tech User. Check your settings and try again.", "Error");
                }
            }
            else
            {
                UiServices.ShowError("Environment not found. Please select a valid environment.", "Error");
            }

            LoadSettingsAsync();

            UiServices.SetIsProcessing(false);
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
