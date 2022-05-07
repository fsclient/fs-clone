namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

    using Windows.System;
    using Windows.UI.Core;

    using FSClient.Shared;

    public partial class PlayerControl
    {
        public static SeekModifier GetCurrentSeekModifier()
        {
            var coreWindow = CoreWindow.GetForCurrentThread();

            var shiftDown = coreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            var ctrlDown = coreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            if (shiftDown && !ctrlDown)
            {
                return SeekModifier.Double;
            }

            if (ctrlDown && !shiftDown)
            {
                return SeekModifier.Half;
            }

            return SeekModifier.Normal;
        }

        public bool FastForward(SeekModifier modifier, PositionChangeType changeType)
        {
            if (modifier == SeekModifier.Auto)
            {
                modifier = GetCurrentSeekModifier();
            }

            var floatModifier = modifier switch
            {
                SeekModifier.Double => 2,
                SeekModifier.Half => 0.5,
                _ => 1,
            };
            return Seek(
                TimeSpan.FromSeconds(floatModifier * Settings.Instance.SeekForwardStep),
                changeType);
        }

        public bool Rewind(SeekModifier modifier, PositionChangeType changeType)
        {
            if (modifier == SeekModifier.Auto)
            {
                modifier = GetCurrentSeekModifier();
            }

            var floatModifier = modifier switch
            {
                SeekModifier.Double => 2,
                SeekModifier.Half => 0.5,
                _ => 1,
            };
            return Seek(
                TimeSpan.FromSeconds(-floatModifier * Settings.Instance.SeekBackwardStep),
                changeType);
        }

        public bool Seek(TimeSpan seekLength, PositionChangeType changeType)
        {
            try
            {
                var duration = Duration;

                if (duration.HasValue
                    && Math.Abs(seekLength.TotalMilliseconds) >= duration.Value.TotalMilliseconds)
                {
                    // out of range
                    return false;
                }

                var oldPosition = Position;

                TimeSpan newPosition;
                if (seekLength > TimeSpan.Zero
                    && duration.HasValue
                    && duration - oldPosition < seekLength)
                {
                    if (duration.Value == oldPosition)
                    {
                        return false;
                    }

                    newPosition = duration.Value;
                }
                else if (TimeSpan.MaxValue - oldPosition < seekLength)
                {
                    return false;
                }
                else
                {
                    newPosition = oldPosition + seekLength;
                }

                Position = newPosition;
                OnPositionChanged(new PositionEventArgs(newPosition, oldPosition, changeType, duration));
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }
}
