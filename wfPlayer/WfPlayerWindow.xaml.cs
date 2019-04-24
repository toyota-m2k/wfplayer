using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        /**
         * VideoSource --> MediaElement.Source
         */
        public static readonly DependencyProperty VideoSourceProperty = DependencyProperty.Register("VideoSource", typeof(Uri), typeof(WfPlayerWindow));
        public Uri VideoSource
        {
            get => GetValue(VideoSourceProperty) as Uri;
            set => SetValue(VideoSourceProperty, value);
        }

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

        /**
         * 再生中 or not
         */
        private UtObservableProperty<bool> mPlaying;
        public bool Playing
        {
            get => mPlaying.Value;
            set => mPlaying.Value = value;
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

        #endregion

        #region 再生操作

        void Play()
        {
            if (Pausing)
            {
                Pause();
            }
            else
            {
                mMediaElement.Play();
                Playing = true;
            }
        }

        void Pause()
        {
            if (!Pausing)
            {
                mMediaElement.Pause();
                Playing = false;
                Pausing = true;
            }
            else
            {
                mMediaElement.Play();
                Playing = true;
                Pausing = false;
            }
        }

        void Stop()
        {
            mMediaElement.Stop();
            Playing = false;
            Pausing = false;
            mPositionSlider.Value = 0;
        }

        private DispatcherTimer _positionTimer = null;
        private void OnPlayingStateChanged(bool newValue)
        {
            if (newValue)
            {
                if (null == _positionTimer)
                {
                    _positionTimer = new DispatcherTimer();
                    _positionTimer.Tick += (s, e) =>
                    {
                        mPositionSlider.Value = mMediaElement.Position.TotalMilliseconds;
                    };
                    _positionTimer.Interval = TimeSpan.FromMilliseconds(50);
                    _positionTimer.Start();
                }
            }
            else
            {
                if (null != _positionTimer)
                {
                    _positionTimer.Stop();
                    _positionTimer = null;
                }
            }
        }

        #endregion

        public WfPlayerWindow(string path)
        {
            Duration = 1.0;
            VideoSource = new Uri(path);

            mPlaying = new UtObservableProperty<bool>("Playing", false, this);
            mPlaying.ValueChanged += OnPlayingStateChanged;
            mPausing = new UtObservableProperty<bool>("Pausing", false, this);

            this.DataContext = this;
            InitializeComponent();
        }

        #region Event Handlers

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            Duration = mMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
            mMediaElement.Position = TimeSpan.FromMilliseconds(mPosition);
            mMediaElement.SpeedRatio = calcSpeedRatio(Speed);
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {

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

        private void OnPrev(object sender, RoutedEventArgs e)
        {
        }

        private void OnNext(object sender, RoutedEventArgs e)
        {

        }

        private double mPosition = 0;
        private async void OnPositionChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var v = e.NewValue;
            if(v!=mPosition)
            {
                mMediaElement.Position = TimeSpan.FromMilliseconds(v);
                if(!Playing)
                {
                    mMediaElement.Play();
                    await Task.Delay(10);
                    mMediaElement.Stop();
                }
            }
        }
        #endregion

        private void OnResetSpeed(object sender, RoutedEventArgs e)
        {
            var btn = sender as Path;
        }

        private void OnMute(object sender, RoutedEventArgs e)
        {

        }
    }
}
