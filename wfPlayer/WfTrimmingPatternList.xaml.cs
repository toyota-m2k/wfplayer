using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

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
            var index = mTPListView.SelectedIndex;
            if(index<0||mList.Count<=index)
            {
                return;
            }
            var item = mList[index];
            var dlg = new WfTrimmingPlayer(item);
            dlg.ShowDialog();
            if(dlg.Result!=null)
            {
                mList.RemoveAt(index);
                mList.Insert(index, (WfFileItem.Trim)dlg.Result);
            }
        }

        private void ExecDeleteTrimming(object sender, ExecutedRoutedEventArgs e)
        {
            var removed = new List<WfFileItem.Trim>();
            using (var txn = WfPlayListDB.Instance.Transaction())
            {
                foreach (WfFileItem.Trim v in mTPListView.SelectedItems)
                {
                    WfPlayListDB.Instance.TP.Remove(v.Name);
                    removed.Add(v);
                }
            }
            foreach(var v in removed)
            {
                mList.Remove(v);
            }
        }

        public ITrim Result { get; private set; } = null;
        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Result = ((System.Windows.Controls.ListViewItem)sender).Content as ITrim;
            Close();
        }
    }
}
