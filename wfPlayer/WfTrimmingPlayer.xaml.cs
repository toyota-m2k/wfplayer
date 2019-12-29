using Reactive.Bindings;
using System;
using System.ComponentModel;
using System.IO;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace wfPlayer {
    /// <summary>
    /// WfTrimmingPlayer.xaml の相互作用ロジック
    /// </summary>
    public partial class WfTrimmingPlayer : Window, INotifyPropertyChanged {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify(string propName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private bool setProp<T>(string name, ref T field, T value, params string[] familyProperties) {
            if (!field.Equals(value)) {
                field = value;
                notify(name);
                foreach (var p in familyProperties) {
                    notify(p);
                }
                return true;
            }
            return false;
        }

        public void NotifyPropertyChanged(string propName) {
            notify(propName);
        }

        #endregion

        #region Binding Properties
        class TrimViewModel : WfViewModelBase, ITimelineOwnerPlayer {
            
            #region Construction
            // Owner
            private WeakReference<WfTrimmingPlayer> mOwner;
            private WfTrimmingPlayer Owner => mOwner?.GetValue();
            private Window OwnerWindow => Window.GetWindow(Owner);

            // コンストラクタ
            public TrimViewModel(ITrim trim, string videoPath, IWfSourceList sourceList, WfTrimmingPlayer owner) {
                mOwner = new WeakReference<WfTrimmingPlayer>(owner);

                InitializeProperties();
                VideoPath = videoPath;
                SourceList = sourceList;

                if (null != trim) {
                    OriginalTrim = trim;
                    SetTrimSettingsWith(trim);
                }
            }

            void InitializeProperties() {
                SelectedTrim.Subscribe((v) => {
                    SetTrimSettingsWith(v);
                });
                Prologue.Subscribe((v) => {
                    UpdateButtonStatus();
                });
                Epilogue.Subscribe((v) => {
                    UpdateButtonStatus();
                });
                PrologueEnabled.Subscribe((v) => {
                    UpdateButtonStatus();
                });
                EpilogueEnabled.Subscribe((v) => {
                    UpdateButtonStatus();
                });
                TrimmingName.Subscribe((v) => {
                    UpdateButtonStatus();
                });
                Started.Subscribe((V) => {
                    UpdatePlayingState();
                });
                Pausing.Subscribe((v) => {
                    UpdatePlayingState();
                });
                Duration.Subscribe((v) => {
                });

                CommandRegister.Subscribe(ExecRegister);
                CommandApply.Subscribe(ExecApply);
                CommandUpdate.Subscribe(ExecUpdate);
                CommandUndo.Subscribe(ExecUndo);
                CommandCancel.Subscribe(ExecCancel);
                CommandSelect.Subscribe(ExecSelect);
            }

            #endregion

            #region 状態更新

            void SetTrimSettingsWith(ITrim trim) {
                if (!IsValidTrim(trim)) {
                    if (IsValidTrim(OriginalTrim)) {
                        SetTrimSettingsWith(OriginalTrim);
                    } else {
                        TrimmingName.Value = "";
                        Prologue.Value = 0;
                        PrologueEnabled.Value = false;
                        Epilogue.Value = 0;
                        EpilogueEnabled.Value = false;
                    }
                } else {
                    TrimmingName.Value = trim.Name;
                    if (trim.Prologue > 0) {
                        Prologue.Value = trim.Prologue;
                        PrologueEnabled.Value = true;
                    } else {
                        Prologue.Value = 0;
                        PrologueEnabled.Value = false;
                    }
                    if (trim.Epilogue > 0) {
                        Epilogue.Value = trim.Epilogue;
                        EpilogueEnabled.Value = true;
                    } else {
                        Epilogue.Value = 0;
                        EpilogueEnabled.Value = false;
                    }
                }
                UpdateButtonStatus();
            }

            private void UpdatePlayingState() {
                Playing.Value = Started.Value && !Pausing.Value;
            }

            public void UpdateNextPrev() {
                HasNext.Value = mSourceList?.HasNext ?? false;
                HasPrev.Value = mSourceList?.HasPrev ?? false;
            }

            // ボタン類の有効性チェック
            private void UpdateButtonStatus() {
                IsAvailable.Value = _IsAvailable;
                CanRegister.Value = _CanRegister;
                CanApply.Value = _CanApply;
                CanUpdate.Value = _CanUpdate;
                CanReset.Value = _CanReset;
                CanUndo.Value = _CanUndo;
            }

            #endregion

            #region Properties

            public ReactiveProperty<double> Volume { get; } = new ReactiveProperty<double>(0.5);
            public ReactiveProperty<bool> Mute { get; } = new ReactiveProperty<bool>(true);

            // もともとアイテムに設定されていたTrimPattern
            private ITrim mOriginalTrim = null;
            public ITrim OriginalTrim {
                get => mOriginalTrim;
                set {
                    mOriginalTrim = value;
                    SelectedTrim.Value = value;
                }
            }

            // 選択されたTrimPattern
            public ReactiveProperty<ITrim> SelectedTrim { get; } = new ReactiveProperty<ITrim>(null);

            // Trimming Settings
            public ReactiveProperty<long> Prologue { get; } = new ReactiveProperty<long>(0);
            public ReactiveProperty<long> Epilogue { get; } = new ReactiveProperty<long>(0);
            public ReactiveProperty<bool> PrologueEnabled { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> EpilogueEnabled { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<string> TrimmingName { get; } = new ReactiveProperty<string>("");

            public ReactiveProperty<double> Duration { get; } = new ReactiveProperty<double>(0);
            public ReactiveProperty<bool> IsReady { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> EditMode { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> HasNext { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> HasPrev { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> Continuous { get; } = new ReactiveProperty<bool>(false);

            public ReactiveProperty<bool> Started { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> Pausing { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> Playing { get; } = new ReactiveProperty<bool>(false);

            public long RealPrologue => PrologueEnabled.Value ? Prologue.Value : 0;
            public long RealEpilogue => EpilogueEnabled.Value ? Epilogue.Value : 0;
            public ReactiveProperty<ITrim> ResultTrim = new ReactiveProperty<ITrim>(null, ReactivePropertyMode.None);

            #endregion

            #region Commands

            // Commands
            public ReactiveProperty<bool> IsAvailable { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> CanRegister { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> CanUpdate { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> CanApply { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> CanReset { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> CanUndo { get; } = new ReactiveProperty<bool>(false);

            public ReactiveCommand CommandUndo { get; } = new ReactiveCommand();
            public ReactiveCommand CommandApply { get; } = new ReactiveCommand();
            public ReactiveCommand CommandUpdate { get; } = new ReactiveCommand();
            public ReactiveCommand CommandRegister { get; } = new ReactiveCommand();
            public ReactiveCommand CommandCancel { get; } = new ReactiveCommand();
            public ReactiveCommand CommandSelect { get; } = new ReactiveCommand();

            public double LargePositionChange => 5000;
            public double SmallPositionChange => 1000;

            // 連続設定用のトリミング情報を取得
            //  - 登録済みのトリミングが選択
            //  - もともと設定されていたトリミング
            public ITrim GetContinuousTrim() {
                var trim = SelectedTrim.Value;
                if (!IsValidTrim(trim)) {
                    trim = OriginalTrim;
                }
                return trim;
            }

            public string VideoPath { get; set; } = null;
            public Subject<bool> FeedNext = new Subject<bool>();
            private IWfSourceList mSourceList = null;
            public IWfSourceList SourceList {
                get => mSourceList;
                set {
                    mSourceList = value;
                    EditMode.Value = value == null;
                    UpdateNextPrev();
                }
            }

            // Trimming設定は有効か？
            private bool _IsAvailable {
                get {
                    return !string.IsNullOrWhiteSpace(TrimmingName.Value)
                        && (PrologueEnabled.Value || EpilogueEnabled.Value);
                }
            }

            // 新規登録可能か？
            // Trimming設定が有効で、且つ、選択されているパターン名と異なる
            private bool _CanRegister {
                get {
                    return _IsAvailable
                        && TrimmingName.Value != SelectedTrim?.Value?.Name;
                }
            }

            private static bool IsValidTrim(ITrim trim) {
                return trim != null && trim.HasValue;
            }

            private bool IsSelectedTrimModified {
                get {
                    return IsValidTrim(SelectedTrim.Value)
                        && SelectedTrim.Value.Name == TrimmingName.Value
                        && (SelectedTrim.Value.Prologue != RealPrologue
                          || SelectedTrim.Value.Epilogue != RealEpilogue);
                }
            }

            // TrimmingPatternの更新は可能か？
            private bool _CanUpdate {
                get {
                    return _IsAvailable
                        && IsSelectedTrimModified;
                }
            }

            private bool _CanApply {
                get {
                    return _IsAvailable
                        && IsValidTrim(SelectedTrim.Value)
                        && SelectedTrim.Value.Name == TrimmingName.Value
                        && !IsSelectedTrimModified;
                }
            }

            private bool _CanReset {
                get {
                    return IsValidTrim(OriginalTrim);
                }
            }

            private bool CanUndoToSelectedTrim {
                get {
                    return IsValidTrim(SelectedTrim.Value)
                        && (SelectedTrim.Value.Prologue != RealPrologue
                          || SelectedTrim.Value.Epilogue != RealEpilogue
                          || SelectedTrim.Value.Name != TrimmingName.Value);
                }
            }

            private bool _CanUndo {
                get {
                    return CanUndoToSelectedTrim
                        || (IsValidTrim(OriginalTrim) && !OriginalTrim.Equals(SelectedTrim.Value));
                }
            }


            /**
             * 新しくトリミングパターンを作成登録して、選択アイテムにセットする
             */
            private void ExecRegister() {
                if (!CanRegister.Value) {
                    return;
                }
                using (var txn = WfPlayListDB.Instance.Transaction()) {
                    var trim = WfPlayListDB.Instance.TP.Register(0, TrimmingName.Value, RealPrologue, RealEpilogue, VideoPath);
                    if (trim == null) {
                        return;
                    }
                    ResultTrim.Value = trim;
                    if (!EditMode.Value) {
                        var item = SourceList.Current;
                        item.Trimming = trim;
                        item.SaveModified();
                    }
                    SelectedTrim.Value = trim;
                    FeedNext.OnNext(true);
                }
            }

            /**
             * 選択されているトリミングパターンの内容を更新してして、選択アイテムにセットする
             */
            private void ExecUpdate() {
                if (!CanUpdate.Value) {
                    return;
                }
                using (var txn = WfPlayListDB.Instance.Transaction()) {
                    var trim = WfPlayListDB.Instance.TP.Register(SelectedTrim.Value.Id, TrimmingName.Value, RealPrologue, RealEpilogue, VideoPath);
                    if (trim == null) {
                        return;
                    }

                    ResultTrim.Value = trim;
                    if (!EditMode.Value) {
                        var item = SourceList.Current;
                        item.Trimming = trim;
                        item.SaveModified();
                    }
                    SelectedTrim.Value = trim;
                    FeedNext.OnNext(true);
                }
            }

            /**
             * 選択されているトリミングパターンを、そのまま選択アイテムにセットする
             */
            private void ExecApply() {
                if (!CanApply.Value) {
                    return;
                }
                var trim = WfPlayListDB.Instance.TP.Get(TrimmingName.Value);
                if (trim != null) {
                    var item = SourceList.Current;
                    item.Trimming = trim;
                    item.SaveModified();
                    FeedNext.OnNext(true);
                    SelectedTrim.Value = trim;
                }
            }


            private void ExecCancel() {
                ResultTrim.Value = null;
                FeedNext.OnNext(false);
            }

            private void ExecSelect() {
                var dlg = new WfTrimmingPatternList();
                dlg.ShowDialog();
                dlg.Owner = OwnerWindow;
                if (null != dlg.Result) {
                    SelectedTrim.Value = dlg.Result;
                }
            }

            private void ExecUndo() {
                if (CanUndoToSelectedTrim) {
                    SetTrimSettingsWith(SelectedTrim.Value);
                } else if (OriginalTrim != null && !OriginalTrim.Equals(SelectedTrim.Value)) {
                    SelectedTrim.Value = null;
                }
            }

            #endregion

            #region ITimelineOwnerPlayer

            // 再生状態の変更監視(true:再生中 / false:停止中）
            public IObservable<bool> IsPlayingProperty => Playing;
            // 再生時間
            public IObservable<double> DurationProperty => Duration;

            // MediaPlayer のシーク位置
            public double SeekPosition {
                get {
                    var owner = Owner;
                    if (owner != null) {
                        var pos = owner.mMediaElement.Position.TotalMilliseconds;
                        return pos;
                    }
                    return 0;
                }
                set {
                    var owner = Owner;
                    if (owner != null) {
                        owner.mMediaElement.Position = TimeSpan.FromMilliseconds(value);
                    }
                }
            }

            // MediaPlayer の再生開始
            public void Play() {
                Owner?.mMediaElement?.Play();
            }
            // MediaPlayer の再生中断
            public void Pause() {
                Owner?.mMediaElement?.Pause();
            }

            #endregion
        }
        #endregion

        #region Fields

        private TaskCompletionSource<bool> mVideoLoadingTask = null;
        public delegate void ResultEventProc(ITrim result, WfPlayListDB dbWithTransaction);
        public event ResultEventProc OnResult;
        public ITrim Result;
        
        #endregion

        private TrimViewModel ViewModel {
            get => DataContext as TrimViewModel;
            set => DataContext = value;
        }

        //public ITrim Result { get; private set; } = null;



        private static string getPathIfExists(string path) {
            if (!string.IsNullOrEmpty(path)) {
                var fi = new FileInfo(path);
                return fi.Exists ? path : null;
            }
            return null;
        }
        public static string GetRefPath(ITrim trim, string videoPath, bool preferRefreshPath) {
            if (preferRefreshPath) {
                return getPathIfExists(videoPath) ?? getPathIfExists(trim?.RefPath);
            } else {
                return getPathIfExists(trim?.RefPath) ?? getPathIfExists(videoPath);
            }
        }

        private void InitViewModel(ITrim trim, string videoPath, IWfSourceList sourceList) {
            ViewModel = new TrimViewModel(trim, videoPath, sourceList, this);
            ViewModel.ResultTrim.Subscribe((v) => {
                Result = v;
                if (v != null) {
                    OnResult?.Invoke(v, WfPlayListDB.Instance);
                }
            });
            ViewModel.FeedNext.Subscribe((v) => {
                if (!v || ViewModel.EditMode.Value) {
                    Close();
                } else {
                    MoveNext(true);
                }
            });
            InitializeComponent();
        }


        public WfTrimmingPlayer(ITrim trim, string videoPath) {
            InitViewModel(trim, videoPath, null);
        }

        public WfTrimmingPlayer(IWfSourceList source) {
            var item = source.Current;
            var path = WfTrimmingPlayer.GetRefPath(item.Trimming, item.FullPath, true);
            InitViewModel(item.Trimming, path, source);
        }

        private async Task SetSourceToPlayer(string path = null) {
            ViewModel.Started.Value = false;
            ViewModel.Pausing.Value = false;
            if (path != null) {
                ViewModel.VideoPath = path;
            }
            if (ViewModel.VideoPath != null) {
                mVideoLoadingTask = new TaskCompletionSource<bool>();
                mMediaElement.Source = new Uri(ViewModel.VideoPath);
                mMediaElement.Position = TimeSpan.FromMilliseconds(0);
                mPositionSlider.Value = 0;
                mMediaElement.Play();
                if (await mVideoLoadingTask.Task) {
                    mMediaElement.Pause();
                    ViewModel.IsReady.Value = true;
                }
                mVideoLoadingTask = null;
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e) {
            await SetSourceToPlayer();
            mPositionSlider.Initialize(ViewModel);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ViewModel.Dispose();
            mVideoLoadingTask?.TrySetResult(false);
            mVideoLoadingTask = null;
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            WfGlobalParams.Instance.TrimmingPlayerPlacement.ApplyPlacementTo(this);
        }

        private void OnClosing(object sender, CancelEventArgs e) {
            WfGlobalParams.Instance.TrimmingPlayerPlacement.GetPlacementFrom(this);
        }

        private void OnMediaOpened(object sender, RoutedEventArgs e) {
            ViewModel.Duration.Value = mMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
            mVideoLoadingTask?.TrySetResult(true);
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e) {
            ViewModel.IsReady.Value = false;
            mVideoLoadingTask?.TrySetResult(false);
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e) {
            mMediaElement.Stop();
            ViewModel.Pausing.Value = false;
            ViewModel.Started.Value = false;
        }

        public void ExecPlay() {
            if (ViewModel.IsReady.Value && !ViewModel.Started.Value) {
                mMediaElement.Play();
                ViewModel.Started.Value = true;
            }
        }
        public void ExecPause() {
            if (ViewModel.IsReady.Value && ViewModel.Started.Value) {
                if (ViewModel.Pausing.Value) {
                    mMediaElement.Play();
                } else {
                    mMediaElement.Pause();
                }
                ViewModel.Pausing.Value = !ViewModel.Pausing.Value;
            }
        }

        private void OnPlay(object sender, RoutedEventArgs e) {
            ExecPlay();
        }

        private void OnPause(object sender, RoutedEventArgs e) {
            ExecPause();
        }

        private void OnStop(object sender, RoutedEventArgs e) {
            if (ViewModel.IsReady.Value && ViewModel.Started.Value) {
                mMediaElement.Stop();
            }
        }

        private void OnStepBack2(object sender, RoutedEventArgs e) {
            double v = mPositionSlider.Value;
            v -= 500;
            if (v < 0) {
                v = 0;
            }
            mPositionSlider.Value = v;
        }

        private void OnStepBack(object sender, RoutedEventArgs e) {
            double v = mPositionSlider.Value;
            v -= 50;
            if (v < 0) {
                v = 0;
            }
            mPositionSlider.Value = v;
        }

        private void OnStepForward(object sender, RoutedEventArgs e) {
            double v = mPositionSlider.Value;
            v += 50;
            if (v > mPositionSlider.Maximum) {
                v = mPositionSlider.Maximum;
            }
            mPositionSlider.Value = v;
        }

        private void OnStepForward2(object sender, RoutedEventArgs e) {
            double v = mPositionSlider.Value;
            v += 500;
            if (v > mPositionSlider.Maximum) {
                v = mPositionSlider.Maximum;
            }
            mPositionSlider.Value = v;
        }

        private async Task EnsureVideoFrame() {
            if (!ViewModel.Playing.Value) {
                mMediaElement.Play();
                await Task.Delay(50);
                mMediaElement.Pause();
            }
        }

        private void SeekTo(double position, bool slider, bool player) {
            if (player) {
                mMediaElement.Position = TimeSpan.FromMilliseconds(position);
            }
            if (slider) {
                mPositionSlider.Value = position;
            }
        }
        //private bool mDragging = false;
        //private bool mUpdatingPositionFromTimer = false;
        //private DispatcherTimer mPositionTimer = null;

        //private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        //{
        //    SeekTo(e.NewValue, slider: false, player: !mUpdatingPositionFromTimer);
        //}

        //private void OnSliderDragStateChanged(TimelineSliderOld.DragState state)
        //{
        //    switch (state)
        //    {
        //        case TimelineSliderOld.DragState.START:
        //            mDragging = true;
        //            mMediaElement.Pause();
        //            break;
        //        case TimelineSliderOld.DragState.DRAGGING:
        //            SeekTo(mPositionSlider.Value, slider: false, player: true);
        //            break;
        //        case TimelineSliderOld.DragState.END:
        //            SeekTo(mPositionSlider.Value, slider: false, player: true);
        //            mDragging = false;
        //            if (ViewModel.Playing.Value)
        //            {
        //                mMediaElement.Play();
        //            }
        //            break;
        //    }
        //}

        //private void OnPlayingStateChanged(bool newValue)
        //{
        //    if (newValue)
        //    {
        //        if (null == mPositionTimer)
        //        {
        //            mPositionTimer = new DispatcherTimer();
        //            mPositionTimer.Tick += (s, e) =>
        //            {
        //                if (!mDragging)
        //                {
        //                    mUpdatingPositionFromTimer = true;
        //                    mPositionSlider.Value = mMediaElement.Position.TotalMilliseconds;
        //                    mUpdatingPositionFromTimer = false;
        //                }
        //            };
        //            mPositionTimer.Interval = TimeSpan.FromMilliseconds(50);
        //            mPositionTimer.Start();
        //        }
        //    }
        //    else
        //    {
        //        if (null != mPositionTimer)
        //        {
        //            mPositionTimer.Stop();
        //            mPositionTimer = null;
        //        }
        //    }
        //}


        private void OnSetTrimStart(object sender, RoutedEventArgs e) {
            var pos = mPositionSlider.Value;
            if (0 < pos && pos < mPositionSlider.Maximum - ViewModel.Epilogue.Value) {
                ViewModel.Prologue.Value = (long)pos;
                ViewModel.PrologueEnabled.Value = true;
            }
        }

        private void OnSetTrimEnd(object sender, RoutedEventArgs e) {
            var pos = mPositionSlider.Value;
            if (ViewModel.Epilogue.Value <= pos && pos < mPositionSlider.Maximum) {
                ViewModel.Epilogue.Value = (long)(mPositionSlider.Maximum - pos);
                ViewModel.EpilogueEnabled.Value = true;
            }
        }

        //private void OnOk(object sender, RoutedEventArgs e)
        //{
        //    if(!IsValid)
        //    {
        //        return;
        //    }
        //    using (var txn = WfPlayListDB.Instance.Transaction())
        //    {
        //        Result = WfPlayListDB.Instance.TP.Register(EditingTrim?.Id ?? 0, TrimmingName, TrimStartEnabled ? TrimStart : 0, TrimEndEnabled ? TrimEnd : 0, mVideoPath );
        //        OnResult?.Invoke(Result, WfPlayListDB.Instance);
        //    }
        //    Close();
        //}

        ///**
        // * 新しくトリミングパターンを作成登録して、選択アイテムにセットする
        // */
        //private void OnRegister(object sender, RoutedEventArgs e)
        //{
        //    if (!ViewModel.CanRegister.Value)
        //    {
        //        return;
        //    }
        //    using (var txn = WfPlayListDB.Instance.Transaction())
        //    {
        //        Result = WfPlayListDB.Instance.TP.Register(0, ViewModel.TrimmingName.Value, ViewModel.RealPrologue, ViewModel.RealEpilogue, ViewModel.VideoPath);
        //        if (Result == null) {
        //            return;
        //        }
        //        OnResult?.Invoke(Result, WfPlayListDB.Instance);
        //        if (!ViewModel.EditMode.Value) {
        //            var item = ViewModel.SourceList.Current;
        //            item.Trimming = Result;
        //            item.SaveModified();
        //        }
        //    }
        //    if (ViewModel.EditMode.Value) {
        //        Close();
        //    }
        //}

        ///**
        // * 選択されているトリミングパターンの内容を更新してして、選択アイテムにセットする
        // */
        //private void OnUpdate(object sender, RoutedEventArgs e)
        //{
        //    if (!ViewModel.CanUpdate.Value)
        //    {
        //        return;
        //    }
        //    using (var txn = WfPlayListDB.Instance.Transaction())
        //    {
        //        Result = WfPlayListDB.Instance.TP.Register(ViewModel.SelectedTrim.Value.Id, ViewModel.TrimmingName.Value, ViewModel.RealPrologue, ViewModel.RealEpilogue, ViewModel.VideoPath);
        //        if (Result == null) {
        //            return;
        //        }
        //        OnResult?.Invoke(Result, WfPlayListDB.Instance);
        //        if(!ViewModel.EditMode.Value) {
        //            var item = ViewModel.SourceList.Current;
        //            item.Trimming = Result;
        //            item.SaveModified();
        //        }
        //    }
        //    if (ViewModel.EditMode.Value) {
        //        Close();
        //    }
        //}

        ///**
        // * 選択されているトリミングパターンを、そのまま選択アイテムにセットする
        // */
        //private void OnApply(object sender, RoutedEventArgs e) {
        //    if (ViewModel.CanApply.Value) {
        //        return;
        //    }
        //    var trim = WfPlayListDB.Instance.TP.Get(ViewModel.TrimmingName.Value);
        //    if(trim!=null) {
        //        var item = ViewModel.SourceList.Current;
        //        item.Trimming = trim;
        //        item.SaveModified();
        //    }
        //}


        //private void OnCancel(object sender, RoutedEventArgs e)
        //{
        //    Result = null;
        //    Close();
        //}

        private void SeekToEpilogue(object sender, RoutedEventArgs e) {
            SeekTo(ViewModel.Duration.Value - ViewModel.Epilogue.Value, true, true);
        }

        private void SeekToPrologue(object sender, RoutedEventArgs e) {
            SeekTo(ViewModel.Prologue.Value, true, true);
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            switch (e.Key) {
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

        //private void OnSelect(object sender, RoutedEventArgs e) {
        //    var dlg = new WfTrimmingPatternList();
        //    dlg.ShowDialog();
        //    if (null != dlg.Result) {
        //        ViewModel.SelectedTrim.Value = dlg.Result;
        //    }
        //}

        private async Task SetTargetVideo(IWfSource source) {
            if (null == source) {
                return;
            }
            var trim = source.Trimming;
            var videoPath = WfTrimmingPlayer.GetRefPath(source.Trimming, source.FullPath, true);

            await SetSourceToPlayer(videoPath);
            if (null != trim) {
                ViewModel.OriginalTrim = trim;
                ViewModel.SelectedTrim.Value = trim;
            } else {
                ViewModel.OriginalTrim = null;
            }
            ViewModel.UpdateNextPrev();
        }

        private async void MoveNext(bool closeIfEnd) {
            if (ViewModel.HasNext.Value) {
                var trim = ViewModel.GetContinuousTrim();
                await SetTargetVideo(ViewModel.SourceList.Next);
                if (ViewModel.Continuous.Value) {
                    ViewModel.SelectedTrim.Value = trim;
                }
            } else if (closeIfEnd) {
                Close();
            }
        }
        private async void MovePrev(bool closeIfTop) {
            if (ViewModel.HasPrev.Value) {
                var trim = ViewModel.GetContinuousTrim();
                await SetTargetVideo(ViewModel.SourceList.Prev);
                if (ViewModel.Continuous.Value) {
                    ViewModel.SelectedTrim.Value = trim;
                }
            } else if (closeIfTop) {
                Close();
            }

        }
        private void OnPrev(object sender, RoutedEventArgs e) {
            MovePrev(false);
        }

        private void OnNext(object sender, RoutedEventArgs e) {
            MoveNext(false);
        }


    }
}
