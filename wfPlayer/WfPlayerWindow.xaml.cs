using common;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using wfPlayer.server;

namespace wfPlayer {
    /**
     * 画面サイズ/伸張モード
     */
    public enum WfStretchMode {
        UniformToFill,
        Uniform,
        Fill,
        CUSTOM125,
        CUSTOM133,
        CUSTOM150,
        CUSTOM177,
    }

    /**
     * ビューモデル
     */
    public class WfPlayerViewModel : MicViewModelBase {
        public ReactiveProperty<double> Duration { get; } = new ReactiveProperty<double>(1.0);
        public ReactiveProperty<double> Volume { get; } = new ReactiveProperty<double>(0.5);
        public ReactiveProperty<bool> Mute { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<double> Speed { get; } = new ReactiveProperty<double>(0.5);
        public ReactiveProperty<double> SeekPosition { get; } = new ReactiveProperty<double>(0);        // 表示専用

        public ReactiveProperty<bool> Ready { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> Started { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> Pausing { get; } = new ReactiveProperty<bool>(false);
        public ReadOnlyReactiveProperty<bool> Playing { get; }

        // パネルの開閉用
        public ReactiveProperty<bool> MouseInPanel { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> MouseInStretchModePanel { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> MouseInSliderPanel { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> MouseInSizingPanel { get; } = new ReactiveProperty<bool>(false);

        public ReadOnlyReactiveProperty<double> LargePositionChange { get; }
        public ReactiveProperty<double> SmallPositionChange { get; } = new ReactiveProperty<double>(1000);

        public ReadOnlyReactiveProperty<bool> ShowPanel { get; }
        public ReadOnlyReactiveProperty<bool> ShowSliderPanel { get; }
        public ReadOnlyReactiveProperty<bool> ShowStretchModePanel { get; }
        public ReadOnlyReactiveProperty<bool> ShowSizingPanel { get; }

        public ReadOnlyReactiveProperty<bool> CusorManagerEnabled { get; }

        public ReactiveProperty<IWfSourceList> Sources { get; } = new ReactiveProperty<IWfSourceList>();
        public ReactiveProperty<IWfSource> Current { get; } = new ReactiveProperty<IWfSource>();
        public ReadOnlyReactiveProperty<bool> HasPrev { get; }
        public ReadOnlyReactiveProperty<bool> HasNext { get; }

        public WfStretchMode StandardStretchMode { get; private set; } = WfStretchMode.UniformToFill;
        public ReactiveProperty<WfStretchMode> StretchMode { get; } = new ReactiveProperty<WfStretchMode>(WfStretchMode.UniformToFill);
        public ReadOnlyReactiveProperty<bool> CustomStretchMode { get; }
        public ReactiveProperty<bool> StretchMaximum { get; } = new ReactiveProperty<bool>(true);
        public ReactiveProperty<bool> SaveAspectAuto { get; } = new ReactiveProperty<bool>(true);
        //public ReadOnlyReactiveProperty<bool> SaveAspectTrigger { get; }

        public ReactiveProperty<bool> SliderPinned { get; } = new ReactiveProperty<bool>(true);

        public ReadOnlyReactiveProperty<string> DurationText { get; } // => FormatDuration(Duration);
        public ReadOnlyReactiveProperty<string> SeekPositionText { get; } // => FormatDuration(Duration);

        public ReactiveProperty<bool> WinMaximized { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> AllowRemoteControl { get; } = new ReactiveProperty<bool>(true);

        public ReactiveCommand CommandMaximize { get; } = new ReactiveCommand();
        public ReactiveCommand CommandPlay { get; } = new ReactiveCommand();
        public ReactiveCommand CommandPlayFast { get; } = new ReactiveCommand();
        public ReactiveCommand CommandPause { get; } = new ReactiveCommand();
        public ReactiveCommand CommandStop { get; } = new ReactiveCommand();
        public ReactiveCommand CommandPrev { get; } = new ReactiveCommand();
        public ReactiveCommand CommandNext { get; } = new ReactiveCommand();
        public ReactiveCommand CommandResetSpeed { get; } = new ReactiveCommand();
        public ReactiveCommand CommandSetTrimStart { get; } = new ReactiveCommand();
        public ReactiveCommand CommandSetTrimEnd{ get; } = new ReactiveCommand();
        public ReactiveCommand CommandResetTrimStart { get; } = new ReactiveCommand();
        public ReactiveCommand CommandResetTrimEnd { get; } = new ReactiveCommand();

        public WfPlayerViewModel() {
            Playing = Started.CombineLatest(Pausing, (s, p) => s && !p).ToReadOnlyReactiveProperty();
            LargePositionChange = Duration.Select((d) => d / 10).ToReadOnlyReactiveProperty();

            ShowPanel = MouseInPanel.CombineLatest(MouseInSliderPanel, Playing, (panel, slider, playing) => panel || slider || !playing).ToReadOnlyReactiveProperty();
            ShowSliderPanel = MouseInSliderPanel.CombineLatest(MouseInPanel, Playing, SliderPinned, (slider, panel, playing, pin) => slider || panel || !playing || pin).ToReadOnlyReactiveProperty();
            ShowStretchModePanel = MouseInStretchModePanel.CombineLatest(Playing, (panel, playing) => panel || !playing).ToReadOnlyReactiveProperty();
            ShowSizingPanel = MouseInSizingPanel.CombineLatest(Playing, (panel, playing) => panel || !playing).ToReadOnlyReactiveProperty();

            CusorManagerEnabled = Playing.CombineLatest(MouseInPanel, MouseInSliderPanel, MouseInStretchModePanel, MouseInSizingPanel, (p, a, b, c, d) => p && !a && !b && !c && !d).ToReadOnlyReactiveProperty();

            HasNext = Sources.Select((c) => c?.HasNext ?? false).ToReadOnlyReactiveProperty();
            HasPrev = Sources.Select((c) => c?.HasPrev ?? false).ToReadOnlyReactiveProperty();

            DurationText = Duration.Select((d) => FormatDuration(d)).ToReadOnlyReactiveProperty();
            SeekPositionText = SeekPosition.Select((d) => FormatDuration(d)).ToReadOnlyReactiveProperty();

            CustomStretchMode = StretchMode.Select((s) => IsCustomStretchMode(s)).ToReadOnlyReactiveProperty();
            //SaveAspectTrigger = StretchMode.CombineLatest(SaveAspectAuto, (m, a) => a).ToReadOnlyReactiveProperty();
            StretchMode.Subscribe((m) => {
                if (!IsCustomStretchMode(m)) {
                    StandardStretchMode = m;
                }
                if(SaveAspectAuto.Value) {
                    var item = Current?.Value;
                    if (item != null) {
                        item.Aspect = StretchMode2Aspect(StretchMode.Value);
                    }
                }
            });
            Current.Subscribe((c) => {
                if (c != null) {
                    StretchMode.Value = Aspect2StretchMode(c.Aspect);
                }
            });

            CommandMaximize.Subscribe(() => WinMaximized.Value = !WinMaximized.Value);
            CommandResetSpeed.Subscribe(() => Speed.Value = 0.5);
            //CommandSetTrimStart.Subscribe(() => Current.Value?.Apply((c)=>c.TrimStart = (long)SeekPosition.Value));
            //CommandResetTrimStart.Subscribe(() => Current.Value?.Apply((c)=>c.TrimStart = 0));
            //CommandSetTrimEnd.Subscribe(() => Current.Value?.Apply((c)=>c.TrimEnd = (long)(Duration.Value - SeekPosition.Value)));
            //CommandResetTrimEnd.Subscribe(() => Current.Value?.Apply((c)=>c.TrimEnd = 0));

            CommandSetTrimStart.CombineLatest(Current.Where((c) => c != null), (s, c) => c).Subscribe((c) => c.TrimStart = (long)SeekPosition.Value);
            CommandSetTrimEnd.CombineLatest(Current.Where((c) => c != null), (s, c) => c).Subscribe((c) => c.TrimEnd= (long)(Duration.Value - SeekPosition.Value));
            CommandResetTrimStart.CombineLatest(Current.Where((c) => c != null), (s, c) => c).Subscribe((c) => c.TrimStart= 0);
            CommandResetTrimEnd.CombineLatest(Current.Where((c) => c != null), (s, c) => c).Subscribe((c) => c.TrimEnd = 0);

        }

        public string FormatDuration(double duration) {
            var t = TimeSpan.FromMilliseconds(duration);
            return string.Format("{0}:{1:00}:{2:00}", t.Hours, t.Minutes, t.Seconds);
            //$"{t.Hours}:{t.Minutes}.{t.Seconds}";

        }

        private static bool IsCustomStretchMode(WfStretchMode mode) {
            return (long)mode > (long)WfStretchMode.Fill;
        }

        public static WfAspect StretchMode2Aspect(WfStretchMode mode) {
            switch (mode) {
                case WfStretchMode.UniformToFill:
                case WfStretchMode.Uniform:
                case WfStretchMode.Fill:
                default:
                    return WfAspect.AUTO;
                case WfStretchMode.CUSTOM125:
                    return WfAspect.CUSTOM125;
                case WfStretchMode.CUSTOM133:
                    return WfAspect.CUSTOM133;
                case WfStretchMode.CUSTOM150:
                    return WfAspect.CUSTOM150;
                case WfStretchMode.CUSTOM177:
                    return WfAspect.CUSTOM177;
            }
        }

        public WfStretchMode Aspect2StretchMode(WfAspect aspect) {
            switch (aspect) {
                case WfAspect.AUTO:
                default:
                    return StandardStretchMode;
                case WfAspect.CUSTOM125:
                    return WfStretchMode.CUSTOM125;
                case WfAspect.CUSTOM133:
                    return WfStretchMode.CUSTOM133;
                case WfAspect.CUSTOM150:
                    return WfStretchMode.CUSTOM150;
                case WfAspect.CUSTOM177:
                    return WfStretchMode.CUSTOM177;
            }
        }
    }

    /// <summary>
    /// WfPlayerWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class WfPlayerWindow : Window {
        
        #region 初期化/解放
        
        private bool mLoaded = false;
        //private bool mPreviewMode = false;  // true にすると、再生時にCounterをインクリメントしない
        private WfServerEx mServer;


        public WfPlayerViewModel ViewModel {
            get => DataContext as WfPlayerViewModel;
            set => DataContext = value;
        }

        // 既読カウンタの管理クラス
        private class SeenFlagMediator {
            public bool PreviewMode { get; }

            private IWfSource CurrentItem;
            private long Tick = 0;
            private long Threshold = 0;
            private bool Playing = false;

            public SeenFlagMediator(bool preview) {
                PreviewMode = preview;
            }

            public void ItemChanged(IWfSource newItem, long duration) {
                if (PreviewMode) return;

                if (Playing && CurrentItem != null && newItem != CurrentItem) {
                    PlayingStateChanged(false);
                }

                CurrentItem = newItem;
                if (newItem != null) {
                    Threshold = (duration - newItem.TrimStart - newItem.TrimEnd) / 10;
                    if (Playing) {
                        Tick = System.Environment.TickCount;
                    }
                }
            }

            public void PlayingStateChanged(bool playing) {
                if (PreviewMode) return;
                if (Playing) {
                    Check();
                } else if(playing) {
                    Tick = System.Environment.TickCount;
                }
                Playing = playing;
            }

            private void Check() {
                if (CurrentItem == null) return;
                var cur = System.Environment.TickCount;
                Threshold -= (cur - Tick);
                if (Threshold < 0) {
                    CurrentItem.Touch();
                    CurrentItem.SaveModified();
                    CurrentItem = null;
                } else {
                    Tick = cur;
                }
            }

            public void Dispose() {
                PlayingStateChanged(false);
                CurrentItem = null;
            }
        }

        private SeenFlagMediator SeenFlagAgent;

        /**
         * コンストラクタ
         */
        public WfPlayerWindow(bool preview) {
            SeenFlagAgent = new SeenFlagMediator(preview);
            ViewModel = new WfPlayerViewModel();

            mCursorManager = new HidingCursor(this);
            InitKeyMap();

            ViewModel.StretchMode.Value = (WfStretchMode)(Convert.ToInt32(WfPlayListDB.Instance.GetValueAt("StretchMode") ?? "0"));
            ViewModel.StretchMaximum.Value = (Convert.ToInt32(WfPlayListDB.Instance.GetValueAt("StretchMaximum") ?? "0")) != 0;
            ViewModel.SliderPinned.Value = (Convert.ToInt32(WfPlayListDB.Instance.GetValueAt("SliderPinned") ?? "0")) != 0;

            ViewModel.CommandPlay.Subscribe(() => Play(0.5));
            ViewModel.CommandPlayFast.Subscribe(() => Play(1.0));
            ViewModel.CommandPause.Subscribe(Pause);
            ViewModel.CommandStop.Subscribe(Stop);
            ViewModel.CommandPrev.Subscribe(()=> _=Prev());
            ViewModel.CommandNext.Subscribe(()=> _=Next());

            InitializeComponent();
        }

        /**
         * ビュー生成時の初期化処理
         */
        private async void OnLoaded(object sender, RoutedEventArgs e) {
            mLoaded = true;
            if (ViewModel.Sources.Value != null) {
                await InitSource(mAutoStart);
            }
            ViewModel.AllowRemoteControl.Value = true;
            ViewModel.WinMaximized.Value = Window.GetWindow(this).WindowStyle == WindowStyle.None;
            ViewModel.WinMaximized.Subscribe((max) => {
                var win = Window.GetWindow(this);
                if (max) {
                    win.WindowStyle = WindowStyle.None;         // タイトルバーと境界線を表示しない
                    win.WindowState = WindowState.Maximized;    // 最大化表示
                } else {
                    win.WindowStyle = WindowStyle.SingleBorderWindow;
                    win.WindowState = WindowState.Normal;
                }
            });

            ViewModel.AllowRemoteControl.Subscribe((svr) => {
                if (svr) {
                    if (null == mServer) {
                        mServer = WfServerEx.CreateInstance(InvokeFromRemote);
                    }
                    mServer.Start();
                } else {
                    if (null != mServer) {
                        mServer.Stop();
                        //mServer = null;
                    }
                }
            });
            ViewModel.CusorManagerEnabled.Subscribe((v) => mCursorManager.Enabled = v);
            ViewModel.Playing.Subscribe(OnPlayingStateChanged);
            ViewModel.StretchMode.Subscribe((c) => OnStretchModeChanged());
            ViewModel.StretchMaximum.Subscribe((c) => OnStretchModeChanged());
            ViewModel.Speed.Subscribe(OnSpeedChanged);
        }

        /**
         * ビューが破棄されるときの終了処理
         */
        private void OnUnloaded(object sender, RoutedEventArgs e) {
            SeenFlagAgent.Dispose();
            ResetSuperFastPlay();
            ViewModel.Current.Value?.SaveModified();

            mServer?.Dispose();
            mServer = null;

            WfPlayListDB.Instance.SetValueAt("StretchMode", $"{(long)ViewModel.StandardStretchMode}");
            WfPlayListDB.Instance.SetValueAt("StretchMaximum", ViewModel.StretchMaximum.Value ? "1" : "0");
            WfPlayListDB.Instance.SetValueAt("SliderPinned", ViewModel.SliderPinned.Value ? "1" : "0");
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            WfGlobalParams.Instance.PlayerPlacement.ApplyPlacementTo(this);
        }

        private void OnClosing(object sender, CancelEventArgs e) {
            WfGlobalParams.Instance.Placement.GetPlacementFrom(this);
        }

        #endregion


        #region Binding Properties

        private double calcSpeedRatio(double speed) {
            if (speed >= 0.5) {
                return 1 + (speed - 0.5) * 2;       // 1 ～ 2
            } else {
                return 0.2 + 0.8 * (speed * 2);     // 0.2 ～ 1
            }
        }
        private void OnSpeedChanged(double newValue) {
            if (mMediaElement != null) {
                mMediaElement.SpeedRatio = calcSpeedRatio(newValue);
            }
        }

        #endregion

        #region ビデオソース

        private TaskCompletionSource<bool> mVideoLoadingTaskSource = null;
        private async Task<bool> SetVideoSource(IWfSource rec, bool startNow) {
            if (null != mVideoLoadingTaskSource) {
                await mVideoLoadingTaskSource.Task;
            }
            ViewModel.Current.Value = rec;
            UpdateTitle(rec);
            if(rec==null) {
                return false;
            }

            mVideoLoadingTaskSource = new TaskCompletionSource<bool>();
            ViewModel.Ready.Value = false;
            mMediaElement.Stop();
            mMediaElement.Close();
            mMediaElement.Source = rec.Uri;
            mMediaElement.Position = TimeSpan.FromMilliseconds(rec.TrimStart);
            mMediaElement.Play();
            var r = await mVideoLoadingTaskSource.Task;
            if (!startNow) {
                mMediaElement.Pause();
            }
            mVideoLoadingTaskSource = null;
            return r;
        }

        private void UpdateTitle(IWfSource rec) {
            string name = rec!=null ? System.IO.Path.GetFileName(rec.FullPath) : "";
            this.Title = $"WfPlayer - {name}";
        }

        private bool mAutoStart = false;

        public async void SetSources(IWfSourceList sources, bool startNow) {
            Stop();
            ViewModel.Sources.Value = sources;
            mAutoStart = startNow;
            if (mLoaded) {
                await InitSource(startNow);
            }
        }

        private async Task InitSource(bool startNow) {
            await SourceChange(async () => {
                var source = ViewModel.Sources.Value;
                if (null != source) {
                    var rec = source.Current ?? source.Head;
                    if(await SetVideoSource(rec, startNow)) {
                        ViewModel.Started.Value = startNow;
                        ViewModel.Pausing.Value = false;
                    }
                    return true;
                }
                return false;
            });
        }

        private delegate Task<bool> SourceChangeTask();
        private bool mSourceChanging = false;

        private async Task<bool> SourceChange(SourceChangeTask task) {
            if (mSourceChanging) {
                return true;
            }
            mSourceChanging = true;
            try {
                return await task();
            }
            finally {
                mSourceChanging = false;
            }
        }

        private async Task<bool> Next() {
            return await SourceChange(async () => {
                var rec = ViewModel.Sources.Value?.Next;
                return await SetVideoSource(rec, ViewModel.Playing.Value);
            });
        }

        private async Task<bool> Prev() {
            return await SourceChange(async () => {
                var rec = ViewModel.Sources.Value?.Prev;
                return await SetVideoSource(rec, ViewModel.Playing.Value);
            });
        }

        #endregion

        #region 再生操作

        void Play(double speed = 0.5) {
            ViewModel.Speed.Value = speed;
            if (ViewModel.Pausing.Value) {
                Pause();
            } else if (!ViewModel.Started.Value) {
                ViewModel.Started.Value = true;
                mMediaElement.Play();
            }
        }

        void Pause() {
            if (!ViewModel.Started.Value) {
                return;
            }

            if (!ViewModel.Pausing.Value) {
                mMediaElement.Pause();
                ViewModel.Pausing.Value = true;
            } else {
                mMediaElement.Play();
                ViewModel.Pausing.Value = false;
            }
        }

        void Stop() {
            if (!ViewModel.Started.Value) {
                return;
            }
            mMediaElement.Stop();
            ViewModel.Started.Value = false;
            mPositionSlider.Value = ViewModel.Current.Value?.TrimStart ?? 0;
        }

        private DispatcherTimer mSuperFastPlayTimer;
        void SuperFastPlay() {
            if (null != mSuperFastPlayTimer) {
                mSuperFastPlayTimer.Stop();
                mSuperFastPlayTimer = null;
            } else {
                mSuperFastPlayTimer = new DispatcherTimer();
                mSuperFastPlayTimer.Interval = TimeSpan.FromSeconds(1.5);
                mSuperFastPlayTimer.Tick += (s, e) => {
                    if (ViewModel.Playing.Value) {
                        RelativeSeek(10 * 1000);
                    }
                };
                mSuperFastPlayTimer.Start();
            }
        }

        void ResetSuperFastPlay() {
            if (mSuperFastPlayTimer != null) {
                mSuperFastPlayTimer.Stop();
                mSuperFastPlayTimer = null;
            }
        }

        #endregion


        #region MediaElement Event Handlers

        private void OnMediaOpened(object sender, RoutedEventArgs e) {
            Debug.WriteLine("MediaOpened");
            ViewModel.Ready.Value = true;
            ViewModel.Duration.Value = mMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
            mMediaElement.SpeedRatio = calcSpeedRatio(ViewModel.Speed.Value);
            updateTimelinePosition(ViewModel.Current.Value.TrimStart, true, true);
            SeenFlagAgent.ItemChanged(ViewModel.Current.Value, (long)ViewModel.Duration.Value);
            mVideoLoadingTaskSource?.TrySetResult(true);
        }

        private async void OnMediaEnded(object sender, RoutedEventArgs e) {
            Debug.WriteLine("MediaEnded");
            ViewModel.Ready.Value = false;
            if (!await Next()) {
                Stop();
                Close();
            }
        }

        private async void OnMediaFailed(object sender, ExceptionRoutedEventArgs e) {
            Debug.WriteLine("MediaFailed");
            mVideoLoadingTaskSource?.TrySetResult(false);
            if (!ViewModel.Playing.Value) {
                return;
            }
            if (!await Next()) {
                Close();
            }
        }

        #endregion

        #region Event Handlers

        private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e) {
            if (!HandleMouseOnPanel(sender as Panel, true)) {
                mCursorManager.Update(e.GetPosition(this));
            }

        }

        private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e) {
            if (!HandleMouseOnPanel(sender as Panel, false)) {
                mCursorManager.Reset();
            }
        }

        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e) {
            if (sender is Window) {
                mCursorManager.Update(e.GetPosition(this));
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            if (ViewModel.CustomStretchMode.Value) {
                OnStretchModeChanged();
            }
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            Debug.WriteLine($"KEY:{e.Key} - {e.SystemKey} - {e.KeyStates} - Rep={e.IsRepeat} - D/U/T={e.IsDown}/{e.IsUp}/{e.IsToggled}");
            if (InvokeKeyCommand(e.Key)) {
                e.Handled = true;
            }
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
        private void OnPositionChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            updateTimelinePosition(e.NewValue, slider: false, player: !mUpdatingPositionFromTimer);
        }

        private void updateTimelinePosition(double position, bool slider, bool player) {
            if (player) {
                mMediaElement.Position = TimeSpan.FromMilliseconds(position);
            }
            if (slider) {
                mPositionSlider.Value = position;
            }
            ViewModel.SeekPosition.Value = position;
        }


        private void OnSliderDragStateChanged(TimelineSliderOld.DragState state) {
            switch (state) {
                case TimelineSliderOld.DragState.START:
                    mDragging = true;
                    mMediaElement.Pause();
                    break;
                case TimelineSliderOld.DragState.DRAGGING:
                    updateTimelinePosition(mPositionSlider.Value, slider: false, player: true);
                    break;
                case TimelineSliderOld.DragState.END:
                    updateTimelinePosition(mPositionSlider.Value, slider: false, player: true);
                    mDragging = false;
                    if (ViewModel.Playing.Value) {
                        mMediaElement.Play();
                    }
                    break;
            }
        }

        private void OnPlayingStateChanged(bool playing) {
            if (playing) {
                if (null == mPositionTimer) {
                    mPositionTimer = new DispatcherTimer();
                    mPositionTimer.Tick += (s, e) => {
                        if (!mDragging) {
                            var current = mMediaElement.Position.TotalMilliseconds;
                            var remains = ViewModel.Duration.Value - current;
                            if (remains > 0 && remains < ViewModel.Current.Value.TrimEnd) {
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
            } else {
                mPositionTimer?.Stop();
                mPositionTimer = null;
            }
            SeenFlagAgent.PlayingStateChanged(playing);
        }

        enum SeekUnit {
            LARGE,
            SMALL,
            SEEK10,
            SEEK5,
        }

        double SeekSpan(SeekUnit unit) {
            switch (unit) {
                default:
                case SeekUnit.SMALL:
                    return ViewModel.SmallPositionChange.Value;
                case SeekUnit.LARGE:
                    return ViewModel.LargePositionChange.Value;
                case SeekUnit.SEEK10:
                    return 10 * 1000;
                case SeekUnit.SEEK5:
                    return 5 * 1000;
            }
        }

        void RelativeSeek(double seek) {
            if (!ViewModel.Ready.Value) return;

            var v = mPositionSlider.Value + seek;
            updateTimelinePosition(Math.Min(Math.Max(v, ViewModel.Current.Value.TrimStart), mPositionSlider.Maximum - ViewModel.Current.Value.TrimEnd), true, true);
        }

        void SeekForward(SeekUnit unit) {
            if (!ViewModel.Ready.Value) {
                return;
            }
            RelativeSeek(SeekSpan(unit));
        }

        void SeekBackward(SeekUnit unit) {
            if (!ViewModel.Ready.Value) {
                return;
            }
            RelativeSeek(-SeekSpan(unit));
        }

        #endregion

        #region Stretch Mode

        public void OnStretchModeChanged() {
            switch (ViewModel.StretchMode.Value) {
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

        private void CustomStretch(double ratio) {
            double w = this.ActualWidth, h = this.ActualHeight;
            double cw = h * ratio, ch = w / ratio;
            double vw, vh;

            if (ViewModel.StretchMaximum.Value) {
                (vw, vh) = (w < cw) ? (cw, h) : (w, ch);
            } else {
                (vw, vh) = (w > cw) ? (cw, h) : (w, ch);
            }
            mMediaElement.Stretch = Stretch.Fill;
            mMediaElement.Width = vw;
            mMediaElement.Height = vh;
        }

        private void ToggleStandardStretchMode() {
            if (ViewModel.CustomStretchMode.Value) {
                ViewModel.StretchMode.Value = ViewModel.StandardStretchMode;
            } else {
                int v = 1 + (int)ViewModel.StretchMode.Value;
                if (v >= (int)WfStretchMode.Fill) {
                    v = (int)WfStretchMode.UniformToFill;
                }
                ViewModel.StretchMode.Value = (WfStretchMode)v;
            }
        }

        private void ToggleCustomStretchMode(bool plus) {
            if (ViewModel.CustomStretchMode.Value) {
                int v = (int)ViewModel.StretchMode.Value;
                if (plus) {
                    v++;
                    if (v > (int)WfStretchMode.CUSTOM177) {
                        v = (int)WfStretchMode.CUSTOM125;
                    }
                } else {
                    v--;
                    if (v < (int)WfStretchMode.CUSTOM125) {
                        v = (int)WfStretchMode.CUSTOM177;
                    }
                }
                ViewModel.StretchMode.Value = (WfStretchMode)v;
            } else {
                if (plus) {
                    ViewModel.StretchMode.Value = WfStretchMode.CUSTOM125;
                } else {
                    ViewModel.StretchMode.Value = WfStretchMode.CUSTOM177;
                }
            }
        }

        #endregion

        #region Control Panels

        private bool HandleMouseOnPanel(Panel panel, bool enter) {
            if (null == panel) {
                return false;
            }
            switch (panel.Name) {
                case "mPanelBase":
                    ViewModel.MouseInPanel.Value = enter;
                    break;
                case "mStretchModePanel":
                    ViewModel.MouseInStretchModePanel.Value = enter;
                    break;
                case "mSliderPanel":
                    ViewModel.MouseInSliderPanel.Value = enter;
                    break;
                case "mSizingPanel":
                    ViewModel.MouseInSizingPanel.Value = enter;
                    break;
                default:
                    return false;
            }
            return true;
        }
        #endregion

        #region Hide/Show Cursor

        class HidingCursor {
            private static long WAIT_TIME = 2000;   //3ms
            private Point mPosition;
            private long mCheck = 0;
            private DispatcherTimer mTimer = null;
            private WeakReference<Window> mWin;
            private bool mEnabled = false;

            public HidingCursor(Window owner) {
                mWin = new WeakReference<Window>(owner);
                mPosition = new Point();
            }

            private Cursor CursorOnWin {
                get => mWin?.GetValue().Cursor;
                set {
                    var win = mWin?.GetValue();
                    if (null != win) {
                        win.Cursor = value;
                    }
                }
            }

            public bool Enabled {
                get => mEnabled;
                set {
                    if (value != mEnabled) {
                        mEnabled = value;
                        if (value) {
                            //Update();
                        } else {
                            Reset();
                        }
                    }
                }
            }

            public void Reset() {
                if (mTimer != null) {
                    mTimer.Stop();
                    mTimer = null;
                }
                CursorOnWin = Cursors.Arrow;
            }

            public void Update(Point pos) {
                if (!Enabled) {
                    return;
                }

                if (mPosition != pos) {
                    mPosition = pos;
                    mCheck = System.Environment.TickCount;
                    CursorOnWin = Cursors.Arrow;
                    if (null == mTimer) {
                        mTimer = new DispatcherTimer();
                        mTimer.Tick += OnTimer;
                        mTimer.Interval = TimeSpan.FromMilliseconds(WAIT_TIME / 3);
                        mTimer.Start();
                    }
                }
            }

            private void OnTimer(object sender, EventArgs e) {
                if (null == mTimer) {
                    return;
                }
                if (System.Environment.TickCount - mCheck > WAIT_TIME) {
                    mTimer.Stop();
                    mTimer = null;
                    CursorOnWin = Cursors.None;
                }
            }
        }
        private HidingCursor mCursorManager = null;

        private void KickOutMouse() {
            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(0, 0);
        }

        public bool RequestShutdown { get; private set; } = false;
        private void ShutdownPC() {
            RequestShutdown = true;
            Close();
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

        private void ResetTrimming(WfFileItem item) {
            if (null == item || !item.HasTrimming) {
                return;
            }
            item.TrimStart = 0;
            item.TrimEnd = 0;
            item.SaveModified();
        }

        #endregion

        #region Key Mapping

        static class Commands {
            public const string PLAY = "play";
            public const string PAUSE = "pause";
            public const string STOP = "stop";
            public const string FAST_PLAY = "fast";
            public const string MUTE = "mute";
            public const string CLOSE = "close";
            public const string NEXT = "next";
            public const string PREV = "prev";
            public const string SEEK_FWD = "fwd";
            public const string SEEK_BACK = "back";
            public const string SEEK_FWD_L = "fwdL";
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
            ViewModel.SliderPinned.Value = !ViewModel.SliderPinned.Value;
        }

        private Dictionary<System.Windows.Input.Key, string> mKeyCommandMap = null;
        private Dictionary<string, Action> mCommandMap = null;
        private void InitKeyMap() {
            mCommandMap = new Dictionary<string, Action>()
            {
                { Commands.PLAY, ()=>Play(0.5) },
                { Commands.PAUSE, Pause },
                { Commands.STOP, Stop },
                { Commands.FAST_PLAY, ()=>Play(1.0) },
                { Commands.MUTE, ()=>{ ViewModel.Mute.Value =!ViewModel.Mute.Value; } },
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
                { Commands.RATING_GOOD, ()=> {ViewModel.Current.Value.Rating=Ratings.GOOD; } },
                { Commands.RATING_NORMAL, ()=> {ViewModel.Current.Value.Rating=Ratings.NORMAL; } },
                { Commands.RATING_BAD, ()=> {ViewModel.Current.Value.Rating=Ratings.BAD; } },
                { Commands.RATING_DREADFUL, ()=> {ViewModel.Current.Value.Rating=Ratings.DREADFUL; var _=Next(); } },

                { Commands.NEXT_STD_STRETCH, ToggleStandardStretchMode },
                { Commands.NEXT_CST_STRETCH, ()=> ToggleCustomStretchMode(true) },
                { Commands.PREV_CST_STRETCH, ()=> ToggleCustomStretchMode(false) },

                //{ Commands.TRIM_EDIT, ()=>EditTrimming(ViewModel.Current.Value as WfFileItem) },
                //{ Commands.TRIM_SELECT, ()=>SelectTrimming(ViewModel.Current.Value as WfFileItem) },
                { Commands.TRIM_RESET, ()=>ResetTrimming(ViewModel.Current.Value as WfFileItem) },

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

        private bool InvokeKeyCommand(Key key) {
            if (mKeyCommandMap.TryGetValue(key, out var cmd)) {
                return InvokeCommand(cmd);
            }
            return false;
        }

        private bool InvokeCommand(string cmd) {
            if (cmd != null) {
                if (mCommandMap.TryGetValue(cmd, out var action)) {
                    action?.Invoke();
                    return true;
                }
            }
            return false;
        }

        private bool InvokeFromRemote(string cmd) {
            return Dispatcher.Invoke<bool>(() => { return InvokeCommand(cmd); });
        }

        #endregion
    }
}
