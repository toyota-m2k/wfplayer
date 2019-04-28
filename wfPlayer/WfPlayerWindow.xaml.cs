using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace wfPlayer
{
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

        private bool setProp<T>(string name, ref T field, T value)
        {
            if (!field.Equals(value))
            {
                field = value;
                notify(name);
                return true;
            }
            return false;
        }

        private bool setProp<T>(string[] names, ref T field, T value)
        {
            if (!field.Equals(value))
            {
                field = value;
                foreach (var name in names)
                {
                    notify(name);
                }
                return true;
            }
            return false;
        }

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

        private UtObservableProperty<bool> mMouseInPanel;
        public bool MouseInPanel
        {
            get => mMouseInPanel.Value;
            set => mMouseInPanel.Value = value;
        }

        public bool ShowPanel => MouseInPanel || !Playing;


        public bool HasNext => mSources?.HasNext ?? false;
        public bool HasPrev => mSources?.HasPrev ?? false;

        #endregion

        #region ビデオソース
        private TaskCompletionSource<bool> mVideoLoadingTaskSource = null;
        private async Task<bool> SetVideoSource(Uri uri)
        {
            if(null!=mVideoLoadingTaskSource)
            {
                await mVideoLoadingTaskSource.Task;
            }
            notify("Ready");
            mVideoLoadingTaskSource = new TaskCompletionSource<bool>();
            mMediaElement.Source = uri;
            mMediaElement.Position = TimeSpan.FromMilliseconds(0);
            mMediaElement.Play();
            var r = await mVideoLoadingTaskSource.Task;
            mMediaElement.Pause();
            mVideoLoadingTaskSource = null;
            notify("Ready");
            return r;
        }

        //private List<string> mSources;
        private IWfSourceList mSources;

        private void VideoSourcesChanged()
        {
            notify("HasNext");
            notify("HasPrev");
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
            if(null==mSources)
            {
                return;
            }
            var rec = mSources.Current ?? mSources.Head;
            var uri = rec?.Uri;
            if(null!=uri)
            {
                await SetVideoSource(uri);
                VideoSourcesChanged();
            }
        }

        private async Task<bool> Next()
        {
            var uri = mSources?.Next?.Uri;
            if(null!=uri)
            {
                bool playing = Playing;
                Stop();
                await SetVideoSource(uri);
                if (playing)
                {
                    Play();
                }
                VideoSourcesChanged();
                return true;
            }
            return false;
        }

        private async Task<bool> Prev()
        {
            var uri = mSources?.Prev?.Uri;
            if (null!=uri)
            {
                bool playing = Playing;
                Stop();
                await SetVideoSource(uri);
                if (playing)
                {
                    Play();
                }
                VideoSourcesChanged();
                return true;
            }
            return false;
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
            mPositionSlider.Value = 0;
        }

        #endregion

        #region 初期化/解放
        private bool mLoaded = false;

        public WfPlayerWindow()
        {
            Duration = 1.0;
            mSources = null;
            mStarted = new UtObservableProperty<bool>("Started", false, this, "Playing", "ShowPanel");
            mPausing = new UtObservableProperty<bool>("Pausing", false, this, "Playing", "ShowPanel");
            mMouseInPanel = new UtObservableProperty<bool>("MouseInPanel", false, this, "ShowPanel");

            this.DataContext = this;
            InitializeComponent();
        }

        private async Task EnsureVideoFrame()
        {
            if (!Playing)
            {
                mMediaElement.Play();
                await Task.Delay(50);
                mMediaElement.Pause();
            }
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

        private async void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            Duration = mMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
            mMediaElement.SpeedRatio = calcSpeedRatio(Speed);
            await updateTimelinePosition(0, true, true);
            mVideoLoadingTaskSource.TrySetResult(true);
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
            mVideoLoadingTaskSource.TrySetResult(false);
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
        private async void OnPositionChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await updateTimelinePosition(e.NewValue, slider:false, player:!mUpdatingPositionFromTimer);
        }

        private async Task updateTimelinePosition(double position, bool slider, bool player)
        {
            if (player)
            {
                mMediaElement.Position = TimeSpan.FromMilliseconds(position);
                if (!Started || mDragging)
                {
                    await EnsureVideoFrame();
                }
            }
            if (slider)
            {
                mPositionSlider.Value = position;
            }
        }

        private async void OnSliderDragStateChanged(TimelineSlider.DragState state)
        {
            switch(state)
            {
                case TimelineSlider.DragState.START:
                    mDragging = true;
                    mMediaElement.Pause();
                    break;
                case TimelineSlider.DragState.DRAGGING:
                    await updateTimelinePosition(mPositionSlider.Value, slider:false, player:true);
                    break;
                case TimelineSlider.DragState.END:
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
            if (e.PropertyName == "Playing")
            {
                OnPlayingStateChanged(Playing);
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
                            mUpdatingPositionFromTimer = true;
                            mPositionSlider.Value = mMediaElement.Position.TotalMilliseconds;
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

        #endregion

        private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            mMouseInPanel.Value = true;
        }

        private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            mMouseInPanel.Value = false;
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
    }
}
