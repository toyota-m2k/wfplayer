using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
            LoadListFromDB();

            mFileListView.DataContext = mFileList;
            var path = mPlayListDB.GetValueAt("LastItem");
            int index = mFileList.IndexOfPath(path);
            mFileListView.SelectedIndex = (index >= 0) ? index : 0;
 
            //ListVideosInFolder(@"F:\mitsuki\private\movie\selected-25\www.moviejap.com");
        }
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnRegisterFolder(object sender, RoutedEventArgs e)
        {
            //var dlg = new OpenFileDialog();
            //dlg.Filter= "全てのファイル(*.*)|*.*";
            //dlg.Multiselect = true;
            //if (dlg.ShowDialog() == true)
            //{
            //    var page = new WfPlayerWindow();
            //    page.SetSources(dlg.FileNames);
            //    page.Show();
            //    //page.Closed += (s, x) =>
            //    //{
            //    //    Show();
            //    //};

            //    //Hide();
            //}

            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    RegisterFilesInPath(fbd.SelectedPath);
                }
            }
        }

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

        private void OnPlayAll(object sender, RoutedEventArgs e)
        {
            if(mFileList.Count>0)
            {
                var page = new WfPlayerWindow();
                mFileList.CurrentIndex = mFileListView.SelectedIndex;
                page.SetSources(mFileList);
                page.Show();
                page.Closed += (s, x) =>
                {
                    var c = mFileList.Current;
                    if(null!=c)
                    {
                        mPlayListDB.SetValueAt("LastItem", ((WfFileItem)c).FullPath);
                    }
                };
            }
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

    }
}
