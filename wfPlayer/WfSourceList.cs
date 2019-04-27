using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wfPlayer
{
    public interface IWfSource
    {
        Uri Uri { get; }
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
