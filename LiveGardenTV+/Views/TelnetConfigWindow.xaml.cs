using System.Windows;
using LiveGardenTVPlus.Services;


namespace LiveGardenTVPlus.Views
{
    public partial class TelnetConfigWindow : Window
    {
        public TelnetConfigWindow()
        {
            InitializeComponent();
            ApplyLanguage();
            LoadSettings();
            LanguageManager.LanguageChanged += ApplyLanguage;

        }

        private void ApplyLanguage()
        {
            Title = "Telnet Configuration";
            HostLabel.Text = "Host:";
            PortLabel.Text = "Port:";
            UserLabel.Text = "Username:";
            PassLabel.Text = "Password:";
            SaveBtn.Content = "Save";
            CancelBtn.Content = "Cancel";
        }

        private void LoadSettings()
        {
            var prefs = UserPreferences.Load();
            HostBox.Text = prefs.TelnetHost;
            PortBox.Text = prefs.TelnetPort.ToString();
            UserBox.Text = prefs.TelnetUser;
            PassBox.Password = prefs.TelnetPass;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var prefs = UserPreferences.Load();
            prefs.TelnetHost = HostBox.Text;
            int.TryParse(PortBox.Text, out int port);
            prefs.TelnetPort = port;
            prefs.TelnetUser = UserBox.Text;
            prefs.TelnetPass = PassBox.Password;
            prefs.Save();
            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}