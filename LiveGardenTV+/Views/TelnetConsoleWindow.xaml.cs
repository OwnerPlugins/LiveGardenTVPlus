using LiveGardenTVPlus.Services;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LiveGardenTVPlus.Views
{
    public partial class TelnetConsoleWindow : Window
    {
        private TelnetClient telnet;
        private string commandsXmlFile;
        private string commandsJsonFile;
        private List<CommandPreset> presets = new List<CommandPreset>();

        public TelnetConsoleWindow()
        {
            try
            {
                InitializeComponent();
                LoadTelnetSettings();
                ApplyLanguage();
                LanguageManager.LanguageChanged += ApplyLanguage;

                Debug.WriteLine("TelnetConsoleWindow constructor - after LoadTelnetSettings");

                telnet = new TelnetClient();
                telnet.DataReceived += OnDataReceived;
                telnet.ErrorOccurred += OnError;
                telnet.ConnectionStateChanged += OnConnectionStateChanged;

                commandsXmlFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "telnet_commands.xml");
                commandsJsonFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "telnet_commands.json");
                Debug.WriteLine($"XML file: {commandsXmlFile}");
                Debug.WriteLine($"JSON file: {commandsJsonFile}");
                LoadCommands();
                Closing += (s, e) => telnet.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in constructor: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ApplyLanguage()
        {
            Title = LanguageManager.GetTranslation("Telnet Commander");
            ConnectBtn.Content = LanguageManager.GetTranslation("Connect");
            DisconnectBtn.Content = LanguageManager.GetTranslation("Disconnect");
            SendBtn.Content = LanguageManager.GetTranslation("Send");
            AddCmdBtn.Content = LanguageManager.GetTranslation("Add Command");
            RemoveCmdBtn.Content = LanguageManager.GetTranslation("Remove Command");
            EditCmdBtn.Content = LanguageManager.GetTranslation("Edit Command");
            ReloadGUIBtn.Content = LanguageManager.GetTranslation("Reload GUI");
            StatusText.Text = LanguageManager.GetTranslation("Disconnected");
        }

        private void LoadTelnetSettings()
        {
            var prefs = UserPreferences.Load();
            HostBox.Text = prefs.TelnetHost;
            PortBox.Text = prefs.TelnetPort.ToString();
            UserBox.Text = prefs.TelnetUser;
            PassBox.Password = prefs.TelnetPass;
        }

        private void SaveTelnetSettings()
        {
            var prefs = UserPreferences.Load();
            prefs.TelnetHost = HostBox.Text;
            int.TryParse(PortBox.Text, out int port);
            prefs.TelnetPort = port;
            prefs.TelnetUser = UserBox.Text;
            prefs.TelnetPass = PassBox.Password;
            prefs.Save();
        }

        private void LoadCommands()
        {
            // Check JSON first
            if (File.Exists(commandsJsonFile))
            {
                Debug.WriteLine("JSON file found, loading...");
                LoadFromJson();
            }
            else if (File.Exists(commandsXmlFile))
            {
                Debug.WriteLine("XML file found, loading...");
                LoadFromXml();
            }
            else
            {
                Debug.WriteLine("No commands file found, creating defaults.");
                CreateDefaultCommands();
                SaveCommands(); // saves as XML
            }
            RefreshCommandList();
        }

        private void LoadFromJson()
        {
            try
            {
                string json = File.ReadAllText(commandsJsonFile);
                Debug.WriteLine($"JSON content length: {json.Length}");
                var categories = JsonSerializer.Deserialize<Dictionary<string, List<List<string>>>>(json);
                if (categories == null) throw new Exception("Deserialization returned null");
                presets.Clear();
                foreach (var category in categories)
                {
                    foreach (var cmdEntry in category.Value)
                    {
                        if (cmdEntry.Count >= 2)
                        {
                            presets.Add(new CommandPreset { Name = cmdEntry[1], Command = cmdEntry[0] });
                        }
                    }
                }
                Debug.WriteLine($"Loaded {presets.Count} commands from JSON");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading JSON: {ex.Message}");
                MessageBox.Show($"Error loading JSON: {ex.Message}\n{ex.StackTrace}");
                CreateDefaultCommands();
            }
        }

        private void LoadFromXml()
        {
            try
            {
                var doc = System.Xml.Linq.XDocument.Load(commandsXmlFile);
                presets = doc.Descendants("Command")
                    .Select(c => new CommandPreset
                    {
                        Name = c.Attribute("name")?.Value ?? "",
                        Command = c.Attribute("cmd")?.Value ?? ""
                    })
                    .Where(c => !string.IsNullOrEmpty(c.Name))
                    .ToList();
            }
            catch { CreateDefaultCommands(); }
        }

        private void CreateDefaultCommands()
        {
            presets = new List<CommandPreset>
            {
                new CommandPreset { Name = "Enigma2 Restart (init 4 + init 3)", Command = "init 4 && killall -9 enigma2 && init 3" },
                new CommandPreset { Name = "ReloadEnigma2 Settings (webif)", Command = "wget -qO- http://127.0.0.1/web/servicelistreload?mode=0" },
                new CommandPreset { Name = "Show Log (last 20 lines)", Command = "tail -20 /var/log/messages" },
                new CommandPreset { Name = "Take Screenshot", Command = "grab /tmp/screenshot.png" },
                new CommandPreset { Name = "Free Memory", Command = "free -h" },
                new CommandPreset { Name = "Last Enigma2 errors", Command = "grep -i 'error' /home/root/.enigma2/enigma2.log | tail -20" },
                new CommandPreset { Name = "Reboot Box", Command = "reboot" }
            };
        }

        private void SaveCommands()
        {
            try
            {
                var doc = new System.Xml.Linq.XDocument(
                    new System.Xml.Linq.XElement("Commands",
                        presets.Select(c => new System.Xml.Linq.XElement("Command",
                            new System.Xml.Linq.XAttribute("name", c.Name),
                            new System.Xml.Linq.XAttribute("cmd", c.Command)
                        ))
                    )
                );
                doc.Save(commandsXmlFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot save commands: {ex.Message}");
            }
        }

        private void RefreshCommandList() => CommandsList.ItemsSource = presets.Select(p => p.Name).ToList();

        private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("ConnectBtn_Click called");
            string host = HostBox.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Host cannot be empty.");
                return;
            }
            if (!int.TryParse(PortBox.Text.Trim(), out int port))
                port = 23;
            string user = UserBox.Text.Trim();
            string pass = PassBox.Password;

            ConnectBtn.IsEnabled = false;
            StatusText.Text = "Connecting...";
            bool ok = await telnet.ConnectAsync(host, port);
            if (ok)
            {
                await telnet.LoginAsync(user, pass);
                SaveTelnetSettings();
                Debug.WriteLine("Login successful");
            }
            else
            {
                ConnectBtn.IsEnabled = true;
                StatusText.Text = "Failed to connect";
                Debug.WriteLine("Connection failed");
            }
        }

        private void DisconnectBtn_Click(object sender, RoutedEventArgs e) => telnet.Disconnect();

        private void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(InputBox.Text))
            {
                telnet.SendCommandAsync(InputBox.Text);
                InputBox.Clear();
                InputBox.Focus();
            }
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendBtn_Click(sender, e);
                e.Handled = true;
            }
        }

        private void AddCmdBtn_Click(object sender, RoutedEventArgs e)
        {
            string name = InputBoxHelper.ShowInputBox(
                LanguageManager.GetTranslation("Command name:"),
                LanguageManager.GetTranslation("Add Command"),
                "");
            if (string.IsNullOrWhiteSpace(name)) return;

            string cmd = InputBoxHelper.ShowInputBox(
                LanguageManager.GetTranslation("Telnet command:"),
                LanguageManager.GetTranslation("Add Command"),
                "");
            if (string.IsNullOrWhiteSpace(cmd)) return;

            presets.Add(new CommandPreset { Name = name, Command = cmd });
            SaveCommands();
            RefreshCommandList();
        }

        private void RemoveCmdBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CommandsList.SelectedIndex < 0) return;
            presets.RemoveAt(CommandsList.SelectedIndex);
            SaveCommands();
            RefreshCommandList();
        }

        private void EditCmdBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CommandsList.SelectedIndex < 0) return;
            var selected = presets[CommandsList.SelectedIndex];
            string newName = InputBoxHelper.ShowInputBox(
                LanguageManager.GetTranslation("Edit command name:"),
                LanguageManager.GetTranslation("Edit Command"),
                selected.Name);

            if (string.IsNullOrWhiteSpace(newName)) return;

            string newCmd = InputBoxHelper.ShowInputBox(
                LanguageManager.GetTranslation("Edit telnet command:"),
                LanguageManager.GetTranslation("Edit Command"),
                selected.Command);
            if (string.IsNullOrWhiteSpace(newCmd)) return;

            selected.Name = newName;
            selected.Command = newCmd;
            SaveCommands();
            RefreshCommandList();
            CommandsList.SelectedIndex = CommandsList.Items.IndexOf(newName);
        }

        private async void ReloadGUIBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!telnet.IsConnected)
            {
                MessageBox.Show(LanguageManager.GetTranslation("Not connected to the box."));
                return;
            }
            var result = MessageBox.Show(LanguageManager.GetTranslation("Are you sure you want to reload the Enigma2 GUI?\n\nThe interface will restart (black screen for a few seconds)."),
                                         LanguageManager.GetTranslation("Confirm Reload"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
                await telnet.SendCommandAsync("init 4 && init 3");
        }

        private void CommandsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CommandsList.SelectedIndex >= 0)
                InputBox.Text = presets[CommandsList.SelectedIndex].Command;
        }

        private void OnDataReceived(string data) => Dispatcher.Invoke(() => OutputBox.AppendText(data));
        private void OnError(string error) => Dispatcher.Invoke(() =>
        {
            OutputBox.AppendText($"[ERROR] {error}\r\n");
            Debug.WriteLine($"Telnet error: {error}");
        });
        private void OnConnectionStateChanged(bool connected) => Dispatcher.Invoke(() =>
        {
            ConnectBtn.IsEnabled = !connected;
            DisconnectBtn.IsEnabled = connected;
            InputBox.IsEnabled = connected;
            SendBtn.IsEnabled = connected;
            StatusText.Text = connected ? LanguageManager.GetTranslation("Connected") : LanguageManager.GetTranslation("Disconnected");
        });

        private class CommandPreset { public string Name { get; set; } public string Command { get; set; } }
    }
}