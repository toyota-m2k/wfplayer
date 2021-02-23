using common;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace wfPlayer {
    public class RangeSliderViewModel : MicViewModelBase {
        public ReadOnlyReactiveProperty<double> Duration { get; }
        public ReactiveProperty<double> TrimStart { get; } = new ReactiveProperty<double>();
        public ReactiveProperty<double> TrimEnd { get; } = new ReactiveProperty<double>();

        public ReactiveProperty<double> TotalWidth { get; } = new ReactiveProperty<double>();
        public ReadOnlyReactiveProperty<double> StartWidth { get; }
        public ReadOnlyReactiveProperty<double> EndWidth { get; }

        public ReadOnlyReactiveProperty<double> TrimStartPosition { get; }
        public ReadOnlyReactiveProperty<double> TrimEndPosition { get; }

        public ReadOnlyReactiveProperty<string> TrimStartText { get; }
        public ReadOnlyReactiveProperty<string> TrimEndText { get; }

        public RangeSliderViewModel(ReadOnlyReactiveProperty<double> duration) {
            Duration = duration;
            StartWidth = TotalWidth.CombineLatest(Duration, TrimStart, (width, dur, start) => {
                var w = (width - RangeSlider.KNOB_POS_MARGIN * 2) * start / dur;
                Debug.WriteLine($"TrimStart:{w:#.#}  (TW={width:#.#},d={dur},s={start})");
                return (width - RangeSlider.KNOB_POS_MARGIN * 2) * start / dur;
            }).ToReadOnlyReactiveProperty();
            EndWidth = TotalWidth.CombineLatest(Duration, TrimEnd, (width, dur, end) => {
                var w = (width - RangeSlider.KNOB_POS_MARGIN * 2) * end / dur;
                Debug.WriteLine($"TrimEnd  :{w:#.#}  (TW={width:#.#},d={dur},s={end})");
                return (width - RangeSlider.KNOB_POS_MARGIN * 2) * end / dur;
            }).ToReadOnlyReactiveProperty();

            TrimStartPosition = TrimStart.ToReadOnlyReactiveProperty();
            TrimEndPosition = TrimEnd.CombineLatest(Duration, (end, dur) => {
                return dur - end;
            }).ToReadOnlyReactiveProperty();

            TrimStartText = TrimStart.Select((v) => $"{(long)(v/1000.0)}").ToReadOnlyReactiveProperty();
            TrimEndText = TrimEndPosition.Select((v) => $"{(long)(v/1000.0)}").ToReadOnlyReactiveProperty();
        }

        public bool CheckAndSetTrimStart(double value) {
            if(0<=value && value<TrimEndPosition.Value) {
                TrimStart.Value = value;
                return true;
            }
            return false;
        }
        public bool CheckAndSetTrimEnd(double value) {
            if (TrimStartPosition.Value <= value && value <= Duration.Value) {
                TrimEnd.Value = Duration.Value - value;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// RangeSlider.xaml の相互作用ロジック
    /// </summary>
    public partial class RangeSlider : UserControl {
        public const double KNOB_POS_MARGIN = 12.0;

        public RangeSlider() {
            InitializeComponent();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            ViewModel?.TotalWidth?.Apply((it) => {
                it.Value = e.NewSize.Width;
            });
        }

        public RangeSliderViewModel ViewModel {
            get => (DataContext as WfPlayerViewModel)?.RangeSliderViewModel;
        }


        //public RangeSliderViewModel ViewModel {
        //    get => DataContext as RangeSliderViewModel;
        //    set {
        //        ViewModel?.Dispose();
        //        DataContext = value;
        //    }
        //}
    }
}
