namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Windows.Foundation.Metadata;
    using Windows.System;
    using Windows.UI.Core;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Input;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;
#endif

    using FSClient.Shared;
    public partial class PlayerControl
    {
        private static readonly Dictionary<(VirtualKey key, VirtualKeyModifiers modifiers), PlayerControlKeyBindingAction> defaultBindingActions = new Dictionary<(VirtualKey key, VirtualKeyModifiers modifiers), PlayerControlKeyBindingAction>
        {
            [(VirtualKey.Escape, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.OnGoBackRequested,

            [(VirtualKey.F, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.ToggleFullscreen,
            [(VirtualKey.F11, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.ToggleFullscreen,
            [(VirtualKey.Enter, VirtualKeyModifiers.Menu)] = PlayerControlKeyBindingAction.ToggleFullscreen,

            [(VirtualKey.K, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.TogglePlayPause,
            [(VirtualKey.Space, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.TogglePlayPause,

            // , < on keyboard
            [((VirtualKey)188, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.PlaybackRateStepDecrease,
            [((VirtualKey)188, VirtualKeyModifiers.Shift)] = PlayerControlKeyBindingAction.PlaybackRateStepDecrease,
            // . > on keyboard
            [((VirtualKey)190, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.PlaybackRateStepIncrease,
            [((VirtualKey)190, VirtualKeyModifiers.Shift)] = PlayerControlKeyBindingAction.PlaybackRateStepIncrease,

            [(VirtualKey.M, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.ToggleIsMuted,

            [(VirtualKey.GamepadView, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.ToggleTransportControlsView,

            [(VirtualKey.J, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.Rewind,
            [(VirtualKey.GamepadLeftTrigger, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.Rewind,
            [(VirtualKey.GamepadDPadLeft, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.Rewind,
            [(VirtualKey.GamepadLeftThumbstickLeft, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.Rewind,
            [(VirtualKey.Left, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.Rewind,

            [(VirtualKey.L, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.FastForward,
            [(VirtualKey.GamepadRightTrigger, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.FastForward,
            [(VirtualKey.GamepadDPadRight, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.FastForward,
            [(VirtualKey.GamepadLeftThumbstickRight, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.FastForward,
            [(VirtualKey.Right, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.FastForward,

            [(VirtualKey.Subtract, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.VolumeDecrease,
            [(VirtualKey.GamepadDPadDown, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.VolumeDecrease,
            [(VirtualKey.GamepadLeftThumbstickDown, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.VolumeDecrease,
            [(VirtualKey.Down, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.VolumeDecrease,

            [(VirtualKey.Add, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.VolumeIncrease,
            [(VirtualKey.GamepadDPadUp, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.VolumeIncrease,
            [(VirtualKey.GamepadLeftThumbstickUp, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.VolumeIncrease,
            [(VirtualKey.Up, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.VolumeIncrease,

            [(VirtualKey.P, VirtualKeyModifiers.Shift)] = PlayerControlKeyBindingAction.GoPreviousMediaItem,
            [(VirtualKey.P, VirtualKeyModifiers.Menu)] = PlayerControlKeyBindingAction.GoPreviousMediaItem,
            [(VirtualKey.PageDown, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoPreviousMediaItem,

            [(VirtualKey.N, VirtualKeyModifiers.Shift)] = PlayerControlKeyBindingAction.GoNextMediaItem,
            [(VirtualKey.N, VirtualKeyModifiers.Menu)] = PlayerControlKeyBindingAction.GoNextMediaItem,
            [(VirtualKey.PageUp, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoNextMediaItem,

            [(VirtualKey.Z, VirtualKeyModifiers.Control)] = PlayerControlKeyBindingAction.GoToOnePreviousPosition,
            [(VirtualKey.Home, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoToStartPosition,
            [(VirtualKey.End, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoToEndPosition,

            [(VirtualKey.Number0, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoToStartPosition,
            [(VirtualKey.Number1, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo10PercentPosition,
            [(VirtualKey.Number2, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo20PercentPosition,
            [(VirtualKey.Number3, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo30PercentPosition,
            [(VirtualKey.Number4, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo40PercentPosition,
            [(VirtualKey.Number5, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo50PercentPosition,
            [(VirtualKey.Number6, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo60PercentPosition,
            [(VirtualKey.Number7, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo70PercentPosition,
            [(VirtualKey.Number8, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo80PercentPosition,
            [(VirtualKey.Number9, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo90PercentPosition,

            [(VirtualKey.NumberPad0, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoToStartPosition,
            [(VirtualKey.NumberPad1, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo10PercentPosition,
            [(VirtualKey.NumberPad2, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo20PercentPosition,
            [(VirtualKey.NumberPad3, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo30PercentPosition,
            [(VirtualKey.NumberPad4, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo40PercentPosition,
            [(VirtualKey.NumberPad5, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo50PercentPosition,
            [(VirtualKey.NumberPad6, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo60PercentPosition,
            [(VirtualKey.NumberPad7, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo70PercentPosition,
            [(VirtualKey.NumberPad8, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo80PercentPosition,
            [(VirtualKey.NumberPad9, VirtualKeyModifiers.None)] = PlayerControlKeyBindingAction.GoTo90PercentPosition,
        };

        private void SetupAccelerators()
        {
            if (!ApiInformation.IsPropertyPresent(typeof(UIElement).FullName, nameof(KeyboardAccelerators)))
            {
                return;
            }

            if (ApiInformation.IsPropertyPresent(typeof(UIElement).FullName, nameof(KeyboardAcceleratorPlacementMode)))
            {
                KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
            }

            var thisWeak = new WeakReference<PlayerControl>(this);
            foreach (var binding in defaultBindingActions)
            {
                var keyboardAccelerator = new PlayerKeyboardAccelerator(binding.Value, thisWeak)
                {
                    Key = binding.Key.key, Modifiers = binding.Key.modifiers, IsEnabled = true
                };

                keyboardAccelerator.Invoked += KeyboardAccelerator_Invoked;
                KeyboardAccelerators.Add(keyboardAccelerator);
            }
        }

        private static async void KeyboardAccelerator_Invoked(KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!args.Handled
                && sender is PlayerKeyboardAccelerator playerKeyboardAccelerator)
            {
                if (playerKeyboardAccelerator.Player.TryGetTarget(out var playerControl))
                {
                    args.Handled = await playerControl
                        .HandleKeyBindingActionUnsafeAsync(playerKeyboardAccelerator.Action).ConfigureAwait(true);
                }
                else
                {
                    sender.Invoked -= KeyboardAccelerator_Invoked;
                }
            }
        }

        public Task<bool> HandleKeyBindingActionAsync(PlayerControlKeyBindingAction action)
        {
            if (WindowMode != WindowMode.FullScreen && WindowMode != WindowMode.CompactOverlay)
            {
                return Task.FromResult(false);
            }

            return HandleKeyBindingActionUnsafeAsync(action);
        }

        private async Task<bool> HandleKeyBindingActionUnsafeAsync(PlayerControlKeyBindingAction action)
        {
            switch (action)
            {
                case PlayerControlKeyBindingAction.OnGoBackRequested
                    when TransportControls is CustomTransportControls ctc
                         && ctc.HidePlaylist():
                    WindowMode = WindowMode.None;
                    return true;
                case PlayerControlKeyBindingAction.OnGoBackRequested
                    when WindowMode != WindowMode.None:
                    WindowMode = WindowMode.None;
                    return true;
                case PlayerControlKeyBindingAction.OnGoBackRequested
                    when WindowMode == WindowMode.None
                         && TryGetPlaybackSessionOrNull()?.PlaybackState ==
                         Windows.Media.Playback.MediaPlaybackState.Playing:
                    await PauseAsync().ConfigureAwait(true);
                    return true;
                case PlayerControlKeyBindingAction.ToggleFullscreen:
                    ToggleFullscreen();
                    return true;
                case PlayerControlKeyBindingAction.ToggleIsMuted:
                    MediaPlayerSafeInvoke(mediaPlayer => mediaPlayer.IsMuted = !mediaPlayer.IsMuted);
                    return true;
                case PlayerControlKeyBindingAction.TogglePlayPause:
                    await TogglePlayPauseAsync().ConfigureAwait(true);
                    return true;
                case PlayerControlKeyBindingAction.Rewind:
                    return Rewind(SeekModifier.Auto, PositionChangeType.Keyboard);
                case PlayerControlKeyBindingAction.FastForward:
                    return FastForward(SeekModifier.Auto, PositionChangeType.Keyboard);
                case PlayerControlKeyBindingAction.VolumeIncrease:
                    Volume += (GetCurrentSeekModifier()) switch
                    {
                        SeekModifier.Double => volumeUpDelta * 2,
                        SeekModifier.Half => volumeUpDelta * 0.5,
                        _ => volumeUpDelta,
                    };
                    return true;
                case PlayerControlKeyBindingAction.VolumeDecrease:
                    Volume -= (GetCurrentSeekModifier()) switch
                    {
                        SeekModifier.Double => volumeDownDelta * 2,
                        SeekModifier.Half => volumeDownDelta * 0.5,
                        _ => volumeDownDelta,
                    };
                    return true;
                case PlayerControlKeyBindingAction.GoNextMediaItem when GoNextCommand?.CanExecute() == true:
                    await GoNextCommand.ExecuteAsync().ConfigureAwait(true);
                    return true;
                case PlayerControlKeyBindingAction.GoPreviousMediaItem when GoPreviousCommand?.CanExecute() == true:
                    await GoPreviousCommand.ExecuteAsync().ConfigureAwait(true);
                    return true;
                case PlayerControlKeyBindingAction.ToggleTransportControlsView
                    when TransportControls is CustomTransportControls cts:
                    cts.ToggleView();
                    return true;
                case PlayerControlKeyBindingAction.GoToOnePreviousPosition
                    when lastTimerPosition is TimeSpan lastPosition:
                    var oldPosition = Position;
                    Position = lastPosition;
                    OnPositionChanged(new PositionEventArgs(lastPosition, oldPosition, PositionChangeType.Keyboard,
                        Duration));
                    return true;
                case PlayerControlKeyBindingAction.GoToStartPosition:
                    Position = TimeSpan.Zero;
                    return true;
                case PlayerControlKeyBindingAction.GoToEndPosition
                    when Duration is TimeSpan duration:
                    Position = duration;
                    return true;
                case PlayerControlKeyBindingAction.GoTo10PercentPosition
                    when Duration is TimeSpan duration:
                    Position = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.1);
                    return true;
                case PlayerControlKeyBindingAction.GoTo20PercentPosition
                    when Duration is TimeSpan duration:
                    Position = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.2);
                    return true;
                case PlayerControlKeyBindingAction.GoTo30PercentPosition
                    when Duration is TimeSpan duration:
                    Position = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.3);
                    return true;
                case PlayerControlKeyBindingAction.GoTo40PercentPosition
                    when Duration is TimeSpan duration:
                    Position = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.4);
                    return true;
                case PlayerControlKeyBindingAction.GoTo50PercentPosition
                    when Duration is TimeSpan duration:
                    Position = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.5);
                    return true;
                case PlayerControlKeyBindingAction.GoTo60PercentPosition
                    when Duration is TimeSpan duration:
                    Position = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.6);
                    return true;
                case PlayerControlKeyBindingAction.GoTo70PercentPosition
                    when Duration is TimeSpan duration:
                    Position = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.7);
                    return true;
                case PlayerControlKeyBindingAction.GoTo80PercentPosition
                    when Duration is TimeSpan duration:
                    Position = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.8);
                    return true;
                case PlayerControlKeyBindingAction.GoTo90PercentPosition
                    when Duration is TimeSpan duration:
                    Position = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.9);
                    return true;
                case PlayerControlKeyBindingAction.PlaybackRateStepIncrease:
                    PlaybackRate = Math.Max(MinPlaybackRate, Math.Min(MaxPlaybackRate,
                        PlaybackRate + PlaybackRateStep));
                    return true;
                case PlayerControlKeyBindingAction.PlaybackRateStepDecrease:
                    PlaybackRate = Math.Max(MinPlaybackRate, Math.Min(MaxPlaybackRate,
                        PlaybackRate - PlaybackRateStep));
                    return true;
            }

            return false;
        }

        private async void OnGlobalKeyPress(AcceleratorKeyEventArgs a)
        {
            if ((a.EventType == CoreAcceleratorKeyEventType.KeyDown
                 || a.EventType == CoreAcceleratorKeyEventType.SystemKeyDown))
            {
                var gamepadDpad = Settings.Instance.SeekWithGamepadDPad;
                
                switch (a.VirtualKey)
                {
                    // Keys below in some cases is handled by navigation system
                    case VirtualKey.Space:
                    case VirtualKey.Tab:
                    case VirtualKey.Enter:
                    // Keys below is couldn't be handled by KeyboardAccelerators or hasn't first priority with it
                    case VirtualKey.GamepadLeftTrigger:
                    case VirtualKey.GamepadDPadLeft when gamepadDpad:
                    case VirtualKey.GamepadLeftThumbstickLeft:
                    case VirtualKey.GamepadRightThumbstickLeft:
                    case VirtualKey.Left:
                    case VirtualKey.GamepadRightTrigger:
                    case VirtualKey.GamepadDPadRight when gamepadDpad:
                    case VirtualKey.GamepadLeftThumbstickRight:
                    case VirtualKey.GamepadRightThumbstickRight:
                    case VirtualKey.Right:
                    case VirtualKey.GamepadDPadUp when gamepadDpad:
                    case VirtualKey.GamepadLeftThumbstickUp:
                    case VirtualKey.GamepadRightThumbstickUp:
                    case VirtualKey.Up:
                    case VirtualKey.GamepadDPadDown when gamepadDpad:
                    case VirtualKey.GamepadLeftThumbstickDown:
                    case VirtualKey.GamepadRightThumbstickDown:
                    case VirtualKey.Down:
                    case VirtualKey.GamepadView:
                        PlayerControlKeyBindingAction bindingAction;
                        if (defaultBindingActions.TryGetValue((a.VirtualKey, VirtualKeyModifiers.None),
                            out bindingAction))
                        {
                            a.Handled = await HandleKeyBindingActionAsync(bindingAction).ConfigureAwait(true);
                        }

                        if (CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift)
                                .HasFlag(CoreVirtualKeyStates.Down)
                            && defaultBindingActions.TryGetValue((a.VirtualKey, VirtualKeyModifiers.Shift),
                                out bindingAction))
                        {
                            a.Handled = await HandleKeyBindingActionAsync(bindingAction).ConfigureAwait(true);
                        }

                        break;
                }
            }
        }

        private class PlayerKeyboardAccelerator : KeyboardAccelerator
        {
            public PlayerKeyboardAccelerator(
                PlayerControlKeyBindingAction action,
                WeakReference<PlayerControl> player)
            {
                Action = action;
                Player = player;
            }

            public PlayerControlKeyBindingAction Action { get; }

            public WeakReference<PlayerControl> Player { get; }
        }
    }
}
