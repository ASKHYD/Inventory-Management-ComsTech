using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SOFT2027.Presentation.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        private string? _errorMessage;
        private string? _statusAnnouncement;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    OnPropertyChanged(nameof(HasErrors));
                }
            }
        }

        public string? StatusAnnouncement
        {
            get => _statusAnnouncement;
            set => SetProperty(ref _statusAnnouncement, value);
        }

        public bool HasErrors => !string.IsNullOrWhiteSpace(ErrorMessage);

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual bool SetProperty<T>(ref T storage, T value, Action onChanged, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            onChanged?.Invoke();
            return true;
        }

        public void SetError(string message)
        {
            var translated = SOFT2027.UI.Services.ExceptionTranslator.Translate(message);
            ErrorMessage = translated;
            StatusAnnouncement = $"Error: {translated}";
        }

        public void ClearErrors()
        {
            ErrorMessage = null;
        }

        public void Announce(string message)
        {
            StatusAnnouncement = message;
        }
    }
}
