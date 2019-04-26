using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace wfPlayer
{
    public class TimelineSlider : Slider
    {
        public enum DragState {
            START,
            DRAGGING,
            END,
        }
        public delegate void DragStateEventProc(DragState start);
        public event DragStateEventProc DragStateChanged;

        protected override void OnThumbDragStarted(DragStartedEventArgs e)
        {
            base.OnThumbDragStarted(e);
            DragStateChanged?.Invoke(DragState.START);
        }

        protected override void OnThumbDragDelta(DragDeltaEventArgs e)
        {
            base.OnThumbDragDelta(e);
            DragStateChanged?.Invoke(DragState.DRAGGING);
        }

        protected override void OnThumbDragCompleted(DragCompletedEventArgs e)
        {
            base.OnThumbDragCompleted(e);
            DragStateChanged?.Invoke(DragState.END);
        }
    }
}
