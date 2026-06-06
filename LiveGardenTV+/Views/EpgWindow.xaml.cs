using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;

namespace LiveGardenTVPlus.Views
{
    public partial class EpgWindow : Window
    {
        private EpgService _epgService;
        private List<Channel> _channels;

        public EpgWindow(EpgService epgService, List<Channel> channels)
        {
            InitializeComponent();
            _epgService = epgService;
            _channels = channels;

            // Subscribe to language changes
            LanguageManager.LanguageChanged += OnLanguageChanged;
            ApplyLanguage();

            var epgChannels = _channels.Select(ch => new
            {
                Channel = ch,
                EpgId = GetEpgChannelId(ch)
            })
            .Select(x => new EpgChannelDisplay
            {
                DisplayName = x.EpgId != null ? x.Channel.Name : x.Channel.Name + " (No EPG)",
                EpgId = x.EpgId
            })
            .OrderBy(x => x.DisplayName)
            .ToList();

            ChannelCombo.ItemsSource = epgChannels;
            if (ChannelCombo.Items.Count > 0)
                ChannelCombo.SelectedIndex = 0;
        }

        private void OnLanguageChanged()
        {
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            // Translate window title
            this.Title = LanguageManager.GetTranslation("TV Guide (EPG)");

            // Translate static controls
            var channelLabel = FindName("ChannelLabel") as TextBlock;
            if (channelLabel != null) channelLabel.Text = LanguageManager.GetTranslation("Channel:");

            RefreshBtn.Content = LanguageManager.GetTranslation("Refresh");
            DetailsBtn.Content = LanguageManager.GetTranslation("Details");

            // Update DataGrid column headers
            var startColumn = ProgramsGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Start (local)");
            if (startColumn != null) startColumn.Header = LanguageManager.GetTranslation("Start (local)");

            var endColumn = ProgramsGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "End (local)");
            if (endColumn != null) endColumn.Header = LanguageManager.GetTranslation("End (local)");

            var titleColumn = ProgramsGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Title");
            if (titleColumn != null) titleColumn.Header = LanguageManager.GetTranslation("Title");

            var descColumn = ProgramsGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Description");
            if (descColumn != null) descColumn.Header = LanguageManager.GetTranslation("Description");

            var catColumn = ProgramsGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Category");
            if (catColumn != null) catColumn.Header = LanguageManager.GetTranslation("Category");
        }

        private string GetEpgChannelId(Channel ch)
        {
            // First try by tvgId
            if (!string.IsNullOrEmpty(ch.TvgId) && _epgService.HasChannel(ch.TvgId))
                return ch.TvgId;

            // Then try fuzzy match by name
            var program = _epgService.GetCurrentProgram(ch.Name, ch.TvgId, DateTime.UtcNow);
            if (program != null)
            {
                return _epgService.GetMappedEpgId(ch.Name);
            }

            // Return null but we will still show the channel in the list with "no EPG"
            return null;
        }

        private async void ChannelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChannelCombo.SelectedItem is EpgChannelDisplay selected)
            {
                // Load programs for this EPG channel
                var programs = await _epgService.GetProgramsForChannelAsync(selected.EpgId, DateTime.Today, DateTime.Today.AddDays(1));
                var list = programs.Select(p => new EpgProgramDisplay
                {
                    Title = p.Title,
                    Description = p.Description,
                    Category = p.Category,
                    StartLocal = p.Start.ToLocalTime(),
                    StopLocal = p.Stop.ToLocalTime()
                }).OrderBy(p => p.StartLocal).ToList();
                ProgramsGrid.ItemsSource = list;
            }
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            // Reload EPG data
            var prefs = UserPreferences.Load();
            if (!string.IsNullOrEmpty(prefs.EpgUrl))
            {
                await _epgService.LoadEpgAsync(prefs.EpgUrl);
                // Refresh current selection
                ChannelCombo_SelectionChanged(null, null);
            }
        }

        // Show details for the selected program (same as double-click)
        private void DetailsBtn_Click(object sender, RoutedEventArgs e)
        {
            var program = ProgramsGrid.SelectedItem as EpgProgramDisplay;
            if (program == null)
            {
                MessageBox.Show(LanguageManager.GetTranslation("Please select a program first."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ShowProgramDetails(program);
        }

        // Extract the detail window logic into a separate method
        private void ShowProgramDetails(EpgProgramDisplay program)
        {
            var detailWindow = new Window
            {
                Title = LanguageManager.GetTranslation("Program Details"),
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Content = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(10),
                        Children =
                        {
                            new TextBlock { Text = program.Title, FontWeight = FontWeights.Bold, FontSize = 16, Margin = new Thickness(0,0,0,10) },
                            new TextBlock { Text = $"{LanguageManager.GetTranslation("Time")}: {program.StartLocal:HH:mm} - {program.StopLocal:HH:mm}", Margin = new Thickness(0,0,0,5) },
                            new TextBlock { Text = $"{LanguageManager.GetTranslation("Category")}: {program.Category}", Margin = new Thickness(0,0,0,10) },
                            new TextBlock { Text = LanguageManager.GetTranslation("Description") + ":", FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,5) },
                            new TextBlock { Text = program.Description ?? LanguageManager.GetTranslation("(No description)"), TextWrapping = TextWrapping.Wrap }
                        }
                    }
                }
            };
            detailWindow.ShowDialog();
        }

        // double-click handler
        private void ProgramsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var program = ProgramsGrid.SelectedItem as EpgProgramDisplay;
            if (program != null)
                ShowProgramDetails(program);
        }

        public class EpgChannelDisplay
        {
            public string DisplayName { get; set; }
            public string EpgId { get; set; }
        }

        public class EpgProgramDisplay
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public string Category { get; set; }
            public DateTime StartLocal { get; set; }
            public DateTime StopLocal { get; set; }
        }
    }
}