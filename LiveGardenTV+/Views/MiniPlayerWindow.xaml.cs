using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using MaterialDesignThemes.Wpf;
using LiveGardenTVPlus.Services;
using LiveGardenTVPlus.Models;

namespace LiveGardenTVPlus.Views
{
    public partial class MiniPlayerWindow : Window
    {
        private List<ChannelJson> _channels;
        private int _currentIndex;
        private string _streamUrl;
        private string _channelName;
        private bool _isPlaying = false;
        private bool _playerReady = false;

        public MiniPlayerWindow(List<ChannelJson> channels, int selectedIndex)
        {
            InitializeComponent();

            _channels = channels;
            _currentIndex = selectedIndex;

            var ch = channels[selectedIndex];
            _channelName = ch.name;
            _streamUrl = ch.stream_urls?.FirstOrDefault() ?? "";

            ApplyLanguage();
            LanguageManager.LanguageChanged += ApplyLanguage;

            ChannelNameText.Text = _channelName;
            StatusText.Text = LanguageManager.GetTranslation("Loading...");
            PlayPauseBtn.IsEnabled = false;

            this.Loaded += async (s, e) =>
            {
                await InitializeWebView2();
            };
        }

        private async Task InitializeWebView2()
        {
            try
            {
                string userDataFolder = Path.Combine(Path.GetTempPath(), "LiveGardenTVPlus", "WebView2");
                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
                await Player.EnsureCoreWebView2Async(env);

                if (Player.CoreWebView2 == null)
                {
                    StatusText.Text = "WebView2 initialization failed";
                    return;
                }

                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlayerHost", "player.html");
                if (!File.Exists(htmlPath))
                {
                    StatusText.Text = "player.html not found";
                    return;
                }

                var webView = Player.CoreWebView2;

                webView.NavigationCompleted += async (sender, args) =>
                {
                    if (!args.IsSuccess)
                    {
                        StatusText.Text = $"Navigation failed: {args.WebErrorStatus}";
                        return;
                    }

                    try
                    {
                        await Task.Delay(500);
                        var check = await webView.ExecuteScriptAsync("typeof playStream === 'function'");
                        if (check == "true")
                        {
                            _playerReady = true;
                            PlayPauseBtn.IsEnabled = true;
                            StatusText.Text = LanguageManager.GetTranslation("Ready");
                            await PlayStream(webView);
                        }
                        else
                        {
                            StatusText.Text = "playStream function not found";
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusText.Text = $"Script error: {ex.Message}";
                    }
                };

                string htmlContent = File.ReadAllText(htmlPath);
                webView.NavigateToString(htmlContent);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private async Task PlayStream(CoreWebView2 webView)
        {
            string js = $"playStream('{_streamUrl.Replace("'", "\\'")}');";
            await webView.ExecuteScriptAsync(js);
        }

        private void ApplyLanguage()
        {
            Title = LanguageManager.GetTranslation("Mini Player");
            if (!string.IsNullOrEmpty(_channelName))
                ChannelNameText.Text = _channelName;
            else
                ChannelNameText.Text = LanguageManager.GetTranslation("No channel selected");
            StatusText.Text = LanguageManager.GetTranslation("Ready");
            PlayPauseBtn.ToolTip = LanguageManager.GetTranslation("Play/Pause");
            StopBtn.ToolTip = LanguageManager.GetTranslation("Stop");
            FullscreenBtn.ToolTip = LanguageManager.GetTranslation("Fullscreen");
            ChUpBtn.ToolTip = LanguageManager.GetTranslation("Channel Up");
            ChDownBtn.ToolTip = LanguageManager.GetTranslation("Channel Down");
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        private async void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Player.CoreWebView2 == null || !_playerReady) return;
            try
            {
                if (!_isPlaying)
                {
                    await Player.CoreWebView2.ExecuteScriptAsync("video.play();");
                    _isPlaying = true;
                    PlayPauseBtn.Content = new PackIcon { Kind = PackIconKind.Pause, Width = 24, Height = 24 };
                    StatusText.Text = LanguageManager.GetTranslation("Playing Media");
                }
                else
                {
                    await Player.CoreWebView2.ExecuteScriptAsync("video.pause();");
                    _isPlaying = false;
                    PlayPauseBtn.Content = new PackIcon { Kind = PackIconKind.Play, Width = 24, Height = 24 };
                    StatusText.Text = LanguageManager.GetTranslation("Paused");
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private async void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Player.CoreWebView2 == null || !_playerReady) return;
            try
            {
                await Player.CoreWebView2.ExecuteScriptAsync("video.pause(); video.currentTime = 0;");
                _isPlaying = false;
                PlayPauseBtn.Content = new PackIcon { Kind = PackIconKind.Play, Width = 24, Height = 24 };
                StatusText.Text = LanguageManager.GetTranslation("Stopped");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private bool _isFullscreen = false;

        private void FullscreenBtn_Click(object sender, RoutedEventArgs e)
        {
            _isFullscreen = !_isFullscreen;
            if (_isFullscreen)
            {
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
                this.Topmost = false;
                FullscreenBtn.Content = new PackIcon { Kind = PackIconKind.FullscreenExit, Width = 24, Height = 24 };
            }
            else
            {
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Normal;
                this.Topmost = true;
                FullscreenBtn.Content = new PackIcon { Kind = PackIconKind.Fullscreen, Width = 24, Height = 24 };
            }
        }

        // ------ CH+ / CH- -------
        private async void ChUpBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_channels == null || _channels.Count == 0) return;
            int newIndex = (_currentIndex + 1) % _channels.Count;
            await ChangeChannel(newIndex);
        }

        private async void ChDownBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_channels == null || _channels.Count == 0) return;
            int newIndex = (_currentIndex - 1 + _channels.Count) % _channels.Count;
            await ChangeChannel(newIndex);
        }

        private async Task ChangeChannel(int newIndex)
        {
            _currentIndex = newIndex;
            var ch = _channels[newIndex];
            _channelName = ch.name;
            _streamUrl = ch.stream_urls?.FirstOrDefault() ?? "";
            ChannelNameText.Text = _channelName;

            _isPlaying = false;
            PlayPauseBtn.Content = new PackIcon { Kind = PackIconKind.Play, Width = 24, Height = 24 };
            StatusText.Text = LanguageManager.GetTranslation("Loading...");

            if (Player.CoreWebView2 != null && _playerReady)
            {
                try
                {
                    await PlayStream(Player.CoreWebView2);
                    _isPlaying = true;
                    PlayPauseBtn.Content = new PackIcon { Kind = PackIconKind.Pause, Width = 24, Height = 24 };
                    StatusText.Text = LanguageManager.GetTranslation("Playing Media");
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error: {ex.Message}";
                }
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Player?.Dispose();
            base.OnClosing(e);
        }
    }
}