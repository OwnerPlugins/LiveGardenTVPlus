using System.Diagnostics;
using System.Reflection;
using System.Windows;
using LiveGardenTVPlus.Services;

namespace LiveGardenTVPlus.Views
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
            ApplyLanguage();
            LoadVersion();
            LanguageManager.LanguageChanged += ApplyLanguage;
        }

        private void ApplyLanguage()
        {
            Title = LanguageManager.GetTranslation("LiveGardenTVPlus Help by Lululla");
            TitleText.Text = LanguageManager.GetTranslation("LiveGardenTVPlus Help by Lululla");
            CloseBtn.Content = LanguageManager.GetTranslation("Exit");
            SupportBtn.Content = LanguageManager.GetTranslation("Support");
            /* GitHubHelpBtn.Content = LanguageManager.GetTranslation("Repository"); */

            // Expander headers
            headerPlaylistLoading.Text = LanguageManager.GetTranslation("Playlist Loading");
            headerEditor.Text = LanguageManager.GetTranslation("Playlist Editor");
            headerPlayerControls.Text = LanguageManager.GetTranslation("Player Control");
            headerEpgLogos.Text = LanguageManager.GetTranslation("EPG & Logos");
            headerSettings.Text = LanguageManager.GetTranslation("Settings");
            headerThemes.Text = LanguageManager.GetTranslation("Themes");
            headerUpdates.Text = LanguageManager.GetTranslation("Updates");
            headerCredits.Text = LanguageManager.GetTranslation("Credits");

            // Playlist Loading texts
            txtLoadLocal.Text = LanguageManager.GetTranslation("Load local M3U/JSON file – via 'Load File' button (toolbar) or 'Open File' in editor.");
            txtLoadOnline.Text = LanguageManager.GetTranslation("Load online M3U/JSON URL – 'Load Online' button (toolbar) or 'Open from URL' in editor.");
            txtRecent.Text = LanguageManager.GetTranslation("Recent files – quick access to last 5 opened playlists.");
            txtDragDrop.Text = LanguageManager.GetTranslation("Drag & drop – drop M3U or JSON files directly onto main window.");

            // Playlist Editor texts
            txtEditorAccess.Text = LanguageManager.GetTranslation("Access via 'Edit Playlist' button on main toolbar.");
            txtEditor1.Text = LanguageManager.GetTranslation("• Edit channel names, groups, URLs, logos, tvg-id, country, languages, favorites.");
            txtEditor2.Text = LanguageManager.GetTranslation("• Add / rename / delete groups.");
            txtEditor3.Text = LanguageManager.GetTranslation("• Check URL availability (only visible channels).");
            txtEditor4.Text = LanguageManager.GetTranslation("• Export OK / FAIL / filtered lists as M3U or JSON.");
            txtEditor5.Text = LanguageManager.GetTranslation("• Import JSON (local or URL) with flexible field mapping (auto-detect, save mapping).");
            txtEditor6.Text = LanguageManager.GetTranslation("• Fetch logos from remote repository (OwnerPlugins/logos).");
            txtEditor7.Text = LanguageManager.GetTranslation("• Enrich with EPG: adds missing tvg-id using fuzzy matching.");
            txtEditor8.Text = LanguageManager.GetTranslation("• Sort any column by clicking header; use 'Reset Order' to revert to original order.");
            txtEditor9.Text = LanguageManager.GetTranslation("• Edit multiple URLs per channel (popup window).");

            // Player Controls texts
            txtPlayer1.Text = LanguageManager.GetTranslation("• Playback speed: 0.5x, 1x, 2x buttons.");
            txtPlayer2.Text = LanguageManager.GetTranslation("• Buffer slider: adjust HLS buffer (1‑10 seconds).");
            txtPlayer3.Text = LanguageManager.GetTranslation("• Picture‑in‑Picture (PIP) button.");
            txtPlayer4.Text = LanguageManager.GetTranslation("• Fullscreen UI: hides sidebar and status bar (ESC to exit).");
            txtPlayer5.Text = LanguageManager.GetTranslation("• Hide/Show channel list button.");
            txtPlayer6.Text = LanguageManager.GetTranslation("• Timeshift: pause live stream; drag slider to seek back; press 'Live' to return.");
            txtPlayer7.Text = LanguageManager.GetTranslation("• EPG info displayed in status bar for current channel.");

            // EPG & Logos texts
            txtEpg1.Text = LanguageManager.GetTranslation("EPG Guide – button on main toolbar shows full grid with program details (double‑click for info).");
            txtEpg2.Text = LanguageManager.GetTranslation("EPG source – can be set in Settings (epgshare01 or custom URL).");
            txtEpg3.Text = LanguageManager.GetTranslation("Logos – automatically download from GitHub repository (Settings → Logos Source).");
            txtEpg4.Text = LanguageManager.GetTranslation("Logo Picker – in editor, click '...' to choose logo from gallery.");

            // Settings texts
            txtSettings1.Text = LanguageManager.GetTranslation("Language – change UI language dynamically (translations in Languages folder).");
            txtSettings2.Text = LanguageManager.GetTranslation("Buffer size – adjust HLS buffer seconds.");
            txtSettings3.Text = LanguageManager.GetTranslation("Online playlist – select from GitHub repository (auto‑refresh).");
            txtSettings4.Text = LanguageManager.GetTranslation("EPG source – choose XMLTV file or enter custom URL.");
            txtSettings5.Text = LanguageManager.GetTranslation("Logos repository – choose owner/repo/path (default OwnerPlugins/logos).");
            txtSettings6.Text = LanguageManager.GetTranslation("Matching thresholds – set fuzzy match sensitivity for EPG and logos.");
            txtSettings7.Text = LanguageManager.GetTranslation("Save – persists all settings without reloading current playlist.");

            // Themes texts
            txtThemes1.Text = LanguageManager.GetTranslation("Theme picker – accessible via Tools menu on main toolbar.");
            txtThemes2.Text = LanguageManager.GetTranslation("16 colour themes + Light/Dark variants.");
            txtThemes3.Text = LanguageManager.GetTranslation("Selected theme is saved and applied on restart.");

            // Updates texts
            txtUpdates1.Text = LanguageManager.GetTranslation("Automatic check on startup – or click 'Check for updates' in Tools menu.");
            txtUpdates2.Text = LanguageManager.GetTranslation("Downloads ZIP, replaces files, restarts app – settings preserved.");

            // Credits texts
            txtCreditsDev.Text = LanguageManager.GetTranslation("Developer: Lululla");
            txtCreditsPlaylist.Text = LanguageManager.GetTranslation("Playlist repository: OwnerPlugins/TivuStreamList (Italian & international streams)");
            txtCreditsHls.Text = LanguageManager.GetTranslation("HLS playback: hls.js (MIT license)");
            txtCreditsUI.Text = LanguageManager.GetTranslation("UI: MaterialDesignThemes.Wpf");
            txtCreditsWebView.Text = LanguageManager.GetTranslation("WebView2: Microsoft Edge WebView2");
            txtCreditsCommunity.Text = LanguageManager.GetTranslation("Community & testing: CorvoBoys (corvoboys.org) & LinuxSat-Support.com");
        }

        private void LoadVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"{LanguageManager.GetTranslation("Version")} {version.Major}.{version.Minor}.{version.Build}";
        }

        private void SupportBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "https://www.corvoboys.org", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open browser: {ex.Message}");
            }
        }

        private void GitHubBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "https://github.com/OwnerPlugins/LiveGardenTVPlus", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open browser: {ex.Message}");
            }
        }

        private void txtCreditsGitHub_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://github.com/OwnerPlugins/LiveGardenTVPlus", UseShellExecute = true });
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}