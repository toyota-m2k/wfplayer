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

        public MainWindow()
        {
            InitializeComponent();

        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            mFileList = new WfFileItemList();
            mFileListView.DataContext = mFileList;
            ListVideosInFolder(@"F:\mitsuki\private\movie\selected-25\www.moviejap.com");
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
                    ListVideosInFolder(fbd.SelectedPath);
                }
            }
        }

        private void ListVideosInFolder(string folderPath)
        {
            var videoExt = new[] { "mp4", "wmv", "avi", "mov", "avi", "mpg", "mpeg", "mpe", "ram", "rm" };

            mFileList.Clear();
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                           .Where(file => videoExt.Any(x => file.EndsWith(x, StringComparison.OrdinalIgnoreCase)));

            foreach (var file in files)
            {
                var f = new WfFileItem(file);
                if (f.Exists)
                {
                    mFileList.Add(f);
                }
            }
        }

        private void OnPlayAll(object sender, RoutedEventArgs e)
        {
            if(mFileList.Count>0)
            {
                var page = new WfPlayerWindow();
                page.SetSources(mFileList);
                page.Show();
            }
        }
    }
}
