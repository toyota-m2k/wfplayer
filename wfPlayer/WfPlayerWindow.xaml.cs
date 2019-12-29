using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using wfPlayer.server;

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

        /**
         * Duration --> mPositionSlider.Value
         */
        //public static readonly DependencyProperty DurationProperty = DependencyProperty.Register("Duration", typeof(double), typeof(WfPlayerWindow));
        //public double Duration
        //{
        //    get => (double)GetValue(DurationProperty);
        //    set
        //    {
        //        SetValue(DurationProperty, value);
        //        notify("LargePositionChange");
        //        notify("SmallPositionChange");
        //        notify("DurationText");
        //    }
        //}
        private double mDuration = 0;
        public double Duration {
            get => mDuration;
            set => setProp("Duration", ref mDuration, value, "DurationText", "LargePositionChange", "SmallPositionChange");
        }

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
         * mPositionSlider.LargeChange/SmallChange
         */
        public double LargePositionChange => Duration / 10;
        public double SmallPositionChange => 1000;

        /**
         * パネルの開閉用
         */
        private bool mMouseInPanel = false;
        public bool MouseInPanel {
            get => mMouseInPanel;
            set => setProp("MouseInPanel", ref mMouseInPanel, value, "ShowPanel", "ShowSliderPanel");
        }
        /**
         * ストレッチモードパネル開閉用
         */
        private bool mMouseInStretchModePanel = false;
        public bool MouseInStretchModePanel {
            get => mMouseInStretchModePanel;
            set => setProp("MouseInStretchModePanel", ref mMouseInStretchModePanel, value, "ShowStretchModePanel");
        }

        private bool mMouseInSliderPanel = false;
        public bool MouseInSliderPanel {
            get => mMouseInSliderPanel;
            set => setProp("MouseInSliderPanel", ref mMouseInSliderPanel, value, "ShowSliderPanel", "ShowPanel");
        }

        public bool ShowPanel => MouseInPanel || MouseInSliderPanel || !Playing;
        public bool ShowSliderPanel => MouseInSliderPanel || MouseInPanel || !Playing || SliderPinned;
        public bool ShowStretchModePanel => MouseInStretchModePanel || !Playing;


        public bool HasNext => mSources?.HasNext ?? false;
        public bool HasPrev => mSources?.HasPrev ?? false;

        public bool TrimmingEnabled => Current?.Trimming?.HasValue ?? false;

        /**
         * Ratings
         */
        public Ratings Rating
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
                    //WfPlayListDB.Instance.UpdatePlaylistItem((WfFileItem)item, (long)WfPlayListDB.FieldFlag.RATING);
                    item.SaveModified();
                    notify("Rating");
                }
            }
        }

        //public class RatingBindable
        //{
        //    public RatingBindable(WfPlayerWindow parent)
        //    {
        //        mParent = new WeakReference<WfPlayerWindow>(parent);
        //    }
        //    private WeakReference<WfPlayerWindow> mParent;
        //    private WfPlayerWindow Parent
        //    {
        //        get
        //        {
        //            WfPlayerWindow v = null;
        //            return (mParent.TryGetTarget(out v)) ? v : null;
        //        }
        //    }

        //    public bool Normal
        //    {
        //        get => Parent.CurrentRating == Ratings.NORMAL;
        //        set => Parent.CurrentRating = Ratings.NORMAL;
        //    }
        //    public bool Good
        //    {
        //        get => Parent.CurrentRating == Ratings.GOOD;
        //        set => Parent.CurrentRating = Ratings.GOOD;
        //    }
        //    public bool Bad
        //    {
        //        get => Parent.CurrentRating == Ratings.BAD;
        //        set => Parent.CurrentRating = Ratings.BAD;
        //    }
        //    public bool Dreadful
        //    {
        //        get => Parent.CurrentRating == Ratings.DREADFUL;
        //        set => Parent.CurrentRating = Ratings.DREADFUL;
        //    }
        //}
        //public RatingBindable Rating { get; }

        /**
         * Stretch Mode
         */
        private WfStretchMode mStandardStretchMode = WfStretchMode.UniformToFill;
        private WfStretchMode mStretchMode = WfStretchMode.UniformToFill;
        public WfStretchMode StretchMode
        {
            get => mStretchMode;
            set
            {
                if(!IsCustomStretchMode(value))
                {
                    mStandardStretchMode = value;
                }
                if(setProp("StretchMode", ref mStretchMode, value, "CustomStretchMode") && SaveAspectAuto)
                {
                    SaveAspect();
                }
            }
        }

        private bool mStretchMaximum = true;
        public bool StretchMaximum
        {
            get => mStretchMaximum;
            set => setProp("StretchMaximum", ref mStretchMaximum, value);
        }

        private static bool IsCustomStretchMode(WfStretchMode mode)
        {
            return (long)mode > (long)WfStretchMode.Fill;
        }
        public bool CustomStretchMode => IsCustomStretchMode(StretchMode);

        private bool mSaveAspectAuto = true;
        public bool SaveAspectAuto
        {
            get => mSaveAspectAuto;
            set
            {
                if (setProp("SaveAspectAuto", ref mSaveAspectAuto, value) && value)
                {
                    SaveAspect();
                }
            }
        }

        private WfServerEx mServer;
        public bool AllowRemoteControl
        {
            get => mServer?.IsListening ?? false;
            set
            {
                if (value)
                {
                    if (null == mServer)
                    {
                        mServer = WfServerEx.CreateInstance(InvokeFromRemote);
                    }
                    mServer.Start();
                }
                else
                {
                    if (null != mServer)
                    {
                        mServer.Stop();
                        //mServer = null;
                    }
                }
                notify("AllowRemoteControl");
            }

        }

        private bool mSliderPinned = true;
        public bool SliderPinned {
            get => mSliderPinned;
            set => setProp("SliderPinned", ref mSliderPinned, value, "ShowSliderPanel");
        }
        
        #endregion

        #region ビデオソース
        private TaskCompletionSource<bool> mVideoLoadingTaskSource = null;
        private async Task<bool> SetVideoSource(IWfSource rec, bool startNow)
        {
            if(null!=mVideoLoadingTaskSource)
            {
                await mVideoLoadingTaskSource.Task;
            }
            UpdateTitle(rec);
            notify("Rating");

            mVideoLoadingTaskSource = new TaskCompletionSource<bool>();
            notify("Ready");
            mMediaElement.Stop();
            mMediaElement.Close();
            mMediaElement.Source = rec.Uri;
            mMediaElement.Position = TimeSpan.FromMilliseconds(rec.Trimming.Prologue);
            mMediaElement.Play();
            var r = await mVideoLoadingTaskSource.Task;
            if (!startNow) {
                mMediaElement.Pause();
            } else if (!mPreviewMode) {
                rec.Touch();
            }
            mVideoLoadingTaskSource = null;
            notify("Ready");
            return r;
        }

        private void UpdateTitle(IWfSource rec)
        {
            string name = System.IO.Path.GetFileName(rec.FullPath);
            this.Title = $"WfPlayer - {name}";
        }

        private bool mAutoStart = false;
        private IWfSourceList mSources;
        private IWfSource Current => mSources?.Current;
        private void VideoSourcesChanged()
        {
            notify("HasNext");
            notify("HasPrev");
            notify("TrimmingEnabled");

            switch (Current.Aspect)
            {
                case WfAspect.AUTO:
                default:
                    StretchMode = mStandardStretchMode;
                    break;
                case WfAspect.CUSTOM125:
                    StretchMode = WfStretchMode.CUSTOM125;
                    break;
                case WfAspect.CUSTOM133:
                    StretchMode = WfStretchMode.CUSTOM133;
                    break;
                case WfAspect.CUSTOM150:
                    StretchMode = WfStretchMode.CUSTOM150;
                    break;
                case WfAspect.CUSTOM177:
                    StretchMode = WfStretchMode.CUSTOM177;
                    break;
            }
        }

        public async void SetSources(IWfSourceList sources, bool startNow)
        {
            Stop();
            mSources = sources;
            mAutoStart = startNow;
            if (mLoaded)
            {
                await InitSource(startNow);
            }
        }

        private async Task InitSource(bool startNow)
        {
            await SourceChange(async () =>
            {
                if (null != mSources)
                {
                    var rec = mSources.Current ?? mSources.Head;
                    if (null != rec)
                    {
                        await SetVideoSource(rec, startNow);
                        Started = startNow;
                        Pausing = false;
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
                    await SetVideoSource(rec, Playing);
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
                    await SetVideoSource(rec, Playing);
                    VideoSourcesChanged();
                    return true;
                }
                return false;
            });
        }

        #endregion

        #region 再生操作

        void Play(double speed=0.5)
        {
            if (Speed == speed) {
                Speed = 0.5;
            } else {
                Speed = speed;
            }
            if (Pausing)
            {
                Pause();
            }
            else if(!Started)
            {
                mMediaElement.Play();
                Started = true;
                if (!mPreviewMode) {
                    Current?.Touch();
                }
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
                if (!mPreviewMode) {
                    Current?.Touch();
                }
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

        private DispatcherTimer mSuperFastPlayTimer;
        void SuperFastPlay() {
            if(null!=mSuperFastPlayTimer) {
                mSuperFastPlayTimer.Stop();
                mSuperFastPlayTimer = null;
            } else {
                mSuperFastPlayTimer = new DispatcherTimer();
                mSuperFastPlayTimer.Interval = TimeSpan.FromSeconds(1.5);
                mSuperFastPlayTimer.Tick += (s, e) => {
                    RelativeSeek(10*1000);
                };
                mSuperFastPlayTimer.Start();
            }
        }

        void ResetSuperFastPlay() {
            if(mSuperFastPlayTimer!=null) {
                mSuperFastPlayTimer.Stop();
                mSuperFastPlayTimer = null;
            }
        }

        #endregion

        #region 初期化/解放
        private bool mLoaded = false;
        private bool mPreviewMode = false;  // true にすると、再生時にCounterをインクリメントしない

        public WfPlayerWindow(bool preview)
        {
            mPreviewMode = preview;
            Duration = 1.0;
            //Rating = new RatingBindable(this);
            mSources = null;
            mStarted = new UtObservableProperty<bool>("Started", false, this, "Playing", "ShowPanel", "ShowStretchModePanel", "ShowSliderPanel");
            mPausing = new UtObservableProperty<bool>("Pausing", false, this, "Playing", "ShowPanel", "ShowStretchModePanel", "ShowSliderPanel");
            mCursorManager = new HidingCursor(this);
            InitKeyMap();

            mStandardStretchMode = mStretchMode = (WfStretchMode)(Convert.ToInt32(WfPlayListDB.Instance.GetValueAt("StretchMode") ?? "0"));
            mStretchMaximum = (Convert.ToInt32(WfPlayListDB.Instance.GetValueAt("StretchMaximum") ?? "0"))!=0;
            mSliderPinned = (Convert.ToInt32(WfPlayListDB.Instance.GetValueAt("SliderPinned") ?? "0")) != 0;


            this.DataContext = this;
            InitializeComponent();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            mLoaded = true;
            PropertyChanged += OnBindingPropertyChanged;
            if(mSources!=null)
            {
                await InitSource(mAutoStart);
            }
            AllowRemoteControl = true;
            OnStretchModeChanged();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ResetSuperFastPlay();
            PropertyChanged -= OnBindingPropertyChanged;
            Current?.SaveModified();
            mServer?.Dispose();
            mServer = null;

            WfPlayListDB.Instance.SetValueAt("StretchMode", $"{(long)mStandardStretchMode}");
            WfPlayListDB.Instance.SetValueAt("StretchMaximum", mStretchMaximum ? "1" : "0");
            WfPlayListDB.Instance.SetValueAt("SliderPinned", mSliderPinned ? "1" : "0");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WfGlobalParams.Instance.PlayerPlacement.ApplyPlacementTo(this);
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            WfGlobalParams.Instance.Placement.GetPlacementFrom(this);
        }

        #endregion

        #region MediaElement Event Handlers

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MediaOpened");
            Duration = mMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
            mMediaElement.SpeedRatio = calcSpeedRatio(Speed);
            updateTimelinePosition(Current.Trimming.Prologue, true, true);
            mVideoLoadingTaskSource?.TrySetResult(true);
        }

        private async void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MediaEnded");
            //ResetSuperFastPlay();
            //Speed = 0.5;
            if (!await Next())
            {
                Stop();
                Close();
            }
        }

        private async void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Debug.WriteLine("MediaFailed");
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

        #endregion

        #region Command Handler

        private void OnPlay(object sender, RoutedEventArgs e)
        {
            Play(0.5);
        }

        private void OnPlayFast(object sender, RoutedEventArgs e)
        {
            Play(1);
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

        #region Event Handlers

        private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!HandleMouseOnPanel(sender as Panel, true))
            {
                mCursorManager.Update(e.GetPosition(this));
            }

        }

        private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!HandleMouseOnPanel(sender as Panel, false))
            {
                mCursorManager.Reset();
            }
        }

        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Window)
            {
                mCursorManager.Update(e.GetPosition(this));
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (CustomStretchMode)
            {
                OnStretchModeChanged();
            }
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Debug.WriteLine($"KEY:{e.Key} - {e.SystemKey} - {e.KeyStates} - Rep={e.IsRepeat} - D/U/T={e.IsDown}/{e.IsUp}/{e.IsToggled}");
            if(InvokeKeyCommand(e.Key))
            {
                e.Handled = true;
            }

            //switch (e.Key)
            //{
            //    case System.Windows.Input.Key.Escape:
            //        Close();
            //        break;
            //    case System.Windows.Input.Key.NumPad2:
            //        await Next();
            //        break;
            //    case System.Windows.Input.Key.NumPad8:
            //        await Prev();
            //        break;
            //    default:
            //        break;
            //}
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
            SeekPositionText = $"{FormatDuration(position)}";
        }

        private string FormatDuration(double duration) {
            var t = TimeSpan.FromMilliseconds(duration);
            return string.Format("{0}:{1:00}:{2:00}", t.Hours, t.Minutes, t.Seconds);
                //$"{t.Hours}:{t.Minutes}.{t.Seconds}";

        }

        private string mSeekPositionText = "";
        public string SeekPositionText {
            get => mSeekPositionText;
            set => setProp("SeekPositionText", ref mSeekPositionText, value);
        }

        public string DurationText => FormatDuration(Duration);

        private void OnSliderDragStateChanged(TimelineSliderOld.DragState state)
        {
            switch(state)
            {
                case TimelineSliderOld.DragState.START:
                    mDragging = true;
                    mMediaElement.Pause();
                    break;
                case TimelineSliderOld.DragState.DRAGGING:
                    updateTimelinePosition(mPositionSlider.Value, slider:false, player:true);
                    break;
                case TimelineSliderOld.DragState.END:
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

        private void OnPlayingStateChanged(bool playing)
        {
            mCursorManager.Enabled = mCursorManager.Enabled = !MouseInPanel && !MouseInSliderPanel && !MouseInStretchModePanel && playing;
            if (playing)
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
                                //mPositionTimer?.Stop();
                                //mPositionTimer = null;
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
                mPositionTimer?.Stop();
                mPositionTimer = null;
            }
        }

        enum SeekUnit {
            LARGE,
            SMALL,
            SEEK10,
            SEEK5,
        }

        double SeekSpan(SeekUnit unit) {
            switch(unit) {
                default:
                case SeekUnit.SMALL:
                    return SmallPositionChange;
                case SeekUnit.LARGE:
                    return LargePositionChange;
                case SeekUnit.SEEK10:
                    return 10 * 1000;
                case SeekUnit.SEEK5:
                    return 5 * 1000;
            }
        }

        void RelativeSeek(double seek) {
            if (!Ready) return;

            var v = mPositionSlider.Value + seek;
            updateTimelinePosition(Math.Min(Math.Max(v, Current.Trimming.Prologue), mPositionSlider.Maximum - Current.Trimming.Epilogue), true, true);
        }

        void SeekForward(SeekUnit unit) {
            if (!Ready) {
                return;
            }
            RelativeSeek(SeekSpan(unit));
        }

        void SeekBackward(SeekUnit unit) {
            if (!Ready) {
                return;
            }
            RelativeSeek(-SeekSpan(unit));
        }

        #endregion

        #region Stretch Mode

        public void OnStretchModeChanged()
        {
            switch (StretchMode)
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
                    CustomStretch(4.0 / 3.0);
                    break;
                case WfStretchMode.CUSTOM150:
                    CustomStretch(1.5);
                    break;
                case WfStretchMode.CUSTOM177:
                    CustomStretch(16.0 / 9.0);
                    break;
                default:
                    break;
            }
        }

        private void CustomStretch(double ratio)
        {
            double w = this.ActualWidth, h = this.ActualHeight;
            double cw = h * ratio, ch = w / ratio;
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

        private void SaveAspect()
        {
            WfAspect aspect;
            switch (StretchMode)
            {
                case WfStretchMode.UniformToFill:
                case WfStretchMode.Uniform:
                case WfStretchMode.Fill:
                default:
                    aspect = WfAspect.AUTO;
                    break;
                case WfStretchMode.CUSTOM125:
                    aspect = WfAspect.CUSTOM125;
                    break;
                case WfStretchMode.CUSTOM133:
                    aspect = WfAspect.CUSTOM133;
                    break;
                case WfStretchMode.CUSTOM150:
                    aspect = WfAspect.CUSTOM150;
                    break;
                case WfStretchMode.CUSTOM177:
                    aspect = WfAspect.CUSTOM177;
                    break;
            }
            Current.Aspect = aspect;
            return;
        }

        private void OnSaveAspect(object sender, RoutedEventArgs e)
        {
            SaveAspect();
            Current.SaveModified();
            return;
        }

        private void ToggleStandardStretchMode()
        {
            if(CustomStretchMode)
            {
                StretchMode = mStandardStretchMode;
            }
            else
            {
                int v = 1+(int)StretchMode;
                if(v>=(int)WfStretchMode.Fill)
                {
                    v = (int)WfStretchMode.UniformToFill;
                }
                StretchMode = (WfStretchMode)v;
            }
        }

        private void ToggleCustomStretchMode(bool plus)
        {
            if(CustomStretchMode)
            {
                int v = (int)StretchMode;
                if(plus)
                {
                    v++;
                    if(v>(int)WfStretchMode.CUSTOM177)
                    {
                        v = (int)WfStretchMode.CUSTOM125;
                    }
                }
                else
                {
                    v--;
                    if (v < (int)WfStretchMode.CUSTOM125)
                    {
                        v = (int)WfStretchMode.CUSTOM177;
                    }
                }
                StretchMode = (WfStretchMode)v;
            }
            else
            {
                if (plus)
                {
                    StretchMode = WfStretchMode.CUSTOM125;
                }
                else
                {
                    StretchMode = WfStretchMode.CUSTOM177;
                }
            }
        }

        #endregion

        #region Control Panels

        private bool HandleMouseOnPanel(Panel panel, bool enter)
        {
            if (null == panel)
            {
                return false;
            }
            switch (panel.Name)
            {
                case "mPanelBase":
                    MouseInPanel = enter;
                    break;
                case "mStretchModePanel":
                    MouseInStretchModePanel = enter;
                    break;
                case "mSliderPanel":
                    MouseInSliderPanel = enter;
                    break;
                default:
                    return false;
            }
            mCursorManager.Enabled = !MouseInPanel && !MouseInStretchModePanel && Playing;
            return true;
        }
        #endregion

        #region Hide/Show Cursor

        class HidingCursor
        {
            private static long WAIT_TIME = 2000;   //3ms
            private Point mPosition;
            private long mCheck = 0;
            private DispatcherTimer mTimer = null;
            private WeakReference<Window> mWin;
            private bool mEnabled = false;

            public HidingCursor(Window owner)
            {
                mWin = new WeakReference<Window>(owner);
                mPosition = new Point();
            }

            private Cursor CursorOnWin
            {
                get => mWin?.GetValue().Cursor;
                set
                {
                    var win = mWin?.GetValue();
                    if (null != win)
                    {
                        win.Cursor = value;
                    }
                }
            }

            public bool Enabled
            {
                get => mEnabled;
                set
                {
                    if (value != mEnabled)
                    {
                        mEnabled = value;
                        if (value)
                        {
                            //Update();
                        }
                        else
                        {
                            Reset();
                        }
                    }
                }
            }

            public void Reset()
            {
                if (mTimer != null)
                {
                    mTimer.Stop();
                    mTimer = null;
                }
                CursorOnWin = Cursors.Arrow;
            }

            public void Update(Point pos)
            {
                if (!Enabled)
                {
                    return;
                }

                if (mPosition != pos)
                {
                    mPosition = pos;
                    mCheck = System.Environment.TickCount;
                    CursorOnWin = Cursors.Arrow;
                    if (null == mTimer)
                    {
                        mTimer = new DispatcherTimer();
                        mTimer.Tick += OnTimer;
                        mTimer.Interval = TimeSpan.FromMilliseconds(WAIT_TIME / 3);
                        mTimer.Start();
                    }
                }
            }

            private void OnTimer(object sender, EventArgs e)
            {
                if (null == mTimer)
                {
                    return;
                }
                if (System.Environment.TickCount - mCheck > WAIT_TIME)
                {
                    mTimer.Stop();
                    mTimer = null;
                    CursorOnWin = Cursors.None;
                }
            }
        }
        private HidingCursor mCursorManager = null;

        private void KickOutMouse()
        {
            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(0, 0);
        }

        public bool RequestShutdown { get; private set; } = false;
        private void ShutdownPC() {
            RequestShutdown = true;
            Close();
            //Application.Current.Shutdown();
            //WinShutdown.Shutdown();
        }

        #endregion

        #region Trimming

        private class SingleSourceList : IWfSourceList {
            public bool HasNext => false;

            public bool HasPrev => false;

            public IWfSource Current { get; }

            public IWfSource Next => null;

            public IWfSource Prev => null;

            public IWfSource Head => Current;

            public SingleSourceList(IWfSource src) {
                Current = src;
            }
        }

        private void EditTrimming(WfFileItem item)
        {
            if (null == item)
            {
                return;
            }
            var tp = new WfTrimmingPlayer(new SingleSourceList(item));
            WfTrimmingPlayer.ResultEventProc onNewTrimming = (result, db) =>
            {
                item.Trimming = result;
                //db.UpdatePlaylistItem(item, (long)WfPlayListDB.FieldFlag.TRIMMING);
                item.SaveModified();
                notify("TrimmingEnabled");
            };
            tp.OnResult += onNewTrimming;
            tp.ShowDialog();
            tp.OnResult -= onNewTrimming;
        }

        private void OnEditTrimming(object sender, RoutedEventArgs e)
        {
            EditTrimming( Current as WfFileItem );
        }

        private void SelectTrimming(WfFileItem item)
        {
            if (null == item)
            {
                return;
            }
            var dlg = new WfTrimmingPatternList();
            dlg.ShowDialog();
            if (null != dlg.Result)
            {
                item.Trimming = dlg.Result;
                // WfPlayListDB.Instance.UpdatePlaylistItem(item, (long)WfPlayListDB.FieldFlag.TRIMMING);
                item.SaveModified();
                notify("TrimmingEnabled");
            }
        }

        private void OnSelectTrimming(object sender, RoutedEventArgs e)
        {
            SelectTrimming(Current as WfFileItem);
        }

        private void ResetTrimming(WfFileItem item)
        {
            if (null == item || !item.Trimming.HasValue)
            {
                return;
            }
            item.Trimming = WfFileItem.Trim.NoTrim;
            //WfPlayListDB.Instance.UpdatePlaylistItem(item, (long)WfPlayListDB.FieldFlag.TRIMMING);
            item.SaveModified();
            notify("TrimmingEnabled");
        }

        private void OnResetTrimming(object sender, RoutedEventArgs e)
        {
            ResetTrimming(Current as WfFileItem);
        }

        #endregion

        #region Key Mapping

        static class Commands
        {
            public const string PLAY = "play";
            public const string PAUSE = "pause";
            public const string STOP = "stop";
            public const string FAST_PLAY = "fast";
            public const string MUTE = "mute";
            public const string CLOSE = "close";
            public const string NEXT = "next";
            public const string PREV = "prev";
            public const string SEEK_FWD= "fwd";
            public const string SEEK_BACK = "back";
            public const string SEEK_FWD_L= "fwdL";
            public const string SEEK_BACK_L = "backL";
            public const string SEEK_FWD_10 = "fwd10";
            public const string SEEK_BACK_10 = "back10";
            public const string SEEK_FWD_5 = "fwd5";
            public const string SEEK_BACK_5 = "back5";
            public const string PLAY_SUPER_FAST = "superFast";
            public const string RATING_GOOD = "good";
            public const string RATING_NORMAL = "normal";
            public const string RATING_BAD = "bad";
            public const string RATING_DREADFUL = "dreadful";
            public const string NEXT_STD_STRETCH = "std";
            public const string NEXT_CST_STRETCH = "custNext";
            public const string PREV_CST_STRETCH = "custPrev";

            public const string TRIM_EDIT = "trimEdit";
            public const string TRIM_SELECT = "trimSelect";
            public const string TRIM_RESET = "trimReset";

            public const string PIN_SLIDER = "showSlider";

            public const string KICKOUT_MOUSE = "kickOutMouse";
            public const string SHUTDOWN = "shutdown";
        }

        private void ToggleSliderPanel() {
            SliderPinned = !SliderPinned;
        }

        private Dictionary<System.Windows.Input.Key, string> mKeyCommandMap = null;
        private Dictionary<string, Action> mCommandMap = null;
        private void InitKeyMap()
        {
            mCommandMap = new Dictionary<string, Action>()
            {
                { Commands.PLAY, ()=>Play(0.5) },
                { Commands.PAUSE, Pause },
                { Commands.STOP, Stop },
                { Commands.FAST_PLAY, ()=>Play(1.0) },
                { Commands.MUTE, ()=>{ Mute=!Mute; } },
                { Commands.CLOSE, Close },
                { Commands.NEXT, ()=>{ var _=Next(); } },
                { Commands.PREV, ()=>{ var _=Prev(); } },
                { Commands.SEEK_BACK, ()=>SeekBackward(SeekUnit.SMALL) },
                { Commands.SEEK_FWD, ()=>SeekForward(SeekUnit.SMALL) },
                { Commands.SEEK_BACK_L, ()=>SeekBackward(SeekUnit.LARGE) },
                { Commands.SEEK_FWD_L, ()=>SeekForward(SeekUnit.LARGE) },

                { Commands.SEEK_BACK_10, ()=>SeekBackward(SeekUnit.SEEK10) },
                { Commands.SEEK_FWD_10, ()=>SeekForward(SeekUnit.SEEK10) },
                { Commands.SEEK_BACK_5, ()=>SeekBackward(SeekUnit.SEEK5) },
                { Commands.SEEK_FWD_5, ()=>SeekForward(SeekUnit.SEEK5) },

                { Commands.PLAY_SUPER_FAST, ()=>SuperFastPlay() },
                { Commands.RATING_GOOD, ()=> {Rating=Ratings.GOOD; } },
                { Commands.RATING_NORMAL, ()=> {Rating=Ratings.NORMAL; } },
                { Commands.RATING_BAD, ()=> {Rating=Ratings.BAD; } },
                { Commands.RATING_DREADFUL, ()=> {Rating=Ratings.DREADFUL; var _=Next(); } },

                { Commands.NEXT_STD_STRETCH, ToggleStandardStretchMode },
                { Commands.NEXT_CST_STRETCH, ()=> ToggleCustomStretchMode(true) },
                { Commands.PREV_CST_STRETCH, ()=> ToggleCustomStretchMode(false) },

                { Commands.TRIM_EDIT, ()=>EditTrimming(Current as WfFileItem) },
                { Commands.TRIM_SELECT, ()=>SelectTrimming(Current as WfFileItem) },
                { Commands.TRIM_RESET, ()=>ResetTrimming(Current as WfFileItem) },

                { Commands.PIN_SLIDER, ToggleSliderPanel },

                { Commands.KICKOUT_MOUSE, KickOutMouse },
                { Commands.SHUTDOWN, ShutdownPC }
            };

            mKeyCommandMap = new Dictionary<Key, string>()
            {
                { Key.G, Commands.PLAY },
                { Key.P, Commands.PAUSE },
                { Key.S, Commands.STOP },
                { Key.F, Commands.FAST_PLAY },
                { Key.M, Commands.MUTE },
                { Key.K, Commands.TRIM_EDIT },
                { Key.L, Commands.TRIM_SELECT },
                //{ Key.L, Commands.TRIM_RESET },
                { Key.Escape, Commands.CLOSE },
                { Key.OemPeriod, Commands.NEXT },
                { Key.OemComma, Commands.PREV },
                { Key.Home, Commands.PREV },
                { Key.End,  Commands.NEXT },
                { Key.Up, Commands.PREV },
                { Key.Down, Commands.NEXT },
                { Key.Left, Commands.SEEK_BACK },
                { Key.Right, Commands.SEEK_FWD },
                { Key.PageUp, Commands.SEEK_BACK_L },
                { Key.PageDown, Commands.SEEK_FWD_L },
                { Key.D1, Commands.RATING_GOOD },
                { Key.D2, Commands.RATING_NORMAL },
                { Key.D3, Commands.RATING_BAD },
                { Key.D4, Commands.RATING_DREADFUL },
                { Key.NumPad1, Commands.RATING_GOOD },
                { Key.NumPad0, Commands.RATING_NORMAL },
                { Key.NumPad2, Commands.RATING_BAD },
                { Key.NumPad3, Commands.RATING_DREADFUL },

                { Key.NumPad4, Commands.NEXT_STD_STRETCH },
                { Key.NumPad5, Commands.NEXT_CST_STRETCH },
                { Key.NumPad6, Commands.PREV_CST_STRETCH },
            };
        }

        private bool InvokeKeyCommand(Key key)
        {
            if(mKeyCommandMap.TryGetValue(key, out var cmd))
            {
                return InvokeCommand(cmd);
            }
            return false;
        }

        private bool InvokeCommand(string cmd)
        {
            if (cmd != null)
            {
                if (mCommandMap.TryGetValue(cmd, out var action))
                {
                    action?.Invoke();
                    return true;
                }
            }
            return false;
        }

        private bool InvokeFromRemote(string cmd)
        {
            return Dispatcher.Invoke<bool>(()=> { return InvokeCommand(cmd); });
        }

        #endregion

    }
}
