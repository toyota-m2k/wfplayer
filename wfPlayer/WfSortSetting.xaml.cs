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
    /// WfSortSetting.xaml の相互作用ロジック
    /// </summary>
    public partial class WfSortSetting : Window
    {
        public WfSortInfo Result { get; private set; } = null;
        private WfSortInfo mSortInfo;
        public WfSortSetting(WfSortInfo sortInfo)
        {
            mSortInfo = sortInfo.Clone();
            InitializeComponent();
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            Result = mSortInfo;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
