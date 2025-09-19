using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using ResXQuickAdd.Services;
using ResXQuickAdd.Utilities;

namespace ResXQuickAdd.Dialogs
{
    public partial class AddResourceDialog : Window, INotifyPropertyChanged
    {
        private string _resourceKey;
        private string _firstLanguageLabel;
        private string _secondLanguageLabel;
        private string _firstLanguageValue;
        private string _secondLanguageValue;
        private string _statusMessage;
        private bool _hasError;

        public AddResourceDialog(LanguageConfiguration languageConfig, string resourceKey)
        {
            InitializeComponent();
            DataContext = this;
            
            if (languageConfig == null)
                throw new ArgumentNullException(nameof(languageConfig));
            
            if (string.IsNullOrWhiteSpace(resourceKey))
                throw new ArgumentException("Resource key cannot be empty", nameof(resourceKey));

            LanguageConfiguration = languageConfig;
            ResourceKey = resourceKey;
            
            InitializeLabels();
            
            FirstLanguageTextBox.Focus();
        }

        public LanguageConfiguration LanguageConfiguration { get; }

        public string ResourceKey
        {
            get => _resourceKey;
            set
            {
                _resourceKey = value;
                OnPropertyChanged();
            }
        }

        public string FirstLanguageLabel
        {
            get => _firstLanguageLabel;
            set
            {
                _firstLanguageLabel = value;
                OnPropertyChanged();
            }
        }

        public string SecondLanguageLabel
        {
            get => _secondLanguageLabel;
            set
            {
                _secondLanguageLabel = value;
                OnPropertyChanged();
            }
        }

        public string FirstLanguageValue
        {
            get => _firstLanguageValue;
            set
            {
                _firstLanguageValue = value;
                OnPropertyChanged();
                ValidateInput();
            }
        }

        public string SecondLanguageValue
        {
            get => _secondLanguageValue;
            set
            {
                _secondLanguageValue = value;
                OnPropertyChanged();
                ValidateInput();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool HasError
        {
            get => _hasError;
            set
            {
                _hasError = value;
                OnPropertyChanged();
            }
        }

        public string PrimaryTranslation { get; private set; }
        public string SecondaryTranslation { get; private set; }
        public bool HasSecondaryLanguage => LanguageConfiguration.HasMultipleLanguages;

        private void InitializeLabels()
        {
            var languageService = new LanguageDetectionService(null);
            
            FirstLanguageLabel = languageService.GetTranslationInputLabel(LanguageConfiguration.PrimaryLanguage);
            SecondLanguageLabel = languageService.GetTranslationInputLabel(LanguageConfiguration.SecondaryLanguage);
            
            if (!LanguageConfiguration.HasMultipleLanguages)
            {
                SecondLanguageLabel += " (will be saved as comment)";
            }
        }

        private void ValidateInput()
        {
            HasError = false;
            StatusMessage = string.Empty;

            if (!ResXHelper.IsValidResourceKey(ResourceKey))
            {
                SetError("Invalid resource key format. Use only letters, numbers, and underscores.");
                return;
            }

            if (string.IsNullOrWhiteSpace(FirstLanguageValue))
            {
                SetError($"{LanguageConfiguration.PrimaryLanguageDisplayName} translation cannot be empty.");
                return;
            }

            if (!LanguageConfiguration.HasMultipleLanguages && string.IsNullOrWhiteSpace(SecondLanguageValue))
            {
                SetError($"{LanguageConfiguration.SecondaryLanguageDisplayName} translation cannot be empty when using single file mode.");
                return;
            }

            OkButton.IsEnabled = true;
        }

        private void SetError(string message)
        {
            StatusMessage = message;
            HasError = true;
            OkButton.IsEnabled = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ValidateInput();
            
            if (HasError)
                return;

            try
            {
                PrimaryTranslation = FirstLanguageValue?.Trim();
                SecondaryTranslation = SecondLanguageValue?.Trim();
                
                if (string.IsNullOrEmpty(PrimaryTranslation))
                {
                    SetError($"{LanguageConfiguration.PrimaryLanguageDisplayName} translation cannot be empty.");
                    return;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                SetError($"Error preparing translations: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public static readonly BooleanToVisibilityConverter Instance = new BooleanToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (value is bool boolean && boolean) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
    }
}