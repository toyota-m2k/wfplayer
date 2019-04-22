using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wfPlayer
{
    public interface IUtPropertyChangedNotifier
    {
        void NotifyPropertyChanged(string propName);
    }

    public class UtObservableProperty<T> where T: IComparable<T>
    {
        private WeakReference<IUtPropertyChangedNotifier> mNotifier;
        private IUtPropertyChangedNotifier Notifier
        {
            get
            {
                IUtPropertyChangedNotifier target = null;
                if(mNotifier?.TryGetTarget(out target) ?? false)
                {
                    return target;
                }
                else
                {
                    return null;
                }
            }
        }

        private T mValue;
        private string mPropName;

        private void Notify()
        {
            Notifier?.NotifyPropertyChanged(mPropName);
        }

        public UtObservableProperty(string name, T initialValue, IUtPropertyChangedNotifier notifier)
        {
            mPropName = name;
            mValue = initialValue;
            mNotifier = new WeakReference<IUtPropertyChangedNotifier>(notifier);
        }

        public T Value
        {
            get => mValue;
            set
            {
                if(value.CompareTo(mValue)!=0)
                {
                    mValue = value;
                    Notify();
                }
            }
        }
    }
}
