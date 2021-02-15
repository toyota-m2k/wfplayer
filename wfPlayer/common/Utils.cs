using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace common
{
    public static class Utils {
        public static T GetValue<T>(this WeakReference<T> w) where T : class {
            return w.TryGetTarget(out T o) ? o : null;
        }

        public static V GetValue<K,V>(this Dictionary<K,V> dic, K key, V def=default) {
            return dic.TryGetValue(key, out var v) ? v : def;
        }

        public static bool IsNullOrEmpty<T>(IEnumerable<T> v) {
            return !(v?.Any() ?? false);
        }

        public static DependencyObject FindChild(DependencyObject root, string name, Type type) {
            for(int i=0, ci=VisualTreeHelper.GetChildrenCount(root); i<ci; i++) {
                var ch = VisualTreeHelper.GetChild(root, i) as FrameworkElement;
                if(ch!=null && ch.GetType()==type && ch.Name == name) {
                    return ch;
                }
                var sub = FindChild(ch, name, type);
                if(null!=sub) {
                    return sub;
                }
            }
            return null;
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject {
            if (depObj != null) {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T) {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child)) {
                        yield return childOfChild;
                    }
                }
            }
        }

        public static T Apply<T>(this T obj, Action<T> fn) where T : class {
            fn(obj);
            return obj;
        }
        public static R Run<T,R>(this T obj, Func<T,R> fn) where T : class {
            return fn(obj);
        }

        public static IEnumerable<T> ToEnumerable<T>(this System.Collections.IList list) {
            foreach(var o in list) {
                yield return (T)o;
            }
        }

        public static IEnumerable<T> ToSingleEnumerable<T>(this T item) {
            yield return item;
        }

        public static T[] Array<T>(params T[] args) {
            return args;
        }

        // ありそうでないメソッド
        public static bool IsEmpty(this String s) {
            return s.Length == 0;
        }

        public static void ScrollToTail(this ListView lv) {
            int lastIndex = lv.Items.Count - 1;
            if (lastIndex >= 0) {
                lv.ScrollIntoView(lv.Items[lastIndex]);
            }
        }

        public static bool ContainsIgnoreCase(this string s, string partialText) {
            return s.IndexOf(partialText, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }
    }
}
