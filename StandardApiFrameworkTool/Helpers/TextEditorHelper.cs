using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;

namespace StandardApiFrameworkTool.Helpers
{
    public static class TextEditorHelper
    {
        public static readonly DependencyProperty BoundTextProperty =
            DependencyProperty.RegisterAttached(
                "BoundText",
                typeof(string),
                typeof(TextEditorHelper),
                new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundTextChanged));

        public static string GetBoundText(DependencyObject obj)
        {
            return (string)obj.GetValue(BoundTextProperty);
        }

        public static void SetBoundText(DependencyObject obj, string value)
        {
            obj.SetValue(BoundTextProperty, value);
        }

        private static void OnBoundTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextEditor editor)
            {
                // Ensure the TextEditor has a Document
                if (editor.Document == null)
                {
                    editor.Document = new ICSharpCode.AvalonEdit.Document.TextDocument();
                }

                // Detach previous event handler to avoid duplicates
                editor.TextChanged -= Editor_TextChanged;

                // Update the TextEditor with the new value
                string newValue = e.NewValue as string ?? string.Empty;
                if (editor.Document.Text != newValue)
                {
                    editor.Document.Text = newValue;
                }

                // Attach the TextChanged event
                editor.TextChanged += Editor_TextChanged;
            }
        }


        private static void Editor_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextEditor editor)
            {
                SetBoundText(editor, editor.Document?.Text);
            }
        }
    }
}
