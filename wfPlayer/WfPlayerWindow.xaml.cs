using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace wfPlayer
{
    public enum WfStretchMode
    {
        UniformToFill,
        Uniform,
        Fill,
        CUSTOM125,
        CUSTOM133,
        CUSTOM150,
        CUSTOM177,
    }

    /// <summary>
    /// WfPlayerWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class WfPlayerWindow : Window, INotifyPropertyChanged, IUtPropertyChangedNotifier
    {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private bool setProp<T>(string name, ref T field, T value, params string[] familyProperties)
        {
            if (!field.Equals(value))
            {
                field = value;
                notify(name);
                foreach (var p in familyProperties)
                {
                    notify(p);
                }
                return true;
            }
            return false;
        }

        //private bool setProp<T>(string[] names, ref T field, T value)
        //{
        //    if (!field.Equals(value))
        //    {
        //        field = value;
        //        foreach (var name in names)
        //        {
        //            notify(name);
        //        }
        //        return true;
        //    }
        //    return false;
        //}

        public void NotifyPropertyChanged(string propName)
        {
            notify(propName);
        }

        #endregion

        #region Binding Properties

        ///**
        // * VideoSource --> MediaElement.Source
        // */
        //public static readonly DependencyProperty VideoSourceProperty = DependencyProperty.Register("VideoSource", typeof(Uri), typeof(WfPlayerWindow));
        //public Uri VideoSource
        //{
        //    get => GetValue(VideoSourceProperty) as Uri;
        //    set =>SetValue(VideoSourceProperty, value);
        //}

        /**
         * Duration --> mPositionSlider.Value
         */
        public static readonly DependencyProperty DurationProperty = DependencyProperty.Register("Duration", typeof(double), typeof(WfPlayerWindow));
        public double Duration
        {
            get => (double)GetValue(DurationProperty);
            set
            {
                SetValue(DurationProperty, value);
                notify("LargePositionChange");
                notify("SmallPositionChange");
            }
        }
        /**
         * mPositionSlider.LargeChange/SmallChange
         */
        public double LargePositionChange => Duration / 10;
        public double SmallPositionChange => 1000;

        /**
         * Volume <--> Volume Slider.Value / MediaElement.Volume
         */
        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register("Volume", typeof(double), typeof(WfPlayerWindow), new PropertyMetadata(0.5));
        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        /**
         * Mute <--> Mute Button --> MediaElement.Volume
         */
        public static readonly DependencyProperty MuteProperty = DependencyProperty.Register("Mute", typeof(bool), typeof(WfPlayerWindow), new PropertyMetadata(false));
        public bool Mute
        {
            get => (bool)GetValue(MuteProperty);
            set => SetValue(MuteProperty, value);
        }

        /**
         * Speed <--> Speed Slider.Value
         *        --> SpeedPropertyChangedCallback --> MediaElement.SpeedRatio (このプロパティはDepandencyPropertyではないのでバインドできない）
         */
        public static readonly DependencyProperty SpeedProperty = DependencyProperty.Register("Speed", typeof(double), typeof(WfPlayerWindow), new PropertyMetadata((double)0.5, SpeedPropertyChangedCallback));
        public double Speed
        {
            get => (double)GetValue(SpeedProperty);
            set => SetValue(SpeedProperty, value);
        }

        /**
         * DependencyPropertyではないMediaElement.SpeedRatioに、スライダーから変更された値をセットするための仕掛け
         */
        private static void SpeedPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as WfPlayerWindow)?.OnSpeedChanged((double)e.NewValue);
        }
        private double calcSpeedRatio(double speed)
        {
            if (speed >= 0.5)
            {
                return 1 + (speed - 0.5) * 2;       // 1 ～ 2
            }
            else
            {
                return 0.2 + 0.8 * (speed * 2);     // 0.2 ～ 1
            }
        }
        private void OnSpeedChanged(double newValue)
        {
            if (mMediaElement != null)
            {
                mMediaElement.SpeedRatio = calcSpeedRatio(newValue);
            }
        }

        public bool Ready
        {
            get => mVideoLoadingTaskSource == null;
        }


        /**
         * 再生中 or not
         */
        private UtObservableProperty<bool> mStarted;
        public bool Started
        {
            get => mStarted.Value;
            set => mStarted.Value = value;
        }
        /**
         * 一時停止中 or not
         */
        private UtObservableProperty<bool> mPausing;
        public bool Pausing
        {
            get => mPausing.Value;
            set => mPausing.Value = value;
        }

        /**
         * 再生中 && 一時停止していないか
         */
        public bool Playing => Started && !Pausing;

        /**
         * パネルの開閉用
         */
        private bool mMouseInPanel = false;
        public bool MouseInPanel
        {
            get => mMouseInPanel;
            set => setProp("MouseInPanel", ref mMouseInPanel, value, "ShowPanel");
        }
        /**
         * ストレッチモードパネル開閉用
         */
        private bool mMouseInStretchModePanel = false;
        public bool MouseInStretchModePanel
        {
            get => mMouseInStretchModePanel;
            set => setProp("MouseInStretchModePanel", ref mMouseInStretchModePanel, value, "ShowStretchModePanel");
        }

        public bool ShowPanel => MouseInPanel || !Playing;
        public bool ShowStretchModePanel => MouseInStretchModePanel || !Playing;


        public bool HasNext => mSources?.HasNext ?? false;
        public bool HasPrev => mSources?.HasPrev ?? false;

        public bool TrimmingEnabled => Current?.Trimming?.HasValue ?? false;

        public Ratings CurrentRating
        {
            get => Current?.Rating ?? Ratings.NORMAL;
            set
            {
                var item = Current;
                if (null == item)
                {
                    return;
                }
                if (item.Rating != value)
                {
                    item.Rating = value;
                    WfPlayListDB.Instance.UpdatePlaylistItem((WfFileItem)item, (long)WfPlayListDB.FieldFlag.RATING);
                    notify("Rating");
                }
            }
        }

        public class RatingBindable
        {
            public RatingBindable(WfPlayerWindow parent)
            {
                mParent = new WeakReference<WfPlayerWindow>(parent);
            }
            private WeakReference<WfPlayerWindow> mParent;
            private WfPlayerWindow Parent
            {
                get
                {
                    WfPlayerWindow v = null;
                    return (mParent.TryGetTarget(out v)) ? v : null;
                }
            }

            public bool Normal
            {
                get => Parent.CurrentRating == Ratings.NORMAL;
                set => Parent.CurrentRating = Ratings.NORMAL;
            }
            public bool Good
            {
                get => Parent.CurrentRating == Ratings.GOOD;
                set => Parent.CurrentRating = Ratings.GOOD;
            }
            public bool Bad
            {
                get => Parent.CurrentRating == Ratings.BAD;
                set => Parent.CurrentRating = Ratings.BAD;
            }
            public bool Dreadful
            {
                get => Parent.CurrentRating == Ratings.DREADFUL;
                set => Parent.CurrentRating = Ratings.DREADFUL;
            }
        }
        public RatingBindable Rating { get; }

        private WfStretchMode mStretchMode = WfStretchMode.UniformToFill;
        public WfStretchMode StretchMode
        {
            get => mStretchMode;
            set => setProp("StretchMode", ref mStretchMode, value, "IsCustomStretchMode");
        }

        private bool mStretchMaximum = false;
        public bool StretchMaximum
        {
            get => mStretchMaximum;
            set => setProp("StretchMaximum", ref mStretchMaximum, value);
        }

        public bool IsCustomStretchMode => (long)StretchMode > (long)WfStretchMode.Fill;

        #endregion

        #region ビデオソース
        private TaskCompletionSource<bool> mVideoLoadingTaskSource = null;
        private async Task<bool> SetVideoSource(IWfSource rec)
        {
            if(null!=mVideoLoadingTaskSource)
            {
                await mVideoLoadingTaskSource.Task;
            }
            UpdateTitle(rec);
            notify("Rating");

            mVideoLoadingTaskSource = new TaskCompletionSource<bool>();
            notify("Ready");
            mMediaElement.Source = rec.Uri;
            mMediaElement.Position = TimeSpan.FromMilliseconds(rec.Trimming.Prologue);
            mMediaElement.Play();
            var r = await mVideoLoadingTaskSource.Task;
            mMediaElement.Pause();
            mVideoLoadingTaskSource = null;
            notify("Ready");
            return r;
        }

        private void UpdateTitle(IWfSource rec)
        {
            string name = System.IO.Path.GetFileName(rec.FullPath);
            this.Title = $"WfPlayer - {name}";
        }

        //private List<string> mSources;
        private IWfSourceList mSources;

        private IWfSource Current => mSources?.Current;

        private void VideoSourcesChanged()
        {
            notify("HasNext");
            notify("HasPrev");
            notify("TrimmingEnabled");
        }

        public async void SetSources(IWfSourceList sources)
        {
            Stop();
            mSources = sources;
            if (mLoaded)
            {
                await InitSource();
            }
        }

        private async Task InitSource()
        {
            await SourceChange(async () =>
            {
                if (null != mSources)
                {
                    var rec = mSources.Current ?? mSources.Head;
                    if (null != rec)
                    {
                        await SetVideoSource(rec);
                        VideoSourcesChanged();
                    }
                    return true;
                }
                return false;
            });
        }

        private delegate Task<bool> SourceChangeTask();
        private bool mSourceChanging = false;

        private async Task<bool> SourceChange(SourceChangeTask task)
        {
            if(mSourceChanging)
            {
                return true;
            }
            mSourceChanging = true;
            try
            {
                return await task();
            }
            finally
            {
                mSourceChanging = false;
            }
        }

        private async Task<bool> Next()
        {
            return await SourceChange(async () =>
            {
                var rec = mSources?.Next;
                if (null != rec)
                {
                    bool playing = Playing;
                    Stop();
                    await SetVideoSource(rec);
                    if (playing)
                    {
                        Play();
                    }
                    VideoSourcesChanged();
                    return true;
                }
                return false;
            });
        }

        private async Task<bool> Prev()
        {
            return await SourceChange(async () =>
            {
                var rec = mSources?.Prev;
                if (null != rec)
                {
                    bool playing = Playing;
                    Stop();
                    await SetVideoSource(rec);
                    if (playing)
                    {
                        Play();
                    }
                    VideoSourcesChanged();
                    return true;
                }
                return false;
            });
        }

        #endregion

        #region 再生操作

        void Play()
        {
            if (Pausing)
            {
                Pause();
            }
            else if(!Started)
            {
                mMediaElement.Play();
                Started = true;
                mSources.Current.OnPlayStarted();
            }
        }

        void Pause()
        {
            if(!Started)
            {
                return;
            }

            if (!Pausing)
            {
                mMediaElement.Pause();
                Pausing = true;
            }
            else
            {
                mMediaElement.Play();
                Pausing = false;
            }
        }

        void Stop()
        {
            if(!Started)
            {
                return;
            }
            mMediaElement.Stop();
            Started = false;
            Pausing = false;
            mPositionSlider.Value = mSources.Current.Trimming.Prologue;
        }

        #endregion

        #region 初期化/解放
        private bool mLoaded = false;

        public WfPlayerWindow()
        {
            Duration = 1.0;
            Rating = new RatingBindable(this);
            mSources = null;
            mStarted = new UtObservableProperty<bool>("Started", false, this, "Playing", "ShowPanel", "ShowStretchModePanel");
            mPausing = new UtObservableProperty<bool>("Pausing", false, this, "Playing", "ShowPanel", "ShowStretchModePanel");

            this.DataContext = this;
            InitializeComponent();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            mLoaded = true;
            PropertyChanged += OnBindingPropertyChanged;
            if(mSources!=null)
            {
                await InitSource();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            PropertyChanged -= OnBindingPropertyChanged;
        }
        #endregion

        #region Event Handlers

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            Duration = mMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
            mMediaElement.SpeedRatio = calcSpeedRatio(Speed);
            updateTimelinePosition(Current.Trimming.Prologue, true, true);
            mVideoLoadingTaskSource?.TrySetResult(true);
        }

        private async void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            if(!await Next())
            {
                Stop();
                Close();
            }
        }

        private async void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            mVideoLoadingTaskSource?.TrySetResult(false);
            if (!Playing)
            {
                return;
            }
            if (!await Next())
            {
                Close();
            }
        }

        private void OnPlay(object sender, RoutedEventArgs e)
        {
            Play();
        }

        private void OnPlayFast(object sender, RoutedEventArgs e)
        {
            Speed = 1;
            Play();
        }

        private void OnPause(object sender, RoutedEventArgs e)
        {
            Pause();
        }
        private void OnStop(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private async void OnPrev(object sender, RoutedEventArgs e)
        {
            if (!Ready) return;
            await Prev();
        }

        private async void OnNext(object sender, RoutedEventArgs e)
        {
            if (!Ready) return;
            await Next();
        }

        private void OnResetSpeed(object sender, RoutedEventArgs e)
        {
            Speed = 0.5;
        }

        #endregion

        #region タイムラインスライダー操作

        private bool mDragging = false;
        private DispatcherTimer mPositionTimer = null;
        private bool mUpdatingPositionFromTimer = false;

        /**
         * スライダーの値が変化したときのイベントハンドラ
         * ２つのケースがあり得る。
         * - スライダーが操作された
         *   --> MediaElementをシークする (1)
         * - 動画再生によりシーク位置が変化した（OnPlayingStateChanged内のタイマーから呼ばれる）
         *   --> スライダーの位置を更新する (2)
         *  問題は、(2)の処理中に(1)が呼び出されてしまうこと。
         *  この問題は、mUpdatingPositionFromTimer で回避する
         *  
         *  もう一つの問題は、再生中以外は、mMediaElement.Position を変更しても、画面表示が更新されないこと。
         *  こちらは、Play/Delay/Stop を呼び出すことで回避。
         */
        private void OnPositionChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            updateTimelinePosition(e.NewValue, slider:false, player:!mUpdatingPositionFromTimer);
        }

        private void updateTimelinePosition(double position, bool slider, bool player)
        {
            if (player)
            {
                mMediaElement.Position = TimeSpan.FromMilliseconds(position);
            }
            if (slider)
            {
                mPositionSlider.Value = position;
            }
        }

        private void OnSliderDragStateChanged(TimelineSlider.DragState state)
        {
            switch(state)
            {
                case TimelineSlider.DragState.START:
                    mDragging = true;
                    mMediaElement.Pause();
                    break;
                case TimelineSlider.DragState.DRAGGING:
                    updateTimelinePosition(mPositionSlider.Value, slider:false, player:true);
                    break;
                case TimelineSlider.DragState.END:
                    updateTimelinePosition(mPositionSlider.Value, slider: false, player: true);
                    mDragging = false;
                    if (Playing)
                    {
                        mMediaElement.Play();
                    }
                    break;
            }
        }

        private void OnBindingPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Playing":
                    OnPlayingStateChanged(Playing);
                    break;
                case "StretchMode":
                case "StretchMaximum":
                    OnStretchModeChanged();
                    break;
                default:
                    break;
            }
        }

        private void OnPlayingStateChanged(bool newValue)
        {
            if (newValue)
            {
                if (null == mPositionTimer)
                {
                    mPositionTimer = new DispatcherTimer();
                    mPositionTimer.Tick += (s, e) =>
                    {
                        if (!mDragging)
                        {
                            var current = mMediaElement.Position.TotalMilliseconds;
                            var remains = Duration - current;
                            if (remains>0 && remains < Current.Trimming.Epilogue)
                            {
                                Stop();
                                mPositionTimer.Stop();
                                mPositionTimer = null;
                                OnMediaEnded(null, null);
                                return;
                            }
                            mUpdatingPositionFromTimer = true;
                            mPositionSlider.Value = current;
                            mUpdatingPositionFromTimer = false;
                        }
                    };
                    mPositionTimer.Interval = TimeSpan.FromMilliseconds(50);
                    mPositionTimer.Start();
                }
            }
            else
            {
                if (null != mPositionTimer)
                {
                    mPositionTimer.Stop();
                    mPositionTimer = null;
                }
            }
        }

        public void OnStretchModeChanged()
        {
            switch(StretchMode)
            {
                case WfStretchMode.UniformToFill:
                    mMediaElement.Width = mMediaElement.Height = Double.NaN;
                    mMediaElement.Stretch = Stretch.UniformToFill;
                    break;
                case WfStretchMode.Uniform:
                    mMediaElement.Width = mMediaElement.Height = Double.NaN;
                    mMediaElement.Stretch = Stretch.Uniform;
                    break;
                case WfStretchMode.Fill:
                    mMediaElement.Width = mMediaElement.Height = Double.NaN;
                    mMediaElement.Stretch = Stretch.Fill;
                    break;
                case WfStretchMode.CUSTOM125:
                    CustomStretch(1.25);
                    break;
                case WfStretchMode.CUSTOM133:
                    CustomStretch(4.0/3.0);
                    break;
                case WfStretchMode.CUSTOM150:
                    CustomStretch(1.5);
                    break;
                case WfStretchMode.CUSTOM177:
                    CustomStretch(16.0/9.0);
                    break;
                default:
                    break;
            }
        }

        private void CustomStretch(double ratio)
        {
            double w = this.ActualWidth, h = this.ActualHeight;
            double cw= h * ratio, ch = w / ratio;
            double vw, vh;

            if (StretchMaximum)
            {
                (vw, vh) = (w < cw) ? (cw, h) : (w, ch);
            }
            else
            {
                (vw, vh) = (w > cw) ? (cw, h) : (w, ch);
            }
            mMediaElement.Stretch = Stretch.Fill;
            mMediaElement.Width = vw;
            mMediaElement.Height = vh;
        }

        #endregion

        private void HandleMouseOnPanel(Panel panel, bool enter)
        {
            if (null == panel)
            {
                return;
            }
            switch (panel.Name)
            {
                case "mPanelBase":
                    MouseInPanel = enter;
                    break;
                case "mStretchModePanel":
                    MouseInStretchModePanel = enter;
                    break;
                default:
                    break;
            }
        }

        private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            HandleMouseOnPanel(sender as Panel, true);
        }

        private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            HandleMouseOnPanel(sender as Panel, false);
        }

        private async void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Debug.WriteLine($"KEY:{e.Key} - {e.SystemKey} - {e.KeyStates} - Rep={e.IsRepeat} - D/U/T={e.IsDown}/{e.IsUp}/{e.IsToggled}");
            switch (e.Key)
            {
                case System.Windows.Input.Key.Escape:
                    Close();
                    break;
                case System.Windows.Input.Key.NumPad2:
                    await Next();
                    break;
                case System.Windows.Input.Key.NumPad8:
                    await Prev();
                    break;
                default:
                    break;
            }
        }

        private void EditTrimming(object sender, RoutedEventArgs e)
        {
            var item = Current as WfFileItem;
            if (null == item)
            {
                return;
            }
            var tp = new WfTrimmingPlayer(item.Trimming, item.FullPath);
            WfTrimmingPlayer.ResultEventProc onNewTrimming = (result, db) =>
            {
                item.Trimming = tp.Result;
                db.UpdatePlaylistItem(item, (long)WfPlayListDB.FieldFlag.TRIMMING);
                notify("TrimmingEnabled");
            };
            tp.OnResult += onNewTrimming;
            tp.ShowDialog();
            tp.OnResult -= onNewTrimming;
        }

        private void SelectTrimming(object sender, RoutedEventArgs e)
        {
            var item = Current as WfFileItem;
            if(null==item)
            {
                return;
            }
            var dlg = new WfTrimmingPatternList();
            dlg.ShowDialog();
            if (null != dlg.Result)
            {
                item.Trimming = dlg.Result;
                WfPlayListDB.Instance.UpdatePlaylistItem(item, (long)WfPlayListDB.FieldFlag.TRIMMING);
                notify("TrimmingEnabled");
            }
        }

        private void ResetTrimming(object sender, RoutedEventArgs e)
        {
            var item = Current as WfFileItem;
            if (null == item||!item.Trimming.HasValue)
            {
                return;
            }
            item.Trimming = WfFileItem.Trim.NoTrim;
            WfPlayListDB.Instance.UpdatePlaylistItem(item, (long)WfPlayListDB.FieldFlag.TRIMMING);
            notify("TrimmingEnabled");
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(IsCustomStretchMode)
            {
                OnStretchModeChanged();
            }
        }
    }
}
