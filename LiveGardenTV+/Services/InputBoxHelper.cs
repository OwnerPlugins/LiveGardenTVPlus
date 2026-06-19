using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LiveGardenTVPlus.Services
{
    public static class InputBoxHelper
    {
        public static string ShowInputBox(string prompt, string title, string defaultText = "")
        {
            var window = new Window
            {
                Title = title,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = (Brush)Application.Current.FindResource("WindowBackgroundBrush"),
                Foreground = (Brush)Application.Current.FindResource("ForegroundBrush")
            };

            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var textBox = new TextBox { Text = defaultText, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okBtn = new Button
            {
                Content = "OK",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 80,
                IsCancel = true
            };
            panel.Children.Add(okBtn);
            panel.Children.Add(cancelBtn);
            Grid.SetRow(panel, 2);
            grid.Children.Add(panel);

            window.Content = grid;
            okBtn.Click += (s, e) => { window.DialogResult = true; window.Close(); };
            cancelBtn.Click += (s, e) => { window.DialogResult = false; window.Close(); };
            textBox.Focus();

            if (window.ShowDialog() == true)
                return textBox.Text;
            return null;
        }
    }
}