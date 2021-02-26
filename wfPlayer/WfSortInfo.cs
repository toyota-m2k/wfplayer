using System.ComponentModel;
using System.Text;

namespace wfPlayer
{
    public enum WfSortKey
    {
        NONE,
        NAME,
        PATH,
        TYPE,
        RATING,
        PLAY_COUNT,
        LAST_PLAY,
        DATE,
        SIZE,
        TRIMMING,
        ID,
    }

    public class WfSortInfo : INotifyPropertyChanged
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

        private WfSortOrder mOrder = WfSortOrder.ASCENDING;
        private WfSortKey mKey = WfSortKey.NONE;
        private bool mShuffle = false;
        public WfSortOrder Order
        {
            get => mOrder;
            set => setProp("Order", ref mOrder, value);
        }
        public WfSortKey Key
        {
            get => mKey;
            set => setProp("Key", ref mKey, value);
        }
        public bool Shuffle {
            get => mShuffle;
            set => setProp("Shuffle", ref mShuffle, value);
        }
        
        public WfSortInfo()
        {

        }
        public WfSortInfo(WfSortKey key, WfSortOrder order, bool shuffle)
        {
            mKey = key;
            mOrder = order;
            mShuffle = shuffle;
        }

        public WfSortInfo Clone()
        {
            var r = new WfSortInfo();
            r.Order = this.Order;
            r.Key = this.Key;
            r.Shuffle = this.Shuffle;
            return r;
        }

        public override bool Equals(object obj)
        {
            var s = obj as WfSortInfo;
            if(s==null)
            {
                return false;
            }
            return s.Key == Key && s.Order == Order && s.Shuffle == Shuffle;
        }

        public override int GetHashCode()
        {
            var hashCode = 740128291;
            hashCode = hashCode * -1521134295 + Order.GetHashCode();
            hashCode = hashCode * -1521134295 + Key.GetHashCode();
            hashCode = hashCode * -1521134295 + Shuffle.GetHashCode();
            return hashCode;
        }

        // DBのソートが使えないキーか？
        [System.Xml.Serialization.XmlIgnore]
        public bool IsExternalKey => Key == WfSortKey.NAME || Key == WfSortKey.TYPE || Key == WfSortKey.ID;

        [System.Xml.Serialization.XmlIgnore]
        public string SQL
        {
            get
            {
                var sb = new StringBuilder(" ORDER BY ");
                var needsSubKey = true;
                switch(Key)
                {
                    case WfSortKey.DATE:
                        sb.Append("date");
                        break;
                    case WfSortKey.LAST_PLAY:
                        sb.Append("lastPlay");
                        break;
                    case WfSortKey.PATH:
                        sb.Append("path");
                        needsSubKey = false;
                        break;
                    case WfSortKey.PLAY_COUNT:
                        sb.Append("playCount");
                        break;
                    case WfSortKey.RATING:
                        sb.Append("rating");
                        break;
                    case WfSortKey.SIZE:
                        sb.Append("size");
                        break;
                    case WfSortKey.TRIMMING:
                        sb.Append("trim_start,trim_end");
                        break;

                    case WfSortKey.TYPE:
                    case WfSortKey.NAME:
                    case WfSortKey.NONE:
                    default:
                        sb.Append("id");
                        needsSubKey = false;
                        break;
                }
                if(needsSubKey)
                {
                    sb.Append(", path");
                }
                if(Order==WfSortOrder.ASCENDING)
                {
                    sb.Append(" ASC ");
                }
                else
                {
                    sb.Append(" DESC ");
                }
                return sb.ToString();
            }
        }
    }
}
