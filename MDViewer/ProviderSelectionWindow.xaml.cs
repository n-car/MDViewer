using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace it.carpanese.utilities.MDViewer
{
    /// <summary>
    /// Dialog per la selezione consapevole del provider Markdown.
    /// </summary>
    public partial class ProviderSelectionWindow : Window
    {
        private sealed class ProviderOption
        {
            public MarkdownProvider Provider { get; set; }
            public string DisplayName { get; set; }
            public string ShortDescription { get; set; }
            public string Description { get; set; }
        }

        private readonly ProviderOption[] _providerOptions;

        public MarkdownProvider SelectedProvider { get; private set; }

        public ProviderSelectionWindow(MarkdownProvider currentProvider)
        {
            InitializeComponent();

            _providerOptions = Enum.GetValues(typeof(MarkdownProvider))
                .Cast<MarkdownProvider>()
                .Select(provider => new ProviderOption
                {
                    Provider = provider,
                    DisplayName = MarkdownProviderInfo.GetDisplayName(provider),
                    ShortDescription = MarkdownProviderInfo.GetShortDescription(provider),
                    Description = MarkdownProviderInfo.GetDescription(provider)
                })
                .ToArray();

            ProvidersList.ItemsSource = _providerOptions;

            var initial = _providerOptions.FirstOrDefault(option => option.Provider == currentProvider)
                ?? _providerOptions.FirstOrDefault();

            if (initial != null)
            {
                ProvidersList.SelectedItem = initial;
                SelectedProvider = initial.Provider;
            }
        }

        private void ProvidersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var option = ProvidersList.SelectedItem as ProviderOption;
            if (option == null)
                return;

            SelectedProvider = option.Provider;
            TxtProviderName.Text = option.DisplayName;
            TxtProviderDescription.Text = option.Description;
        }

        private void BtnUseProvider_Click(object sender, RoutedEventArgs e)
        {
            if (ProvidersList.SelectedItem == null)
            {
                MessageBox.Show(
                    Localizer.Get("ProviderDialogSelectProviderMessage"),
                    Localizer.Get("ProviderDialogSelectProviderTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
