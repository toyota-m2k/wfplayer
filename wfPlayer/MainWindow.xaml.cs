using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace wfPlayer
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private bool setProp<T>(string name, ref T field, T value) {
            if (!field.Equals(value)) {
                field = value;
                notify(name);
                return true;
            }
            return false;
        }

        private bool setProp<T>(string[] names, ref T field, T value) {
            if (!field.Equals(value)) {
                field = value;
                foreach (var name in names) {
                    notify(name);
                }
                return true;
            }
            return false;
        }

        public void NotifyPropertyChanged(string propName) {
            notify(propName);
        }

        #endregion

        #region Binding Properties

        public bool ItemSelected => mFileListView.SelectedIndex >= 0;

        public bool ItemMultiSelected => mFileListView.SelectedItems.Count > 1;

        private bool mIsFiltered = false;
        public bool IsFiltered {
            get => mIsFiltered;
            set => setProp("IsFiltered", ref mIsFiltered, value);
        }

        public GeCommand AddFolderCommand { get; }
        public GeCommand RefreshAllCommand { get; }

        #endregion

        #region Private Field

        private WfFileItemList mFileList = null;
        private WfPlayListDB mPlayListDB = null;
        private WfSorter<WfFileItem> mSorter = null;

        #endregion

        #region Creation/Termination

        public MainWindow() {
            AddFolderCommand = new GeCommand(() => {
                RegisterFolder();
            });
            RefreshAllCommand = new GeCommand(() => {
                RefreshDB();
            });

            DataContext = this;
            InitializeComponent();

        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            WfGlobalParams.Instance.Placement.ApplyPlacementTo(this);
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            OpenDB(WfGlobalParams.Instance.FilePath);
            UpdateColumnHeaderOnSort(WfGlobalParams.Instance.SortInfo);
            CreateFileMenu();
        }

        private void CreateFileMenu() {
            var curPath = WfGlobalParams.Instance.FilePath;
            var menu = WfGlobalParams.Instance.MRU.Aggregate(new System.Windows.Controls.ContextMenu(), (m, path) => {
                if (path != curPath) {
                    var i = new System.Windows.Controls.MenuItem();
                    var c = m.Items.Count + 1;
                    if (c >= 10) {
                        c = 0;
                    }
                    i.Header = $"_{c}: {Path.GetFileName(path)}";
                    i.Command = new GeCommand(() => {
                        OpenDB(path);
                    });
                    m.Items.Add(i);
                }
                return m;
            });
            if (menu.Items.Count > 0) {
                menu.Items.Add(new Separator());
            }
            var item = new System.Windows.Controls.MenuItem();
            item.Header = "_Select DB File";
            item.Command = new GeCommand(() => {
                OnOpenFile(null, null);
            }, true);
            menu.Items.Add(item);
            dbMenuButton.DropDownMenu = menu;
        }

        private void OnContentRendered(object sender, EventArgs e) {
            mFileListView.Focus();
            var item = mFileListView.ItemContainerGenerator.ContainerFromIndex(mFileListView.SelectedIndex) as System.Windows.Controls.ListViewItem;
            item?.Focus();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            mFileList.CurrentChanged -= PlayingItemChanged;
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e) {
            SaveCurrentSelection();
            WfGlobalParams.Instance.Placement.GetPlacementFrom(this);
            WfGlobalParams.Instance.Serialize();
        }

        #endregion

        #region Event Handlers

        private void PlayingItemChanged(int from, WfFileItem fromItem, int to, WfFileItem toItem) {
            mFileListView.SelectedIndex = to;
            mFileListView.ScrollIntoView(toItem);
        }

        private void OnFileItemSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            notify("ItemSelected");
            notify("ItemMultiSelected");
        }

        #endregion

        #region Command Handlers

        private void OnOpenFile(object sender, RoutedEventArgs e) {
            SelectDB();
        }

        private void OnRegisterFolder(object sender, RoutedEventArgs e) {
            RegisterFolder();
        }

        private void OnPlayAll(object sender, RoutedEventArgs e) {
            Play(true);
        }

        private void OnCreateTrimming(object sender, RoutedEventArgs e) {
            CreateTrimmingPattern();
        }

        private void OnSelectTrimming(object sender, RoutedEventArgs e) {
            ApplyTrimmingPattern();
        }

        private void OnResetTrimming(object sender, RoutedEventArgs e) {
            ResetTrimmingPattern();
        }

        private void OnResetCounter(object sender, RoutedEventArgs e) {
            ResetPlayedCounter();
        }

        private void OnSetRating(object sender, RoutedEventArgs e) {
            var btn = sender as System.Windows.Controls.Button;
            SetRating((Ratings)Convert.ToInt16(btn.Tag));
        }
        #endregion


        private void OnListItemDoubleClick(object sender, MouseButtonEventArgs e) {
            Play(false);
        }
        private WfSortKey HeaderName2SortKey(string name) {
            switch (name) {
                case "Size": return WfSortKey.SIZE;
                case "Name": return WfSortKey.NAME;
                case "Type": return WfSortKey.TYPE;
                case "Date": return WfSortKey.DATE;
                case "Last Play": return WfSortKey.LAST_PLAY;
                case "Count": return WfSortKey.PLAY_COUNT;
                case "Trimming": return WfSortKey.TRIMMING;
                case "Rating": return WfSortKey.RATING;
                case "Path": return WfSortKey.PATH;
                default: return WfSortKey.NONE;
            }
        }
        private string SortKey2HeaderName(WfSortKey key) {
            switch (key) {
                case WfSortKey.SIZE: return "Size";
                case WfSortKey.NAME: return "Name";
                case WfSortKey.TYPE: return "Type";
                case WfSortKey.DATE: return "Date";
                case WfSortKey.LAST_PLAY: return "Last Play";
                case WfSortKey.PLAY_COUNT: return "Count";
                case WfSortKey.TRIMMING: return "Trimming";
                case WfSortKey.RATING: return "Rating";
                case WfSortKey.PATH: return "Path";
                default: return "";
            }
        }

        private void OnHeaderClick(object sender, RoutedEventArgs e) {
            var header = e.OriginalSource as GridViewColumnHeader;
            if (null == header) {
                return;
            }
            WfSortKey key = HeaderName2SortKey(header.Content.ToString());
            if (key == WfSortKey.NONE) {
                return;
            }
            var prev = WfGlobalParams.Instance.SortInfo;
            var next = new WfSortInfo();
            next.Key = key;
            if (prev.Key == key) {
                next.Order = prev.Order == WfSortOrder.ASCENDING ? WfSortOrder.DESCENDING : WfSortOrder.ASCENDING;
            }
            ExecSort(next);
            UpdateColumnHeaderOnSort(next);
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject {
            if (depObj != null) {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T) {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child)) {
                        yield return childOfChild;
                    }
                }
            }
        }

        private Dictionary<WfSortKey, GridViewColumnHeader> mHeaderColumnDic = null;

        private void InitHeaderColumnDic() {
            if (null == mHeaderColumnDic) {
                mHeaderColumnDic = new Dictionary<WfSortKey, GridViewColumnHeader>(10);
                foreach (var header in FindVisualChildren<GridViewColumnHeader>(mFileListView)) {
                    Debug.WriteLine(header.ToString());
                    var textBox = FindVisualChildren<TextBlock>(header).FirstOrDefault();
                    if (null != textBox) {
                        var key = HeaderName2SortKey(textBox.Text);
                        if (key != WfSortKey.NONE) {
                            mHeaderColumnDic[key] = header;
                        }
                    }
                }
            }
        }

        private void UpdateColumnHeaderOnSort(WfSortInfo info) {
            InitHeaderColumnDic();
            foreach (var v in mHeaderColumnDic) {
                if (v.Key == info.Key && !info.Shuffle) {
                    v.Value.Tag = info.Order == WfSortOrder.ASCENDING ? "asc" : "desc";
                } else {
                    v.Value.Tag = null;
                }
            }
        }

        #region Operation / Function

        /**
         * ファイル選択ダイアログを開いてDBファイルを選択して開く
         */
        private void SelectDB() {
            var dlg = new CommonOpenFileDialog();
            dlg.DefaultExtension = ".wpd";
            dlg.RestoreDirectory = true;
            dlg.Filters.Add(new CommonFileDialogFilter("wfPlayer DB", "*.wpd"));
            dlg.Filters.Add(new CommonFileDialogFilter("All Files", "*.*"));
            if (dlg.ShowDialog(GetWindow(this)) == CommonFileDialogResult.Ok) {
                OpenDB(dlg.FileName);
            }
        }

        /**
         * 選択されたDBファイル名をタイトルに表示する
         */
        private void UpdateTitle() {
            var path = WfGlobalParams.Instance.FilePath;

            string fname = !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : "<untitled>";
            var v = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //Debug.WriteLine(v.ToString());
            this.Title = String.Format("{0} (v{1}.{2}.{3})  - {4}", v.ProductName, v.FileMajorPart, v.FileMinorPart, v.FileBuildPart, fname);
        }

        /**
         * フォルダーを指定して、DBに追加する
         */
        private void RegisterFolder() {
            using (var dlg = new CommonOpenFileDialog("Select Folder")) {
                dlg.IsFolderPicker = true;
                dlg.Multiselect = false;
                if (dlg.ShowDialog() == CommonFileDialogResult.Ok) {
                    RegisterFilesInPath(dlg.FileName);
                }
            }

            //using (var fbd = new FolderBrowserDialog())
            //{
            //    DialogResult result = fbd.ShowDialog();
            //    if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            //    {
            //        RegisterFilesInPath(fbd.SelectedPath);
            //    }
            //}
        }

        public void RefreshDB() {
            // ファイルがなくなっているアイテムをDBから削除
            //List<WfFileItem> removed = null;
            using (var retr = mPlayListDB.QueryAll(true)) {
                var removed = retr.List.Where((v) => !v.Exists);
                DeleteFilesInList(removed);
            }
            // リストからも削除
            // 追加ファイルがあれば登録
            foreach (var folder in mPlayListDB.ListTargetFolders()) {
                RegisterFilesInPath(folder);
            }
        }

        /**
         * 現在選択されているファイルから再生を開始する
         */
        private void Play(bool start) {
            if (mFileList.Count > 0) {
                Dispatcher.InvokeAsync(() => {
                    var player = new WfPlayerWindow();
                    mFileList.CurrentIndex = mFileListView.SelectedIndex;
                    player.SetSources(mFileList, start);
                    player.ShowDialog();
                    var item = mFileListView.ItemContainerGenerator.ContainerFromIndex(mFileListView.SelectedIndex) as System.Windows.Controls.ListViewItem;
                    item?.Focus();

                    if (player.RequestShutdown) {
                        Application.Current.Shutdown();
                        WinShutdown.Shutdown();
                    }
                });
            }
        }

        /**
         * 選択されているファイルの再生回数カウンターをクリアする
         */
        private void ResetPlayedCounter() {
            using (var txn = WfPlayListDB.Instance.Transaction()) {
                foreach (WfFileItem v in mFileListView.SelectedItems) {
                    v.PlayCount = 0;
                    v.SaveModified();
                }
            }
        }

        /**
         * カレントアイテムに、トリミングパターンを作成してセットする
         */
        private void CreateTrimmingPattern() {
            var item = mFileListView.SelectedItem as WfFileItem;
            if (null == item) {
                return;
            }
#if true
            mFileList.CurrentIndex = mFileListView.SelectedIndex;
            var tp = new WfTrimmingPlayer(mFileList);
            tp.ShowDialog();
#else
            var tp = new WfTrimmingPlayer(item.Trimming, WfTrimmingPlayer.GetRefPath(null, item.FullPath, true));
            WfTrimmingPlayer.ResultEventProc onNewTrimming = (result, db) =>
            {
                if (null != tp.Result)
                {
                    item.Trimming = tp.Result;
                    item.SaveModified();
                }
            };
            tp.OnResult += onNewTrimming;
            tp.ShowDialog();
            tp.OnResult -= onNewTrimming;
#endif
        }

        /**
         * トリミングパターンを選択して、選択アイテムに設定
         */
        private void ApplyTrimmingPattern() {
            var dlg = new WfTrimmingPatternList();
            dlg.ShowDialog();
            if (dlg.Result != null) {
                using (var txn = WfPlayListDB.Instance.Transaction()) {
                    foreach (WfFileItem v in mFileListView.SelectedItems) {
                        v.Trimming = dlg.Result;
                        v.SaveModified();
                    }
                }
            }
        }

        /**
         * 選択アイテムのトリミングパターンを解除する
         */
        private void ResetTrimmingPattern() {
            using (var txn = WfPlayListDB.Instance.Transaction()) {
                foreach (WfFileItem v in mFileListView.SelectedItems) {
                    v.Trimming = WfFileItem.Trim.NoTrim;
                    v.SaveModified();
                }
            }
        }

        /**
         * 選択ファイルのレイティングを変更する
         */
        private void SetRating(Ratings rating) {
            using (var txn = WfPlayListDB.Instance.Transaction()) {
                foreach (WfFileItem v in mFileListView.SelectedItems) {
                    v.Rating = rating;
                    v.SaveModified();
                }
            }
        }

        /**
         * DBファイルパスを指定してDBを開く（なければ作成される）
         */
        private void OpenDB(string dbPath) {
            if (mPlayListDB != null) {
                SaveCurrentSelection();
            }
            WfGlobalParams.Instance.FilePath = dbPath;
            WfGlobalParams.Instance.AddMru(dbPath);
            mPlayListDB = WfPlayListDB.CreateInstance(dbPath);
            LoadListFromDB();
            UpdateTitle();
            CreateFileMenu();
        }

        /**
         * DBからファイル一覧をロードする
         */
        private void LoadListFromDB() {
            if (mFileList == null) {
                SetFileList(new WfFileItemList());
            }

            var filter = "";
            if (IsFiltered) {
                filter = WfGlobalParams.Instance.Filter.SQL;
            }
            var orderBy = WfGlobalParams.Instance.SortInfo.SQL;
            var shuffle = WfGlobalParams.Instance.SortInfo.Shuffle;

            mFileList.Clear();
            InitSorter();
            using (var retriever = mPlayListDB.QueryAll(false, filter, orderBy)) {
                foreach (var item in retriever.List) {
                    mSorter.Add(item);
                }
            }
            if(shuffle) {
                Shuffle();
            }
            EnsureSelectItem();
        }

        /**
         * 前回のファイル選択状態を復元、または、先頭のファイルを選択する
         */
        private void EnsureSelectItem() {
            if (mFileList.Count > 0) {
                var path = mPlayListDB.GetValueAt("LastItem");
                int index = mFileList.IndexOfPath(path);
                if (index < 0) {
                    index = 0;
                }
                mFileListView.SelectedIndex = index;
                mFileListView.ScrollIntoView(mFileList[index]);
            }
        }

        /**
         * ListViewのデータソースとなるFileItemListを設定する
         */
        public void SetFileList(WfFileItemList newList) {
            if (mFileList != null) {
                mFileList.CurrentChanged -= PlayingItemChanged;
                mFileListView.DataContext = null;
                mFileList = null;
            }
            if (newList != null) {
                mFileList = newList;
                mFileList.CurrentChanged += PlayingItemChanged;
                mFileListView.DataContext = mFileList;
            }
        }

        /**
         * パスで指定されたディレクトリ以下に含まれる動画ファイルを列挙してDBに登録する
         */
        private void RegisterFilesInPath(string folderPath) {
            var videoExt = new[] { "mp4", "wmv", "avi", "mov", "avi", "mpg", "mpeg", "mpe", "ram", "rm" };

            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                           .Where(file => videoExt.Any(x => file.EndsWith(x, StringComparison.OrdinalIgnoreCase)));
            if (!files.Any()) {
                return;
            }
            using (var appender = mPlayListDB.BeginRegister(folderPath)) {
                foreach (var file in files) {
                    var f = new WfFileItem(file);
                    if (f.Exists) {
                        if (appender.Add(f)) {
                            mFileList.Add(f);
                        }
                    }
                }
            }
        }

        /**
         * 現在のファイル選択状態をDBに保存する
         */
        private void SaveCurrentSelection() {
            var sel = mFileListView?.SelectedItem as WfFileItem;
            if (sel != null) {
                mPlayListDB.SetValueAt("LastItem", sel.FullPath);
            }
        }
        #endregion

        #region Routed Commands

        public readonly static RoutedCommand CreateTrimming = new RoutedCommand("CreateTrimming", typeof(MainWindow));
        public readonly static RoutedCommand ApplyTrimming = new RoutedCommand("ApplyTrimming", typeof(MainWindow));
        public readonly static RoutedCommand ResetCounter = new RoutedCommand("ResetCounter", typeof(MainWindow));
        public readonly static RoutedCommand DeleteFiles = new RoutedCommand("DeleteFiles", typeof(MainWindow));

        private void CanCreateTrimming(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = mFileListView.SelectedIndex >= 0;
        }

        private void ExecCreateTrimming(object sender, ExecutedRoutedEventArgs e) {
            CreateTrimmingPattern();
            e.Handled = true;
        }

        private void CanApplyTrimming(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = mFileListView.SelectedIndex >= 0;
        }

        private void ExecApplyTrimming(object sender, ExecutedRoutedEventArgs e) {
            ApplyTrimmingPattern();
            e.Handled = true;
        }

        private void ExecResetCounter(object sender, ExecutedRoutedEventArgs e) {
            ResetPlayedCounter();
            e.Handled = true;
        }

        private void CanResetCounter(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = mFileListView.SelectedIndex >= 0;
        }

        private void ExecDeleteFiles(object sender, ExecutedRoutedEventArgs e) {
            var r = System.Windows.MessageBox.Show("Selected files will be removed.\r\n Are you sure?", "Delete Filies", MessageBoxButton.OKCancel);
            if (r == MessageBoxResult.OK) {
                DeleteFilesInList(CopiedSelectedFiles);
            }
            e.Handled = true;
        }

        private void CanDeleteFiles(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = mFileListView.SelectedIndex >= 0;
        }

        private void DeleteFilesInList(IEnumerable<WfFileItem> list) {
            //if (!list.Any()) {
            //    return;
            //}

            using (var remover = mPlayListDB.BeginRemove()) {
                foreach (WfFileItem v in list) {
                    remover.Remove(v.FullPath);
                    File.Delete(v.FullPath);
                    var idx = mFileList.IndexOfPath(v.FullPath);
                    if (idx >= 0) {
                        mFileList.RemoveAt(idx);
                    }
                }
            }
        }

        private List<WfFileItem> CopiedSelectedFiles {
            get {
                var list = new List<WfFileItem>(mFileListView.SelectedItems.Count);
                foreach (WfFileItem v in mFileListView.SelectedItems) {
                    list.Add(v);
                }
                return list;
            }
        }

        #endregion

        private void OnFilter(object sender, RoutedEventArgs e)
        {
            if (IsFiltered)
            {
                IsFiltered = false;
                LoadListFromDB();
            }
            else
            {
                var dlg = new WfFilterSetting(WfGlobalParams.Instance.Filter);
                dlg.ShowDialog();
                if (dlg.Result != null)
                {
                    WfGlobalParams.Instance.Filter = dlg.Result;
                    IsFiltered = true;
                    LoadListFromDB();
                }
            }
        }

        private void ExecSort(WfSortInfo next)
        {
            var prev = WfGlobalParams.Instance.SortInfo;
            if(prev == next)
            {
                return;
            }
            WfGlobalParams.Instance.SortInfo = next;
            if (next.Shuffle) {
                Shuffle();
                return;
            }
            if (prev.Key == next.Key && !prev.Shuffle)
            {
                if (prev.Order != next.Order)
                {
                    SaveCurrentSelection();
                    SetFileList(new WfFileItemList(mFileList.Reverse(), null));
                    EnsureSelectItem();
                }
            }
            else
            {
                if (next.IsExternalKey)
                {
                    InitSorter().Sort();
                }
                else
                {
                    LoadListFromDB();
                }
            }
        }

        private void Shuffle() {
            //Fisher-Yatesアルゴリズムでシャッフルする
            Random rnd = new Random();
            int n = mFileList.Count;
            while (n > 1) {
                n--;
                int k = rnd.Next(n + 1);
                var tmp = mFileList[k];
                mFileList[k] = mFileList[n];
                mFileList[n] = tmp;
            }
        }

        private void OnSort(object sender, RoutedEventArgs e)
        {
            var dlg = new WfSortSetting(WfGlobalParams.Instance.SortInfo);
            dlg.ShowDialog();
            if (dlg.Result != null)
            {
                ExecSort(dlg.Result);
            }
        }

        private int CompareInType(WfFileItem o1, WfFileItem o2)
        {
            int r = o1.Type.CompareTo(o2.Type);
            if(r==0)
            {
                r = o1.FullPath.CompareTo(o2.FullPath);
            }
            return r;
        }
        private int CompareInName(WfFileItem o1, WfFileItem o2)
        {
            int r = o1.Name.CompareTo(o2.Name);
            if (r == 0)
            {
                r = o1.FullPath.CompareTo(o2.FullPath);
            }
            return r;
        }

        private WfSorter<WfFileItem> InitSorter()
        {
            if(mSorter==null)
            {
                mSorter = new WfSorter<WfFileItem>();
            }
            var sortInfo = WfGlobalParams.Instance.SortInfo;
            IWfSorterComparator<WfFileItem> comparator = null;
            switch (sortInfo.Key)
            {
                case WfSortKey.NAME:
                    comparator = CompareInName;
                    break;
                case WfSortKey.TYPE:
                    comparator = CompareInType;
                    break;
                default:
                    break;
            }
            mSorter.Instraction(mFileList, sortInfo.Order, comparator, false);
            return mSorter;
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Return:
                    Play(false);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

    }
}
