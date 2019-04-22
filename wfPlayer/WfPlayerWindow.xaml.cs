using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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


        private UtObservableProperty<double> mDuration;
        public double Duration
        {
            get => mDuration.Value;
            set => mDuration.Value = value;
        }

        private UtObservableProperty<double> mPosition;
        public double Position
        {
            get => mPosition.Value;
            set => mPosition.Value = value;
        }

        private UtObservableProperty<double> mVolume;
        public double Volume
        {
            get => mVolume.Value;
            set => mVolume.Value = value;
        }

        private UtObservableProperty<double> mSpeed;
        public double Speed
        {
            get => mSpeed.Value;
            set => mSpeed.Value = value;
        }

        #endregion

        public WfPlayerWindow(string path)
        {
            mDuration = new UtObservableProperty<double>("Duration", 1.0, this);
            mPosition = new UtObservableProperty<double>("Position", 0.0, this);
            mSpeed= new UtObservableProperty<double>("Speed", 1.0, this);
            mVolume = new UtObservableProperty<double>("Volume", 1.0, this);
            VideoSource = new Uri(path);

            this.DataContext = this;
            InitializeComponent();
        }

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            Duration = mMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            mMediaElement.Stop();
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }

        private void OnPlay(object sender, RoutedEventArgs e)
        {
            mMediaElement.Play();
        }

        private void OnPlayFast(object sender, RoutedEventArgs e)
        {
            Speed = 2;
            mMediaElement.Play();
        }
        private void OnPause(object sender, RoutedEventArgs e)
        {
            mMediaElement.Pause();
        }
        private void OnStop(object sender, RoutedEventArgs e)
        {
            mMediaElement.Stop();
        }

        private void OnPrev(object sender, RoutedEventArgs e)
        {
        }

        private void OnNext(object sender, RoutedEventArgs e)
        {

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
