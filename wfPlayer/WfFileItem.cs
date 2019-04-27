using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wfPlayer
{
    public class WfFileItem : INotifyPropertyChanged, IUtPropertyChangedNotifier, IWfSource
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

        private UtObservableProperty<string> mMark;
        public string Mark
        {
            get => mMark.Value;
            set => mMark.Value = value;
        }

        public WfFileItem(string path)
        {
            FullPath = path;
            var info = new FileInfo(path);
            Exists = info.Exists;
            Size = info.Length;
            Date = info.CreationTimeUtc;
            mMark = new UtObservableProperty<string>("Mark", "", this);
        }

        public bool Exists { get; }

        public string Name => Path.GetFileNameWithoutExtension(FullPath);

        public string Type => Path.GetExtension(FullPath);

        public string FullPath { get; }

        public long Size { get; }

        public DateTime Date { get; }

        public Uri Uri => new Uri(FullPath);
    }

    public class WfFileItemList : ObservableCollection<WfFileItem>, IWfSourceList
    {
        private int CurrentIndex;

        public bool HasNext => CurrentIndex + 1 < Count;

        public bool HasPrev => 0 < CurrentIndex;

        public IWfSource Current => (0 <= CurrentIndex && CurrentIndex < Count) ? this[CurrentIndex] : null;

        public IWfSource Next => (HasNext) ? this[++CurrentIndex] : null;

        public IWfSource Prev => (HasPrev) ? this[--CurrentIndex] : null;

        public IWfSource Head
        {
            get
            {
                CurrentIndex = 0;
                return Current;
            }
        }

        //protected override void ClearItems()
        //{
        //    CurrentIndex = 0;
        //    base.ClearItems();
        //}
        //protected override void InsertItem(int index, WfFileItem item)
        //{
        //    if(index<=CurrentIndex)
        //    {
        //        CurrentIndex++;
        //    }
        //    base.InsertItem(index, item);
        //}
        //protected override void MoveItem(int oldIndex, int newIndex)
        //{
        //    base.MoveItem(oldIndex, newIndex);
        //    if(oldIndex==CurrentIndex)
        //    {
        //        CurrentIndex = newIndex;
        //    }
        //    else if(oldIndex<CurrentIndex)
        //    {
        //        if(newIndex>CurrentIndex)
        //        {
        //            CurrentIndex--;
        //        }
        //    }
        //    else
        //    {
        //        if(newIndex<=CurrentIndex)
        //        {
        //            CurrentIndex++;
        //        }
        //    }
        //}
        //protected override void RemoveItem(int index)
        //{
        //    base.RemoveItem(index);
        //    if(index<CurrentIndex)
        //    {
        //        CurrentIndex--;
        //    }
        //}
    }
}
