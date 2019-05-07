using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wfPlayer
{
    public enum WfSortOrder
    {
        ASCENDING  = 1,
        DESCENDING = -1,
    }

    public delegate int IWfSorterComparator<T>(T o1, T o2);
    public struct WfSorterFindResult
    {
        public int Hit;
        public int Prev;
        public int Next;

        public WfSorterFindResult(int hit=-1, int prev=-1, int next=-1)
        {
            Hit = hit;
            Prev = prev;
            Next = next;
        }

        public void Reset()
        {
            Hit = -1;
            Prev = -1;
            Next = -1;
        }
    }

    public interface IWfSorter<T>
    {
        IWfSorterComparator<T> Comparator { get; set; }
        WfSortOrder Order { get; set; }

        int Find(T element, ref WfSorterFindResult detail);
        int Add(T element);
    }

    public class WfSorter<T> : IWfSorter<T>
    {
        private IWfSorterComparator<T> mComparator { get; set; }
        private IList<T> mList = null;
        private WfSortOrder mOrder;
        private int Count => mList?.Count ?? 0;

        public bool AllowDuplication { get; set; }
        
        public IList<T> List
        {
            get => mList;
            set
            {
                mList = value;
                if (Count > 0)
                {
                    Sort();
                }
            }
        }

        public IWfSorterComparator<T> Comparator
        {
            get => mComparator;
            set
            {
                mComparator = value;
                if (mList.Count > 0)
                {
                    Sort();
                }
            }
        }

        public WfSortOrder Order
        {
            get => mOrder;
            set
            {
                if(mOrder!=value)
                {
                    mOrder = value;
                    if(Count>0)
                    {
                        Sort();
                    }
                }
            }
        }

        public WfSorter(bool allowDuplication=true)
        {
            AllowDuplication = allowDuplication;
        }

        public WfSorter(IList<T> list, WfSortOrder order, IWfSorterComparator<T> comparator, bool allowDuplication = true)
        {
            AllowDuplication = allowDuplication;
            Instraction(list, order, comparator, false);
        }

        public void Instraction(IList<T> list, WfSortOrder order, IWfSorterComparator<T> comparator, bool execSort)
        {
            mOrder = order;
            mList = list;
            mComparator = comparator;
            if (execSort)
            {
                Sort();
            }
        }


        private WfSorterFindResult mPos = new WfSorterFindResult();
        public int Add(T element)
        {
            if(Comparator==null)
            {
                mList.Add(element);
                return mList.Count - 1;
            }

            if (Find(element, ref mPos) >= 0 && !AllowDuplication)
            {
                return -1;
            }

            if (mPos.Next < 0)
            {
                mList.Add(element);
                return mList.Count - 1;
            }
            else
            {
                mList.Insert(mPos.Next, element);
                return mPos.Next;
            }
        }

        public int Find(T element, ref WfSorterFindResult detail)
        {
            detail.Reset();

            int count = mList.Count;
            int s = 0;
            int e = count - 1;
            int m = 0;
            if (e < 0)
            {
                // 要素が空
                return -1;
            }

            if (Comparator(mList[e], element) < 0)
            {
                // 最後の要素より後ろ
                detail.Prev = e;
                return -1;
            }

            while (s <= e)
            {
                m = (s + e) / 2;
                T v = mList[m];
                int cmp = Comparator(v, element);
                if (cmp == 0)
                {
                    detail.Hit = m;
                    detail.Prev = m - 1;
                    if (m < count - 1)
                    {
                        detail.Next = m + 1;
                    }
                    return m;     // 一致する要素が見つかった
                }
                else if (cmp < 0)
                {
                    s = m + 1;
                }
                else
                {
                    e = m - 1;
                }
            }
            detail.Next = s;
            detail.Prev = s - 1;
            return -1;
        }

        public void Sort()
        {
            if(Comparator==null || List==null || Count==0)
            {
                return;
            }
            ShellSort(0, mList.Count - 1);
        }

        private void ShellSort(int start, int end)
        {
            int h, i, j;
            int size = end - start + 1;
            if (size < 2)
                return;

            int nLim = size / 9;
            for (h = 1; h < nLim; h = h * 3 + 1)
                ;
            for (; h > 0; h /= 3)
            {
                for (i = h; i < size; i++)
                {
                    j = i;
                    while (j >= h && (int)Order * Comparator(mList[j - h + start], mList[j + start]) > 0)
                    {
                        Swap(j + start, j - h + start);
                        j -= h;
                    }
                }
            }

        }
        private void Swap(int n1, int n2)
        {
            var v = mList[n1];
            mList[n1] = mList[n2];
            mList[n2] = v;
        }
    }
}
