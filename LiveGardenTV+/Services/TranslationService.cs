using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LiveGardenTVPlus.Services
{
    public static class TranslationHelper
    {
        public static void TranslateUI(DependencyObject parent)
        {
            if (parent == null) return;

            // Skip dynamic controls that show runtime data
            if (parent is FrameworkElement fe)
            {
                if (fe.Name == "StreamNameStatus" || fe.Name == "StatusTextBlock")
                    return;
            }

            // Translate ContentControl (Button, Label, CheckBox...)
            if (parent is ContentControl contentControl && contentControl.Content is string text)
            {
                string translated = LanguageManager.GetTranslation(text);
                if (translated != text)
                    contentControl.Content = translated;
            }

            // Translate TextBlock (only if not dynamic)
            if (parent is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text))
            {
                string translated = LanguageManager.GetTranslation(textBlock.Text);
                if (translated != textBlock.Text)
                    textBlock.Text = translated;
            }

            // Translate ToolTip
            if (parent is FrameworkElement element && element.ToolTip is string tooltip)
            {
                string translated = LanguageManager.GetTranslation(tooltip);
                if (translated != tooltip)
                    element.ToolTip = translated;
            }

            // Recursively process children
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                TranslateUI(child);
            }
        }
    }
}