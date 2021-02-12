using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wfPlayer
{
    public enum Ratings
    {
        GOOD = 0,       // 優良
        NORMAL,         // ふつう
        BAD,           // 一覧に表示しても再生はしない
        DREADFUL,       // 削除予定
    }

    public enum WfAspect
    {
        AUTO = 0,
        CUSTOM125,      // 5:4
        CUSTOM133,      // 4:3
        CUSTOM150,      // 3:2
        CUSTOM177,      // 16:9
    }

    public interface ITrim
    {
        long Id { get; }
        string Name { get; }
        long Prologue { get; }
        long Epilogue { get; }
        string RefPath { get; }
        bool HasValue { get; }
        string RangeLabel { get; }
    }

    public interface IWfSource
    {
        Uri Uri { get; }
        string FullPath { get; }
        string Mark { get; set; }
        Ratings Rating { get; set; }
        //ITrim Trimming { get; set; }
        bool HasTrimming { get; }
        long TrimStart { get; set; }
        long TrimEnd { get; set; }
        WfAspect Aspect { get; set; }
        void Touch();
        void SaveModified();
    }

    public interface IWfSourceList
    {
        bool HasNext { get; }
        bool HasPrev { get; }

        IWfSource Current { get; }
        IWfSource Next { get; }
        IWfSource Prev { get; }
        IWfSource Head { get; }

    }

}
