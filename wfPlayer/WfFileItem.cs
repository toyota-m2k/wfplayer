﻿using System;
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
        public WfFileItem(string path, long size, DateTime date, string mark, Ratings rating, bool exists, DateTime lastPlay, int playCount, Trim trimming)
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

        public enum Ratings
        {
            GOOD = 0,       // 優良
            NORMAL,         // ふつう
            SKIP,           // 一覧に表示しても再生はしない
            DELETING,       // 削除予定
        }
        private Ratings mRating = Ratings.NORMAL;
        public Ratings Rating
        {
            get => mRating;
            set { if (setProp("Rating", ref mRating, value)) { mDirty |= (long)WfPlayListDB.FieldFlag.RATING; } }
        }

        public class Trim
        {
            public long Id { get; }
            public string Name { get; }
            public TimeSpan Prologue { get; }
            public TimeSpan Epilogue { get; }

            public Trim(long id, string name, TimeSpan prologue, TimeSpan epilogue)
            {
                Id = id;
                Name = name;
                Prologue = prologue;
                Epilogue = epilogue;
            }

            public static Trim NoTrim { get; } = new Trim(0, "", TimeSpan.Zero, TimeSpan.Zero);

            public bool IsEmpty() => Id == 0;
            public bool HasValue() => !IsEmpty();

            public override bool Equals(object obj)
            {
                var s = obj as Trim;
                if(null==s)
                {
                    return false;
                }
                return Id == s.Id && Name == Name && Prologue.TotalMilliseconds == Prologue.TotalMilliseconds && Epilogue.TotalMilliseconds == Epilogue.TotalMilliseconds;
            }

            public override string ToString()
            {
                return $"${Name}({Id}):{Prologue}/{Epilogue}";
            }

            public override int GetHashCode()
            {
                return ToString().GetHashCode();
            }
            #endregion
        }

        private Trim mTrimming = Trim.NoTrim;
        public Trim Trimming
        {
            get => mTrimming;
            set { if (setProp("Trimming", ref mTrimming, value)) { mDirty |= (long)WfPlayListDB.FieldFlag.TRIMMING; } }
        }

        public void UpdateDB(WfPlayListDB db)
        {
            if(mDirty==0)
            {
                return;
            }
            db.UpdatePlaylistItem(this, mDirty);
            mDirty = 0;
        }
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

        public void SetCurrentByPath(string path)
        {
            for(int i=0; i<Count; i++)
            {
                if(this[i].FullPath == path)
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
