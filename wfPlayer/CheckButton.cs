using System.Windows;
using System.Windows.Controls;

namespace wfPlayer
{
    public class CheckButton : Button
    {
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register("IsChecked", typeof(bool), typeof(CheckButton), new PropertyMetadata(false));
        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }
    }
}
