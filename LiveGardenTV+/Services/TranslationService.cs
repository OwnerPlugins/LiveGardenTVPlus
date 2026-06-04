using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LiveGardenTVPlus.Services
{
    public static class TranslationHelper
    {
        private static readonly Dictionary<DependencyObject, string> _originalTexts = new Dictionary<DependencyObject, string>();

        public static void ResetCache()
        {
            _originalTexts.Clear();
        }

        public static void TranslateUI(DependencyObject parent)
        {
            if (parent == null) return;

            // Skip dynamic status controls
            if (parent is FrameworkElement fe && (fe.Name == "StreamNameStatus" || fe.Name == "StatusTextBlock"))
                return;

            // Handle Button with StackPanel (MaterialDesign pattern)
            if (parent is Button button && button.Content is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
                    {
                        string originalKey = GetOriginalKey(tb, tb.Text);
                        string translated = LanguageManager.GetTranslation(originalKey);
                        if (translated != originalKey)
                            tb.Text = translated;
                    }
                }
            }

            // Handle simple ContentControl
            if (parent is ContentControl contentControl && contentControl.Content is string text)
            {
                string originalKey = GetOriginalKey(contentControl, text);
                string translated = LanguageManager.GetTranslation(originalKey);
                if (translated != originalKey)
                    contentControl.Content = translated;
            }

            // Handle TextBlock
            if (parent is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text))
            {
                string originalKey = GetOriginalKey(textBlock, textBlock.Text);
                string translated = LanguageManager.GetTranslation(originalKey);
                if (translated != originalKey)
                    textBlock.Text = translated;
            }

            // Handle ToolTip
            if (parent is FrameworkElement element && element.ToolTip is string tooltip)
            {
                string originalKey = GetOriginalKey(element, tooltip);
                string translated = LanguageManager.GetTranslation(originalKey);
                if (translated != originalKey)
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

        private static string GetOriginalKey(DependencyObject obj, string currentText)
        {
            // Use Name as key if available, otherwise fallback to stored text
            if (obj is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
                return fe.Name;

            if (_originalTexts.TryGetValue(obj, out string original))
                return original;

            _originalTexts[obj] = currentText;
            return currentText;
        }
    }
}