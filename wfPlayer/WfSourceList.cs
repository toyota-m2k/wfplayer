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
        SKIP,           // 一覧に表示しても再生はしない
        DELETING,       // 削除予定
    }

    public interface ITrim
    {
        long Id { get; }
        string Name { get; }
        TimeSpan Prologue { get; }
        TimeSpan Epilogue { get; }
    }

    public interface IWfSource
    {
        Uri Uri { get; }
        string Mark { get; set; }
        Ratings Rating { get; set; }
        ITrim Trimming { get; set; }
        void OnPlayStarted();
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
