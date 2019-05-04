using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// WfTrimmingPatternList.xaml の相互作用ロジック
    /// </summary>
    public partial class WfTrimmingPatternList : Window
    {
        private ObservableCollection<WfFileItem.Trim> mList;

        public WfTrimmingPatternList()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mList = new ObservableCollection<WfFileItem.Trim>();
            WfPlayListDB.Instance.TP.List(mList);
            mTPListView.DataContext = mList;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {

        }


        public readonly static RoutedCommand EditTrimming = new RoutedCommand("EditTrimming", typeof(MainWindow));
        public readonly static RoutedCommand DeleteTrimming = new RoutedCommand("DeleteTrimming", typeof(MainWindow));

        private void CanEditTrimming(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = mTPListView.SelectedIndex >= 0;
        }

        private void CanDeleteTrimming(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = mTPListView.SelectedIndex >= 0;
        }

        private void ExecEditTrimming(object sender, ExecutedRoutedEventArgs e)
        {

        }

        private void ExecDeleteTrimming(object sender, ExecutedRoutedEventArgs e)
        {

        }

        public ITrim Result { get; private set; } = null;
        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Result = ((System.Windows.Controls.ListViewItem)sender).Content as ITrim;
            Close();
        }
    }
}
