using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace wfPlayer
{
    /// <summary>
    /// WfTrimmingPlayer.xaml の相互作用ロジック
    /// </summary>
    public partial class WfTrimmingPlayer : Window, INotifyPropertyChanged
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

        private double mVolume = 0.5;
        public double Volume
        {
            get => mVolume;
            set => setProp("Volume", ref mVolume, value);
        }
        private bool mMute = true;
        public bool Mute
        {
            get => mMute;
            set => setProp("Mute", ref mMute, value);
        }
        private string mTrimmingName = "";
        public string TrimmingName
        {
            get => mTrimmingName;
            set => setProp("TrimmingName", ref mTrimmingName, value);
        }
        private double mTrimStart = 0;
        public double TrimStart
        {
            get => mTrimStart;
            set => setProp(new string[] { "TrimStart", "IsValid" }, ref mTrimStart, value);
        }
        private double mTrimEnd = 0;
        public double TrimEnd
        {
            get => mTrimEnd;
            set => setProp(new string[] { "TrimEnd", "IsValid" }, ref mTrimEnd, value);
        }

        private bool mTrimStartEnabled = false;
        public bool TrimStartEnabled
        {
            get => mTrimStartEnabled;
            set => setProp(new string[] { "TrimStartEnabled", "IsValid" }, ref mTrimStartEnabled, value);
        }
        private bool mTrimEndEnabled = false;
        public bool TrimEndEnabled
        {
            get => mTrimEndEnabled;
            set => setProp(new string[] { "TrimEndEnabled", "IsValid" }, ref mTrimEndEnabled, value);
        }
        private double mDuration = 0;
        public double Duration
        {
            get => mDuration;
            set => setProp(new string[] { "Duration", "LargePositionChange" }, ref mDuration, value);
        }

        private bool mIsModified = false;
        public bool IsModified
        {
            get => mIsModified;
            set => setProp("IsModified", ref mIsModified, value);
        }

        private bool mIsReady = false;
        public bool IsReady
        {
            get => mIsReady;
            set => setProp("IsReady", ref mIsReady, value);
        }

        private bool mStarted = false;
        public bool Started
        {
            get => mStarted;
            set => setProp(new string[] { "Started", "Playing" }, ref mStarted, value);
        }
        private bool mPausing = false;
        public bool Pausing
        {
            get => mPausing;
            set => setProp(new string[] { "Pausing", "Playing" }, ref mPausing, value);
        }
        public bool Playing
        {
            get => mStarted && !mPausing;
        }

        public double LargePositionChange => Duration / 10;
        public double SmallPositionChange => 1000;

        public bool IsValid => (TrimEndEnabled && TrimEnd > 0) || (TrimStartEnabled && TrimStart > 0);

        #endregion

        #region Private Fields
        private string mVideoPath = null;
        private TaskCompletionSource<bool> mVideoLoadingTask = null;
        private ITrim mEditingTrim = null;
        #endregion

        public ITrim Result { get; private set; } = null;

        public delegate void ResultEventProc(ITrim result, WfPlayListDB dbWithTransaction);
        public event ResultEventProc OnResult;

        private static string getPathIfExists(string path)
        {
            if(!string.IsNullOrEmpty(path))
            {
                var fi = new FileInfo(path);
                return fi.Exists ? path : null;
            }
            return null;
        }
        public static string GetRefPath(ITrim trim, string videoPath, bool preferRefreshPath)
        {
            if(preferRefreshPath)
            {
                return getPathIfExists(videoPath) ?? getPathIfExists(trim?.RefPath);
            }
            else
            {
                return getPathIfExists(trim?.RefPath) ?? getPathIfExists(videoPath);
            }
        }

        public WfTrimmingPlayer(ITrim trim, string videoPath)
        {
            mVideoPath = videoPath;

            if (null != trim) {
                mEditingTrim = trim;
                mTrimmingName = trim.Name;
                if (trim.Prologue > 0)
                {
                    mTrimStart = trim.Prologue;
                    mTrimStartEnabled = true;
                }
                if(trim.Epilogue>0)
                {
                    mTrimEnd = trim.Epilogue;
                    mTrimEndEnabled = true;
                }
            }
            DataContext = this;
            InitializeComponent();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            PropertyChanged += OnBindingPropertyChanged;
            Started = false;
            Pausing = false;
            if (null != mVideoPath)
            {
                mVideoLoadingTask = new TaskCompletionSource<bool>();
                mMediaElement.Source = new Uri(mVideoPath);
                mMediaElement.Position = TimeSpan.FromMilliseconds(0);
                mMediaElement.Play();
                if (await mVideoLoadingTask.Task)
                {
                    mMediaElement.Pause();
                    IsReady = true;
                }
                mVideoLoadingTask = null;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            PropertyChanged -= OnBindingPropertyChanged;
            mVideoLoadingTask?.TrySetResult(false);
            mVideoLoadingTask = null;
        }

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            Duration = mMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
            mVideoLoadingTask?.TrySetResult(true);
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            IsReady = false;
            mVideoLoadingTask?.TrySetResult(false);
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            mMediaElement.Stop();
            Pausing = false;
            Started = false;
        }

        private void OnPlay(object sender, RoutedEventArgs e)
        {
            if(IsReady && !Started)
            {
                mMediaElement.Play();
                Started = true;
            }
        }

        private void OnPause(object sender, RoutedEventArgs e)
        {
            if(IsReady && Started)
            {
                if(Pausing)
                {
                    mMediaElement.Play();
                }
                else
                {
                    mMediaElement.Pause();
                }
                Pausing = !Pausing;
            }
        }

        private void OnStop(object sender, RoutedEventArgs e)
        {
            if(IsReady && Started)
            {
                mMediaElement.Stop();
            }
        }

        private void OnStepBack2(object sender, RoutedEventArgs e)
        {
            double v = mPositionSlider.Value;
            v -= 500;
            if (v < 0)
            {
                v = 0;
            }
            mPositionSlider.Value = v;
        }

        private void OnStepBack(object sender, RoutedEventArgs e)
        {
            double v = mPositionSlider.Value;
            v -= 50;
            if(v<0)
            {
                v = 0;
            }
            mPositionSlider.Value = v;
        }

        private void OnStepForward(object sender, RoutedEventArgs e)
        {
            double v = mPositionSlider.Value;
            v += 50;
            if (v>mPositionSlider.Maximum)
            {
                v = mPositionSlider.Maximum;
            }
            mPositionSlider.Value = v;
        }

        private void OnStepForward2(object sender, RoutedEventArgs e)
        {
            double v = mPositionSlider.Value;
            v += 500;
            if (v > mPositionSlider.Maximum)
            {
                v = mPositionSlider.Maximum;
            }
            mPositionSlider.Value = v;
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

        private void SeekTo(double position, bool slider, bool player)
        {
            if(player)
            {
                mMediaElement.Position = TimeSpan.FromMilliseconds(position);
            }
            if (slider)
            {
                mPositionSlider.Value = position;
            }
        }
        private bool mDragging = false;
        private bool mUpdatingPositionFromTimer = false;
        private DispatcherTimer mPositionTimer = null;

        private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SeekTo(e.NewValue, slider: false, player: !mUpdatingPositionFromTimer);
        }

        private void OnSliderDragStateChanged(TimelineSlider.DragState state)
        {
            switch (state)
            {
                case TimelineSlider.DragState.START:
                    mDragging = true;
                    mMediaElement.Pause();
                    break;
                case TimelineSlider.DragState.DRAGGING:
                    SeekTo(mPositionSlider.Value, slider: false, player: true);
                    break;
                case TimelineSlider.DragState.END:
                    SeekTo(mPositionSlider.Value, slider: false, player: true);
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


        private void OnSetTrimStart(object sender, RoutedEventArgs e)
        {
            var pos = mPositionSlider.Value;
            if (0 < pos && pos < mPositionSlider.Maximum - TrimEnd)
            {
                TrimStart = pos;
                TrimStartEnabled = true;
            }
        }

        private void OnSetTrimEnd(object sender, RoutedEventArgs e)
        {
            var pos = mPositionSlider.Value;
            if (TrimStart <= pos && pos < mPositionSlider.Maximum)
            {
                TrimEnd = mPositionSlider.Maximum - pos;
                TrimEndEnabled = true;
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if(!IsValid)
            {
                return;
            }
            using (var txn = WfPlayListDB.Instance.Transaction())
            {
                Result = WfPlayListDB.Instance.TP.Register(mEditingTrim?.Id ?? 0, TrimmingName, TrimStartEnabled ? TrimStart : 0, TrimEndEnabled ? TrimEnd : 0, mVideoPath );
                OnResult?.Invoke(Result, WfPlayListDB.Instance);
            }
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Result = null;
            Close();
        }

        private void SeekToEpilogue(object sender, RoutedEventArgs e)
        {
            SeekTo(mPositionSlider.Maximum - TrimEnd, true, true);
        }

        private void SeekToPrologue(object sender, RoutedEventArgs e)
        {
            SeekTo(TrimStart, true, true);
        }
    }
}
