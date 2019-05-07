using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wfPlayer
{
    public enum WfSortOrder
    {
        ASCENDING,
        DESCENDING,
    }

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
    }

    public class WfSortInfo
    {
        public WfSortOrder Order { get; set; } = WfSortOrder.ASCENDING;
        public WfSortKey Key { get; set; } = WfSortKey.NONE;
        
        public WfSortInfo()
        {

        }

        public WfSortInfo Clone()
        {
            var r = new WfSortInfo();
            r.Order = this.Order;
            r.Key = this.Key;
            return r;
        }
    }
}
