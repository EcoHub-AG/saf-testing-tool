using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using StandardApiFrameworkTool.Helpers;
using StandardApiFrameworkTool.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace StandardApiFrameworkTool.ViewModels
{
    public class ReceiveDataViewModel : ViewModelBase
    {
        private readonly AppDbContext _dbContext;

        public ReceiveDataViewModel()
        {
            ThreadList = new ObservableCollection<ThreadItem>
            {
                new ThreadItem { Title = "Event 1", Timestamp = "2024-12-10 14:00", Payload = "{ \"event\": \"Event 1 Payload\" }" },
                new ThreadItem { Title = "Event 2", Timestamp = "2024-12-10 14:05", Payload = "{ \"event\": \"Event 2 Payload\" }" },
                new ThreadItem { Title = "Event 3", Timestamp = "2024-12-10 14:10", Payload = "{ \"event\": \"Event 3 Payload\" }" }
            };

            PrivateKeys = new ObservableCollection<PrivateKey>();
            GenerateKeyPairCommand = new RelayCommand(GenerateKeyPair);
            ActivateKeyCommand = new RelayCommand(ActivateKey);

            _dbContext = new AppDbContext();

            PrivateKeys = new ObservableCollection<PrivateKey>(_dbContext.PrivateKeys.Select(pk => new PrivateKey
            {
                Id = pk.Id,
                Version = pk.Version,
                CreatedAt = pk.CreatedAt,
                IsActive = pk.IsActive,
            }));
        }

        #region Receive event
        private ObservableCollection<ThreadItem> _threadList;
        public ObservableCollection<ThreadItem> ThreadList
        {
            get => _threadList;
            set
            {
                _threadList = value;
                OnPropertyChanged(nameof(ThreadList));
            }
        }

        private ThreadItem _selectedThread;
        public ThreadItem SelectedThread
        {
            get => _selectedThread;
            set
            {
                _selectedThread = value;
                SelectedThreadPayload = value?.Payload;
                OnPropertyChanged(nameof(SelectedThread));
            }
        }

        private string _selectedThreadPayload;
        public string SelectedThreadPayload
        {
            get => _selectedThreadPayload;
            set
            {
                _selectedThreadPayload = value;
                OnPropertyChanged(nameof(SelectedThreadPayload));
            }
        }

        #endregion

        #region Encryption
        public ObservableCollection<PrivateKey> PrivateKeys { get; set; }
        public string GeneratedPublicKey { get; set; }
        public string GeneratedPrivateKey { get; set; }

        public ICommand GenerateKeyPairCommand { get; }
        public ICommand ActivateKeyCommand { get; }

        private void GenerateKeyPair()
        {
            
            (GeneratedPublicKey, GeneratedPrivateKey) = RSAKeyHelper.GenerateKeyPair();
            OnPropertyChanged(nameof(GeneratedPublicKey));
            OnPropertyChanged(nameof(GeneratedPrivateKey));
        }


        private void ActivateKey()
        {
            if (string.IsNullOrWhiteSpace(GeneratedPublicKey) || string.IsNullOrWhiteSpace(GeneratedPrivateKey))
            {
                MessageBox.Show("Empty public key or private key. Please generate a key pair first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // validate 
            var isValid = RSAKeyHelper.ValidateKeyPair(GeneratedPublicKey, GeneratedPrivateKey);

            if (!isValid)
            {
                MessageBox.Show("Validation failed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // upload to EcoHub


            _dbContext.PrivateKeys.Add(new PrivateKey
            {
                Version = "1.0",
                Key = GeneratedPrivateKey,
                IsActive = DateTime.Now.Ticks % 2 == 0,
                CreatedAt = DateTime.Now,
            });

            _dbContext.SaveChanges();

            // Call service to upload and activate the key (implement service logic)
            MessageBox.Show("Public key activated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        #endregion
    }
}
