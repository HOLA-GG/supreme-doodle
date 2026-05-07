using System;
using System.ComponentModel;

namespace SecurityMonitor.Commander.Desktop.Models
{
    public class AgentStatusModel : INotifyPropertyChanged
    {
        private string _machineName = string.Empty;
        public string MachineName 
        { 
            get => _machineName; 
            set { _machineName = value; OnPropertyChanged(nameof(MachineName)); } 
        }

        private string _ipAddress = string.Empty;
        public string IpAddress 
        { 
            get => _ipAddress; 
            set { _ipAddress = value; OnPropertyChanged(nameof(IpAddress)); } 
        }

        private string _connectionInfo = string.Empty;
        public string ConnectionInfo 
        { 
            get => _connectionInfo; 
            set { _connectionInfo = value; OnPropertyChanged(nameof(ConnectionInfo)); } 
        }

        private DateTime _lastSeen;
        public DateTime LastSeen 
        { 
            get => _lastSeen; 
            set 
            { 
                _lastSeen = value; 
                OnPropertyChanged(nameof(LastSeen)); 
                OnPropertyChanged(nameof(LastSeenText)); 
            } 
        }

        public string LastSeenText => LastSeen.ToString("HH:mm:ss");

        private string _status = string.Empty;
        public string Status 
        { 
            get => _status; 
            set { _status = value; OnPropertyChanged(nameof(Status)); } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
