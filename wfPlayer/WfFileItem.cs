using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

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

        public WfFileItem(string path)
        {
            FullPath = path;
            var info = new FileInfo(path);
            Exists = info.Exists;
            Size = info.Length;
            Date = info.CreationTimeUtc;
            mRating = Ratings.NORMAL;
        }
        public WfFileItem(string path, long size, DateTime date, string mark, Ratings rating, bool exists, DateTime lastPlay, int playCount, Trim trimming, WfAspect aspect)
        {
            FullPath = path;
            Size = size;
            Date = date;
            mMark = mark;
            mRating = rating;
            Exists = exists;

            LastPlayDate = lastPlay;
            PlayCount = playCount;
            Trimming = trimming;
            Aspect = aspect;
            mDirty = 0;
        }

        #region RO Properties

        public bool Exists { get; }

        public string Name => Path.GetFileNameWithoutExtension(FullPath);

        public string Type => Path.GetExtension(FullPath);

        public string FullPath { get; }

        public Uri Uri => new Uri(FullPath);

        public long Size { get; }

        public DateTime Date { get; }

        #endregion 
        #region RW Properties

        private long mDirty = 0;

        private string mMark = "";
        public string Mark
        {
            get => mMark;
            set { if (setProp("Mark", ref mMark, value)) { mDirty |= (long)WfPlayListDB.FieldFlag.MARK; } }
        }

        private DateTime mLastPlayDate = DateTime.MinValue;
        public DateTime LastPlayDate
        {
            get => mLastPlayDate;
            set { if(setProp("LastPlayDate", ref mLastPlayDate, value)) { mDirty |= (long)WfPlayListDB.FieldFlag.LAST_PLAY; } }
        }

        private int mPlayCount = 0;
        public int PlayCount
        {
            get => mPlayCount;
            set { if (setProp("PlayCount", ref mPlayCount, value)) { mDirty |= (long)WfPlayListDB.FieldFlag.PLAY_COUNT; } }
        }

        private Ratings mRating = Ratings.NORMAL;
        public Ratings Rating
        {
            get => mRating;
            set { if (setProp("Rating", ref mRating, value)) { mDirty |= (long)WfPlayListDB.FieldFlag.RATING; } }
        }

        private WfAspect mAspect = WfAspect.AUTO;
        public WfAspect Aspect
        {
            get => mAspect;
            set { if(setProp("Aspect", ref mAspect, value)) { mDirty |= (long)WfPlayListDB.FieldFlag.ASPECT; } }
        }

        public class Trim : ITrim
        {
            public long Id { get; }
            public string Name { get; }
            public long Prologue { get; }
            public long Epilogue { get; }
            public string RefPath { get; }

            public Trim(long id, string name, long prologue, long epilogue, string refPath)
            {
                Id = id;
                Name = name;
                Prologue = prologue;
                Epilogue = epilogue;
                RefPath = refPath;
            }

            public static Trim NoTrim { get; } = new Trim(0, "", 0, 0, "");

            public bool IsEmpty => Id == 0;
            public bool HasValue => !IsEmpty;

            public override bool Equals(object obj)
            {
                var s = obj as Trim;
                if(null==s)
                {
                    return false;
                }
                return Id == s.Id && Name == s.Name && Prologue == s.Prologue && Epilogue == s.Epilogue;
            }

            public override string ToString()
            {
                return $"{Name}({Id}):{Prologue}/{Epilogue}";
            }

            public override int GetHashCode()
            {
                return ToString().GetHashCode();
            }
            #endregion
        }

        private ITrim mTrimming = Trim.NoTrim;
        public ITrim Trimming
        {
            get => mTrimming;
            set { if (setProp("Trimming", ref mTrimming, value)) { mDirty |= (long)WfPlayListDB.FieldFlag.TRIMMING; } }
        }

        bool mPlayCountModified = false;
        public void Touch()
        {
            if(!mPlayCountModified)
            {
                mPlayCountModified = true;
                PlayCount++;
            }
            LastPlayDate = DateTime.UtcNow;
        }

        public void SaveModified()
        {
            if(mDirty==0)
            {
                return;
            }
            WfPlayListDB.Instance.UpdatePlaylistItem(this, mDirty);
            mDirty = 0;
        }
    }

    public class WfFileItemList : ObservableCollection<WfFileItem>, IWfSourceList
    {
        public WfFileItemList()
        {
        }

        public WfFileItemList(IEnumerable<WfFileItem> src, string initialSelctPath)
            : base(src)
        {
            SetCurrentByPath(initialSelctPath);
        }

        public delegate void CurrentChangedHandler(int from, WfFileItem fromItem, int to, WfFileItem toItem);
        public event CurrentChangedHandler CurrentChanged;

        private int mCurrentIndex = -1;
        public int CurrentIndex
        {
            get => Count==0 ? -1 : mCurrentIndex;
            set
            {
                if(mCurrentIndex != value)
                {
                    if (0 <= value && value < Count)
                    {
                        int org = mCurrentIndex;
                        var orgItem = Current;
                        mCurrentIndex = value;
                        CurrentChanged?.Invoke(org, (WfFileItem)orgItem, mCurrentIndex, (WfFileItem)Current);
                        if(orgItem!=null)
                        {
                            orgItem.SaveModified();
                        }
                    }
                }
            }
        }

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

        public int IndexOfPath(string path)
        {
            if (null != path)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (this[i].FullPath == path)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        public void SetCurrentByPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                int i = IndexOfPath(path);
                if (i >= 0)
                {
                    CurrentIndex = i;
                }
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
