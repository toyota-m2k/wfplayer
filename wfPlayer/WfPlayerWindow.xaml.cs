using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
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

        public static readonly DependencyProperty VideoSourceProperty = DependencyProperty.Register("VideoSource", typeof(Uri), typeof(WfPlayerWindow));
        public Uri VideoSource
        {
            get => GetValue(VideoSourceProperty) as Uri;
            set => SetValue(VideoSourceProperty, value);
        }

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

        public double LargePositionChange => Duration / 10;
        public double SmallPositionChange => 1000;


        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register("Volume", typeof(double), typeof(WfPlayerWindow), new PropertyMetadata(0.5));
        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        public static readonly DependencyProperty SpeedProperty = DependencyProperty.Register("Speed", typeof(double), typeof(WfPlayerWindow), new PropertyMetadata((double)0.5, SpeedPropertyChangedCallback));
        public double Speed
        {
            get => (double)GetValue(SpeedProperty);
            set => SetValue(SpeedProperty, value);
        }
        private static void SpeedPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as WfPlayerWindow)?.OnSpeedChanged((double)e.NewValue);
        }
        private double speedRatio(double speed)
        {
            if (speed >= 0.5)
            {
                return 1 + (speed - 0.5) * 8;
            }
            else
            {
                return 0.2 + 0.8 * (speed * 2);
            }
        }
        private void OnSpeedChanged(double newValue)
        {
            // 0.125 --> 0.25
            // 0.25 --> 0.5
            // 0.5 --> 1
            // 0.75 --> 2
            // 0.875 --> 3
            if (mMediaElement != null)
            {
                mMediaElement.SpeedRatio = speedRatio(newValue);
            }
        }


        private UtObservableProperty<bool> mPlaying;
        public bool Playing
        {
            get => mPlaying.Value;
            set => mPlaying.Value = value;
        }
        private UtObservableProperty<bool> mPausing;
        public bool Pausing
        {
            get => mPausing.Value;
            set => mPausing.Value = value;
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

        private DispatcherTimer _positionTimer = null;
        private void OnPlayingStateChanged(bool newValue)
        {
            if(newValue)
            {
                if(null==_positionTimer)
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
                if(null!=_positionTimer)
                {
                    _positionTimer.Stop();
                    _positionTimer = null;
                }
            }
        }

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            Duration = mMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
            mMediaElement.Position = TimeSpan.FromMilliseconds(mPosition);
            mMediaElement.SpeedRatio = speedRatio(Speed);
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            OnStop(sender, null);
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }

        private void OnPlay(object sender, RoutedEventArgs e)
        {
            if(Pausing)
            {
                OnPause(sender, e);
            }
            else
            {
                mMediaElement.Play();
                Playing = true;
            }
        }

        private void OnPlayFast(object sender, RoutedEventArgs e)
        {
            Speed = 0.6;
            OnPlay(sender, e);
        }

        private void OnPause(object sender, RoutedEventArgs e)
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
        private void OnStop(object sender, RoutedEventArgs e)
        {
            mMediaElement.Stop();
            Playing = false;
            Pausing = false;
            mPosition = 0;
            mPositionSlider.Value = 0;
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



        //void InitializePropertyValues()
        //{
        //    // Set the media's starting Volume and SpeedRatio to the current value of the
        //    // their respective slider controls.
        //    mMediaElement.Volume = (double)volumeSlider.Value;
        //    myMediaElement.SpeedRatio = (double)speedRatioSlider.Value;
        //}
    }
}
