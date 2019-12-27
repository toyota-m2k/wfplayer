using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
            set {
                if (setProp("TrimmingName", ref mTrimmingName, value)) {
                    IsRegisteredTrim = false;
                }
            }
        }
        private double mTrimStart = 0;
        public double TrimStart
        {
            get => mTrimStart;
            set {
                if (setProp("TrimStart", ref mTrimStart, value, "IsValid")) {
                    IsRegisteredTrim = false;
                }
            }
        }
        private double mTrimEnd = 0;
        public double TrimEnd
        {
            get => mTrimEnd;
            set {
                if (setProp("TrimEnd", ref mTrimEnd, value, "IsValid")) {
                    IsRegisteredTrim = false;
                }
            }
        }

        private bool mTrimStartEnabled = false;
        public bool TrimStartEnabled
        {
            get => mTrimStartEnabled;
            set {
                if (setProp("TrimStartEnabled", ref mTrimStartEnabled, value, "IsValid")) {
                    IsRegisteredTrim = false;
                }
            }
        }
        private bool mTrimEndEnabled = false;
        public bool TrimEndEnabled
        {
            get => mTrimEndEnabled;
            set {
                if (setProp("TrimEndEnabled", ref mTrimEndEnabled, value, "IsValid")) {
                    IsRegisteredTrim = false;
                }
            }
        }
        private double mDuration = 0;
        public double Duration
        {
            get => mDuration;
            set => setProp("Duration", ref mDuration, value, "LargePositionChange");
        }

        private bool mIsReady = false;
        public bool IsReady
        {
            get => mIsReady;
            set => setProp("IsReady", ref mIsReady, value);
        }

        private IWfSourceList mSourceList = null;
        public IWfSourceList SourceList {
            get => mSourceList;
            set {
                mSourceList = value;
                notify("EditMode");
            }
        }

        public bool EditMode {      // true:パターンリストの編集中 false:動画に対するトリミング設定
            get => mSourceList == null;
        }

        public bool HasNext => mSourceList?.HasNext ?? false;
        public bool HasPrev => mSourceList?.HasPrev?? false;

        private bool mStarted = false;
        public bool Started
        {
            get => mStarted;
            set => setProp("Started", ref mStarted, value, "Playing");
        }
        private bool mPausing = false;
        public bool Pausing
        {
            get => mPausing;
            set => setProp("Pausing", ref mPausing, value, "Playing");
        }
        public bool Playing
        {
            get => mStarted && !mPausing;
        }

        public double LargePositionChange => Duration / 10;
        public double SmallPositionChange => 1000;

        public bool IsValid => (TrimEndEnabled && TrimEnd > 0) || (TrimStartEnabled && TrimStart > 0);

        private bool mIsRegisteredTrim = false;
        public bool IsRegisteredTrim {
            get => mIsRegisteredTrim;
            set => setProp("IsRegisteredTrim", ref mIsRegisteredTrim, value);
        }

        public bool HasTarget => (EditingTrim?.Id ?? 0) > 0;

        private ITrim mEditingTrim = null;
        public ITrim EditingTrim
        {
            get => mEditingTrim;
            set => setProp("EditingTrim", ref mEditingTrim, value, "HasTarget");
        }

        #endregion

        #region Private Fields
        private string mVideoPath = null;
        private TaskCompletionSource<bool> mVideoLoadingTask = null;
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

        private void init(ITrim trim, string videoPath, IWfSourceList sourceList)
        {
            mVideoPath = videoPath;
            mSourceList = sourceList;

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
        public WfTrimmingPlayer(ITrim trim, string videoPath) {
            init(trim, videoPath, null);
        }

        public WfTrimmingPlayer(IWfSourceList source) {
            var item = source.Current;
            var path = WfTrimmingPlayer.GetRefPath(item.Trimming, item.FullPath, true);
            init(item.Trimming, path, source);
        }

        private async Task SetSourceToPlayer(string path=null) {
            Started = false;
            Pausing = false;
            if(path!=null) {
                mVideoPath = path;
            }
            if (null != mVideoPath) {
                mVideoLoadingTask = new TaskCompletionSource<bool>();
                mMediaElement.Source = new Uri(mVideoPath);
                mMediaElement.Position = TimeSpan.FromMilliseconds(0);
                mMediaElement.Play();
                if (await mVideoLoadingTask.Task) {
                    mMediaElement.Pause();
                    IsReady = true;
                }
                mVideoLoadingTask = null;
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            PropertyChanged += OnBindingPropertyChanged;
            await SetSourceToPlayer();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            PropertyChanged -= OnBindingPropertyChanged;
            mVideoLoadingTask?.TrySetResult(false);
            mVideoLoadingTask = null;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WfGlobalParams.Instance.TrimmingPlayerPlacement.ApplyPlacementTo(this);
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            WfGlobalParams.Instance.TrimmingPlayerPlacement.GetPlacementFrom(this);
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
                Result = WfPlayListDB.Instance.TP.Register(EditingTrim?.Id ?? 0, TrimmingName, TrimStartEnabled ? TrimStart : 0, TrimEndEnabled ? TrimEnd : 0, mVideoPath );
                OnResult?.Invoke(Result, WfPlayListDB.Instance);
            }
            Close();
        }

        /**
         * 新しくトリミングパターンを作成登録して、選択アイテムにセットする
         */
        private void OnRegister(object sender, RoutedEventArgs e)
        {
            if (!IsValid)
            {
                return;
            }
            using (var txn = WfPlayListDB.Instance.Transaction())
            {
                Result = WfPlayListDB.Instance.TP.Register(0, TrimmingName, TrimStartEnabled ? TrimStart : 0, TrimEndEnabled ? TrimEnd : 0, mVideoPath);
                if (Result == null) {
                    return;
                }
                OnResult?.Invoke(Result, WfPlayListDB.Instance);
                if (!EditMode) {
                    var item = SourceList.Current;
                    item.Trimming = Result;
                    item.SaveModified();
                }
            }
            if (EditMode) {
                Close();
            }
        }

        /**
         * 選択されているトリミングパターンの内容を更新してして、選択アイテムにセットする
         */
        private void OnUpdate(object sender, RoutedEventArgs e)
        {
            if (!IsValid || !HasTarget)
            {
                return;
            }
            using (var txn = WfPlayListDB.Instance.Transaction())
            {
                Result = WfPlayListDB.Instance.TP.Register(EditingTrim.Id, TrimmingName, TrimStartEnabled ? TrimStart : 0, TrimEndEnabled ? TrimEnd : 0, mVideoPath);
                if (Result == null) {
                    return;
                }
                OnResult?.Invoke(Result, WfPlayListDB.Instance);
                if(!EditMode) {
                    var item = SourceList.Current;
                    item.Trimming = Result;
                    item.SaveModified();
                }
            }
            if (EditMode) {
                Close();
            }
        }

        /**
         * 選択されているトリミングパターンを、そのまま選択アイテムにセットする
         */
        private void OnApply(object sender, RoutedEventArgs e) {
            if (EditMode) {
                return;
            }
            var trim = WfPlayListDB.Instance.TP.Get(TrimmingName);
            if(trim!=null) {
                var item = SourceList.Current;
                item.Trimming = trim;
                item.SaveModified();
            }
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

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                //case Key.Return:
                //    EditTrimming(mList.IndexOf(CurrentItem));
                //    break;
                case Key.Escape:
                    Close();
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        private void OnSelect(object sender, RoutedEventArgs e) {
            var dlg = new WfTrimmingPatternList();
            dlg.ShowDialog();
            if (null != dlg.Result && dlg.Result.Name != TrimmingName) {
                TrimmingName = dlg.Result.Name;
                TrimStart = dlg.Result.Prologue;
                TrimStartEnabled = TrimStart != 0;
                TrimEnd = dlg.Result.Epilogue;
                TrimEndEnabled = TrimEnd != 0;
                IsRegisteredTrim = true;
            }
        }

        private async Task setTargetVideo(IWfSource source) {
            if(null==source) {
                return;
            }
            var trim = source.Trimming;
            var videoPath = WfTrimmingPlayer.GetRefPath(source.Trimming, source.FullPath, true);

            await SetSourceToPlayer(videoPath);
            if (null != trim) {
                EditingTrim = trim;
                TrimmingName = trim.Name;
                TrimStart = trim.Prologue;
                TrimStartEnabled = TrimStart > 0;
                TrimEnd = trim.Epilogue;
                TrimEndEnabled = TrimEnd>0;
                IsRegisteredTrim = true;
            } else {
                IsRegisteredTrim = false;
            }

            notify("HasPrev");
            notify("HasNext");
        }

        private async void OnPrev(object sender, RoutedEventArgs e) {
            if(HasPrev) {
                await setTargetVideo(SourceList.Prev);
            }
        }

        private async void OnNext(object sender, RoutedEventArgs e) {
            if (HasNext) {
                await setTargetVideo(SourceList.Next);
            }
        }

    }
}
