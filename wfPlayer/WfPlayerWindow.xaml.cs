using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace wfPlayer
{
    /// <summary>
    /// WfPlayerWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class WfPlayerWindow : Window
    {
        public WfPlayerWindow()
        {
            InitializeComponent();
        }

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {

        }

        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {

        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }


    }
}
