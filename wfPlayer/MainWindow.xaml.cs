using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace wfPlayer
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private WfFileItemList mFileList;
        private WfPlayListDB mPlayListDB;

        public MainWindow()
        {
            InitializeComponent();

        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mPlayListDB = WfPlayListDB.CreateInstance(null);
            mFileList = new WfFileItemList();
            mFileList.CurrentChanged += PlayingItemChanged;
            LoadListFromDB();

            mFileListView.DataContext = mFileList;
            var path = mPlayListDB.GetValueAt("LastItem");
            int index = mFileList.IndexOfPath(path);
            if(index<0)
            {
                index = 0;
            }
            mFileListView.SelectedIndex = index;
            mFileListView.ScrollIntoView(mFileList[index]);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            mFileList.CurrentChanged -= PlayingItemChanged;
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var sel = mFileListView.SelectedItem as WfFileItem;
            mPlayListDB.SetValueAt("LastItem", sel.FullPath);
        }

        private void PlayingItemChanged(int from, WfFileItem fromItem, int to, WfFileItem toItem)
        {
            mFileListView.SelectedIndex = to;
            mFileListView.ScrollIntoView(toItem);
        }

        #region Command Handlers

        private void OnNewFile(object sender, RoutedEventArgs e)
        {

        }

        private void OnOpenFile(object sender, RoutedEventArgs e)
        {

        }

        private void OnRegisterFolder(object sender, RoutedEventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    RegisterFilesInPath(fbd.SelectedPath);
                }
            }
        }

        private void OnPlayAll(object sender, RoutedEventArgs e)
        {
            if (mFileList.Count > 0)
            {
                var page = new WfPlayerWindow();
                mFileList.CurrentIndex = mFileListView.SelectedIndex;
                page.SetSources(mFileList);
                page.Show();
                page.Closed += (s, x) =>
                {
                    var c = mFileList.Current;
                    if (null != c)
                    {
                        mPlayListDB.SetValueAt("LastItem", ((WfFileItem)c).FullPath);
                    }
                };
            }
        }

        private void OnCreateTrimming(object sender, RoutedEventArgs e)
        {

        }

        private void OnSelectTrimming(object sender, RoutedEventArgs e)
        {

        }

        private void OnResetTrimming(object sender, RoutedEventArgs e)
        {

        }

        private void OnResetCounter(object sender, RoutedEventArgs e)
        {

        }

        private void OnSetRating(object sender, RoutedEventArgs e)
        {

        }
        #endregion

        private void LoadListFromDB()
        {
            mFileList.Clear();
            using (var retriever = mPlayListDB.QueryAll(false))
            {
                foreach (var item in retriever)
                {
                    mFileList.Add(item);
                }
            }
        }

        private void RegisterFilesInPath(string folderPath)
        {
            var videoExt = new[] { "mp4", "wmv", "avi", "mov", "avi", "mpg", "mpeg", "mpe", "ram", "rm" };

            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                           .Where(file => videoExt.Any(x => file.EndsWith(x, StringComparison.OrdinalIgnoreCase)));

            using (var appender = mPlayListDB.BeginRegister(folderPath))
            {
                foreach (var file in files)
                {
                    var f = new WfFileItem(file);
                    if (f.Exists)
                    {
                        if(appender.Add(f))
                        {
                            mFileList.Add(f);
                        }
                    }
                }
            }
        }

        private void OnListItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OnPlayAll(null, null);
        }

        private void OnHeaderClick(object sender, RoutedEventArgs e)
        {
            //var header = (GridViewColumnHeader)e.OriginalSource;
            //if (header.Column == null)
            //{
            //    return;
            //}
            //switch(header.Column.Header)
        }

        public readonly static RoutedCommand CreateTrimming = new RoutedCommand("CreateTrimming", typeof(MainWindow));
        public readonly static RoutedCommand ApplyTrimming = new RoutedCommand("ApplyTrimming", typeof(MainWindow));
        public readonly static RoutedCommand ResetCounter = new RoutedCommand("ResetCounter", typeof(MainWindow));

        private void CanCreateTrimming(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = mFileListView.SelectedIndex >= 0;
        }
        private void CanApplyTrimming(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = mFileListView.SelectedIndex >= 0;
        }

        private void ExecCreateTrimming(object sender, ExecutedRoutedEventArgs e)
        {
            var item = mFileListView.SelectedItem as WfFileItem;
            var tp = new WfTrimmingPlayer(item.Trimming, item.FullPath);
            WfTrimmingPlayer.ResultEventProc onNewTrimming = (result, db) =>
            {
                item.Trimming = tp.Result;
                db.UpdatePlaylistItem(item, (long)WfPlayListDB.FieldFlag.TRIMMING);
            };
            tp.OnResult += onNewTrimming;
            tp.ShowDialog();
            tp.OnResult -= onNewTrimming;
            e.Handled = true;
        }

        private void ExecApplyTrimming(object sender, ExecutedRoutedEventArgs e)
        {
            var dlg = new WfTrimmingPatternList();
            dlg.ShowDialog();
            if(dlg.Result!=null)
            {
                using(var txn = WfPlayListDB.Instance.Transaction())
                {
                    foreach(WfFileItem v in mFileListView.SelectedItems)
                    {
                        v.Trimming = dlg.Result;
                        WfPlayListDB.Instance.UpdatePlaylistItem(v, (long)WfPlayListDB.FieldFlag.TRIMMING);
                    }
                }
            }
        }

        private void ExecResetCounter(object sender, ExecutedRoutedEventArgs e)
        {
            using (var txn = WfPlayListDB.Instance.Transaction())
            {
                foreach (WfFileItem c in mFileListView.SelectedItems)
                {
                    if (c.PlayCount > 0)
                    {
                        c.PlayCount = 0;
                        WfPlayListDB.Instance.UpdatePlaylistItem(c, (long)WfPlayListDB.FieldFlag.PLAY_COUNT);
                    }
                }
            }
        }

        private void CanResetCounter(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = mFileListView.SelectedIndex >= 0;
        }

    }
}
