using io.github.toyota32k.toolkit.view;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace wfPlayer.player {
    /// <summary>
    /// ChapterView.xaml の相互作用ロジック
    /// </summary>
    public partial class ChapterView : UserControl {
        public PlayerViewModel ViewModel => DataContext as PlayerViewModel;

        const double TICK_WIDTH = 2;

        private double PrevWidth = 0;

        public ChapterView() {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ViewModel?.Chapters.Subscribe(OnChapterListChanged);
            ViewModel?.DisabledRanges.Subscribe(OnDisabledRangesChanged);
        }


        private double Time2Position(ulong time) {
            var dur = ViewModel.Duration.Value;
            if (dur == 0) return 0;
            return this.ActualWidth * (double)time / (double)dur;
        }

        private void OnChapterListChanged(ChapterList list) {
            PrevWidth = ActualWidth;
            TickerView.Children.Clear();
            RangeView.Children.Clear();
            var duration = ViewModel.Duration.Value;
            if (duration == 0) return;
            if (list != null && duration > 0) {
                foreach (var c in list.Values) {
                    var pos = Math.Min(Time2Position(c.Position), ActualWidth - TICK_WIDTH);
                    var rc = new Rectangle() {
                        Width = TICK_WIDTH,
                        Fill = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(pos, 0, 0, 0)
                    };
                    TickerView.Children.Add(rc);
                }
            }
        }

        private void OnDisabledRangesChanged(List<PlayRange> list) {
            RangeView.Children.Clear();
            var duration = ViewModel.Duration.Value;
            if (list != null && duration > 0) {
                foreach (var r in list) {
                    var end = r.End == 0 ? duration : r.End;
                    var rc = new Rectangle() {
                        Width = Time2Position(end - r.Start),
                        Fill = new SolidColorBrush(Colors.Gray),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(Time2Position(r.Start), 0, 0, 0)
                    };
                    RangeView.Children.Add(rc);
                }
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            if(PrevWidth>0 && PrevWidth != ActualWidth) {
                OnChapterListChanged(ViewModel.Chapters.Value);
                OnDisabledRangesChanged(ViewModel.DisabledRanges.Value);
            }
        }
    }
}
