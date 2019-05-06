using System;
using System.ComponentModel;
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
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private bool setProp<T>(string name, ref T field, T value)
        {
            if (!field.Equals(value))
            {
                field = value;
                notify(name);
                return true;
            }
            return false;
        }

        private bool setProp<T>(string[] names, ref T field, T value)
        {
            if (!field.Equals(value))
            {
                field = value;
                foreach (var name in names)
                {
                    notify(name);
                }
                return true;
            }
            return false;
        }

        public void NotifyPropertyChanged(string propName)
        {
            notify(propName);
        }

        #endregion

        #region Binding Properties

        public bool ItemSelected => mFileListView.SelectedIndex >= 0;

        public bool ItemMultiSelected => mFileListView.SelectedItems.Count > 1;

        private bool mIsFiltered = false;
        public bool IsFiltered
        {
            get => mIsFiltered;
            set => setProp("IsFiltered", ref mIsFiltered, value);
        }

        #endregion

        #region Private Field

        private WfFileItemList mFileList = null;
        private WfPlayListDB mPlayListDB = null;

        #endregion

        #region Creation/Termination

        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();

        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WfGlobalParams.Instance.Placement.ApplyPlacementTo(this);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            OpenDB(WfGlobalParams.Instance.FilePath);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            mFileList.CurrentChanged -= PlayingItemChanged;
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveCurrentSelection();
            WfGlobalParams.Instance.Placement.GetPlacementFrom(this);
            WfGlobalParams.Instance.Serialize();
        }

        #endregion

        #region Event Handlers

        private void PlayingItemChanged(int from, WfFileItem fromItem, int to, WfFileItem toItem)
        {
            mFileListView.SelectedIndex = to;
            mFileListView.ScrollIntoView(toItem);
        }

        private void OnFileItemSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            notify("ItemSelected");
            notify("ItemMultiSelected");
        }

        #endregion

        #region Command Handlers

        private void OnOpenFile(object sender, RoutedEventArgs e)
        {
            SelectDB();
        }

        private void OnRegisterFolder(object sender, RoutedEventArgs e)
        {
            RegisterFolder();
        }

        private void OnPlayAll(object sender, RoutedEventArgs e)
        {
            Play();
        }

        private void OnCreateTrimming(object sender, RoutedEventArgs e)
        {
            CreateTrimmingPattern();
        }

        private void OnSelectTrimming(object sender, RoutedEventArgs e)
        {
            ApplyTrimmingPattern();
        }

        private void OnResetTrimming(object sender, RoutedEventArgs e)
        {
            ResetTrimmingPattern();
        }

        private void OnResetCounter(object sender, RoutedEventArgs e)
        {
            ResetPlayedCounter();
        }

        private void OnSetRating(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            SetRating((Ratings)Convert.ToInt16(btn.Tag));
        }
        #endregion


        private void OnListItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Play();
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

        #region Operation / Function

        private void SelectDB()
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "WfPlayer Data (.wpd)|*.wpd|All Files (*.*)|*.*";
            dlg.CheckFileExists = false;
            dlg.DefaultExt = "wpd";
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OpenDB(dlg.SafeFileName);
            }
        }

        private void OpenDB(string dbPath)
        {
            if(mPlayListDB!=null)
            {
                SaveCurrentSelection();
            }
            WfGlobalParams.Instance.AddMru(dbPath);
            mPlayListDB = WfPlayListDB.CreateInstance(dbPath);
            mFileList = new WfFileItemList();
            mFileList.CurrentChanged += PlayingItemChanged;
            LoadListFromDB();

            mFileListView.DataContext = mFileList;
            if (mFileList.Count > 0)
            {
                var path = mPlayListDB.GetValueAt("LastItem");
                int index = mFileList.IndexOfPath(path);
                if (index < 0)
                {
                    index = 0;
                }
                mFileListView.SelectedIndex = index;
                mFileListView.ScrollIntoView(mFileList[index]);
            }
        }

        private void RegisterFolder()
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

        private void Play()
        {
            if (mFileList.Count > 0)
            {
                var page = new WfPlayerWindow();
                mFileList.CurrentIndex = mFileListView.SelectedIndex;
                page.SetSources(mFileList);
                page.ShowDialog();
            }
        }

        private void ResetPlayedCounter()
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

        private void CreateTrimmingPattern()
        {
            var item = mFileListView.SelectedItem as WfFileItem;
            if (null == item)
            {
                return;
            }
            var tp = new WfTrimmingPlayer(item.Trimming, item.FullPath);
            WfTrimmingPlayer.ResultEventProc onNewTrimming = (result, db) =>
            {
                if (null != tp.Result)
                {
                    item.Trimming = tp.Result;
                    db.UpdatePlaylistItem(item, (long)WfPlayListDB.FieldFlag.TRIMMING);
                }
            };
            tp.OnResult += onNewTrimming;
            tp.ShowDialog();
            tp.OnResult -= onNewTrimming;
        }

        private void ApplyTrimmingPattern()
        {
            var dlg = new WfTrimmingPatternList();
            dlg.ShowDialog();
            if (dlg.Result != null)
            {
                using (var txn = WfPlayListDB.Instance.Transaction())
                {
                    foreach (WfFileItem v in mFileListView.SelectedItems)
                    {
                        v.Trimming = dlg.Result;
                        WfPlayListDB.Instance.UpdatePlaylistItem(v, (long)WfPlayListDB.FieldFlag.TRIMMING);
                    }
                }
            }
        }

        private void ResetTrimmingPattern()
        {
            using (var txn = WfPlayListDB.Instance.Transaction())
            {
                foreach (WfFileItem v in mFileListView.SelectedItems)
                {
                    v.Trimming = WfFileItem.Trim.NoTrim;
                    WfPlayListDB.Instance.UpdatePlaylistItem(v, (long)WfPlayListDB.FieldFlag.TRIMMING);
                }
            }
        }

        private void SetRating(Ratings rating)
        {
            using (var txn = WfPlayListDB.Instance.Transaction())
            {
                foreach (WfFileItem v in mFileListView.SelectedItems)
                {
                    if (v.Rating != rating)
                    {
                        v.Rating = rating;
                        WfPlayListDB.Instance.UpdatePlaylistItem(v, (long)WfPlayListDB.FieldFlag.RATING);
                    }
                }
            }
        }

        #endregion

        #region Sub-Routines

        private void LoadListFromDB(string filter="")
        {
            mFileList.Clear();
            using (var retriever = mPlayListDB.QueryAll(false, filter))
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
                        if (appender.Add(f))
                        {
                            mFileList.Add(f);
                        }
                    }
                }
            }
        }

        private void SaveCurrentSelection()
        {
            var sel = mFileListView?.SelectedItem as WfFileItem;
            if (sel != null)
            {
                mPlayListDB.SetValueAt("LastItem", sel.FullPath);
            }
        }
        #endregion

        #region Routed Commands

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
            CreateTrimmingPattern();
            e.Handled = true;
        }

        private void ExecApplyTrimming(object sender, ExecutedRoutedEventArgs e)
        {
            ApplyTrimmingPattern();
            e.Handled = true;
        }

        private void ExecResetCounter(object sender, ExecutedRoutedEventArgs e)
        {
            ResetPlayedCounter();
            e.Handled = true;
        }

        private void CanResetCounter(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = mFileListView.SelectedIndex >= 0;
        }

        #endregion

        private void OnFilter(object sender, RoutedEventArgs e)
        {
            if (IsFiltered)
            {
                LoadListFromDB();
                IsFiltered = false;
            }
            else
            {
                var dlg = new WfFilterSetting(WfGlobalParams.Instance.Filter);
                dlg.ShowDialog();
                if (dlg.Result != null)
                {
                    WfGlobalParams.Instance.Filter = dlg.Result;
                    LoadListFromDB(dlg.Result.SQL);
                    IsFiltered = true;
                }
            }
        }
    }
}
