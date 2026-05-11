using System.Windows;
using System.Windows.Controls;

namespace Lightspeed_wpf
{
    public class InputDialog : Window
    {
        private System.Windows.Controls.TextBox textBox;
        public string ResponseText => textBox.Text;

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            Title = title;
            Width = 350;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;
            Background = System.Windows.Media.Brushes.White;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Margin = new Thickness(15);

            var label = new System.Windows.Controls.Label { Content = prompt, Margin = new Thickness(0, 10, 0, 5) };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            textBox = new System.Windows.Controls.TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(5) };
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            Grid.SetRow(buttonPanel, 2);

            var okButton = new System.Windows.Controls.Button { Content = "确定", Width = 80, Height = 28, Margin = new Thickness(5, 0, 0, 0) };
            okButton.Click += (s, e) => { DialogResult = true; Close(); };
            buttonPanel.Children.Add(okButton);

            var cancelButton = new System.Windows.Controls.Button { Content = "取消", Width = 80, Height = 28, Margin = new Thickness(5, 0, 0, 0) };
            cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(buttonPanel);
            Content = grid;

            Loaded += (s, e) => textBox.Focus();
        }
    }
}