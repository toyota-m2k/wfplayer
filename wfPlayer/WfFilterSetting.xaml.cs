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
    /// WfFilterSetting.xaml の相互作用ロジック
    /// </summary>
    public partial class WfFilterSetting : Window
    {
        private WfFilter mFilter;
        public WfFilter Result { get; private set; }

        public WfFilterSetting(WfFilter filter)
        {
            Result = null;
            mFilter = filter ?? new WfFilter();
            DataContext = mFilter;
            InitializeComponent();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            Result = mFilter;
            Close();
        }
    }
}
