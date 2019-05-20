using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace wfPlayer
{
    /// <summary>
    /// WfTrimmingPatternList.xaml の相互作用ロジック
    /// </summary>
    public partial class WfTrimmingPatternList : Window, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private bool setProp<T>(string name, ref T field, T value, params string[] familyProperties)
        {
            if (!field.Equals(value))
            {
                field = value;
                notify(name);
                foreach (var p in familyProperties)
                {
                    notify(p);
                }
                return true;
            }
            return false;
        }
        #endregion

        #region Binding Properties
        public WfFileItem.Trim CurrentItem
        {
            get; private set;
        }
        #endregion

        private ObservableCollection<WfFileItem.Trim> mList;

        public WfTrimmingPatternList()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mList = new ObservableCollection<WfFileItem.Trim>();
            WfPlayListDB.Instance.TP.List(mList);
            mTPListView.DataContext = mList;
            SelectItemByName(WfGlobalParams.Instance.LastSelectTrimmingName);
        }

        private void SelectItemByName(string name)
        {
            if(string.IsNullOrEmpty(name))
            {
                return;
            }

            foreach(var v in mList)
            {
                if(v.Name == name)
                {
                    mTPListView.SelectedItem = v;
                    break;
                }
            }
        }

        private void OnContentRendered(object sender, EventArgs e)
        {
            mTPListView.Focus();
            var item = mTPListView.ItemContainerGenerator.ContainerFromIndex(mTPListView.SelectedIndex) as System.Windows.Controls.ListViewItem;
            item?.Focus();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WfGlobalParams.Instance.TrimmingPatternListPlacement.ApplyPlacementTo(this);
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            WfGlobalParams.Instance.TrimmingPatternListPlacement.GetPlacementFrom(this);
        }

        public readonly static RoutedCommand CmdEditTrimming = new RoutedCommand("EditTrimming", typeof(MainWindow));
        public readonly static RoutedCommand CmdDeleteTrimming = new RoutedCommand("DeleteTrimming", typeof(MainWindow));

        private void CanEditTrimming(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = mTPListView.SelectedIndex >= 0;
        }

        private void CanDeleteTrimming(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = mTPListView.SelectedIndex >= 0;
        }

        private void EditTrimming(int index)
        {
            if (index < 0 || mList.Count <= index)
            {
                return;
            }
            var item = mList[index];
            var path = WfTrimmingPlayer.GetRefPath(item, null, false);
            if(null==path)
            {
                return;
            }
            var dlg = new WfTrimmingPlayer(item, path);
            dlg.ShowDialog();
            if(dlg.Result!=null)
            {
                mList.RemoveAt(index);
                mList.Insert(index, (WfFileItem.Trim)dlg.Result);
            }
        }

        private void DeleteTrimming(int index)
        {
            if (index < 0 || mList.Count <= index)
            {
                return;
            }
            var removed = mList[index];
            using (var txn = WfPlayListDB.Instance.Transaction())
            {
                WfPlayListDB.Instance.TP.Remove(removed.Name);
            }
            mList.RemoveAt(index);
        }

        private void ExecEditTrimming(object sender, ExecutedRoutedEventArgs e)
        {
            EditTrimming(mTPListView.SelectedIndex);
        }

        private void ExecDeleteTrimming(object sender, ExecutedRoutedEventArgs e)
        {
            DeleteTrimming(mTPListView.SelectedIndex);
        }

        private void OnDone(ITrim item)
        {
            if(item!=null)
            {
                Result = item;
                Close();
            }
        }

        public ITrim Result { get; private set; } = null;
        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OnDone(((System.Windows.Controls.ListViewItem)sender).Content as ITrim);
        }

        private void OnApplyPattern(object sender, RoutedEventArgs e)
        {
            OnDone(CurrentItem);
        }

        private void OnEditPattern(object sender, RoutedEventArgs e)
        {
            EditTrimming(mList.IndexOf(CurrentItem));
        }

        private void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            CurrentItem = mTPListView.SelectedItem as WfFileItem.Trim;
            if (CurrentItem != null)
            {
                WfGlobalParams.Instance.LastSelectTrimmingName = CurrentItem.Name;
            }
            notify("CurrentItem");
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch(e.Key)
            {
                case Key.Return:
                    OnDone(CurrentItem);
                    break;
                case Key.Escape:
                    Close();
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

    }
}
