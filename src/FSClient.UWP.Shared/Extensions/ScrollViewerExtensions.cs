// ******************************************************************
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THE CODE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
// THE CODE OR THE USE OR OTHER DEALINGS IN THE CODE.
// ******************************************************************

namespace FSClient.UWP.Shared.Extensions
{
    using System;
    using System.Linq;
    using System.Threading;

    using Windows.Devices.Input;
    using Windows.Foundation;
    using Windows.UI.Core;
    using Windows.UI.Input;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
#endif

    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;
    /// <summary>
    /// Provides attached dependency properties for the <see cref="ScrollViewer"/> to scroll with middle click
    /// </summary>
    public static class ScrollViewerExtensions
    {
        private static bool isMoved;
        private static bool isDeferredMovingStarted;
        private static Timer? scrollTimer;
        private static Point startPosition;

        private static bool isCursorAvailable;

        private const double scrollFactor = 50;
        private const double scrollThreshold = 40;
        private const uint scrollMaxSpeed = 150;

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Attached <see cref="DependencyProperty"/> for enabling middle click scrolling
        /// </summary>
        public static readonly DependencyProperty EnableMiddleClickScrollingProperty =
            DependencyProperty.RegisterAttached("EnableMiddleClickScrolling", typeof(bool),
                typeof(ScrollViewerExtensions), new PropertyMetadata(false, OnEnableMiddleClickScrollingChanged));

        /// <summary>
        /// Get <see cref="EnableMiddleClickScrollingProperty"/>. Returns `true` if middle click scrolling is enabled else retuen `false`
        /// </summary>
        /// <param name="obj">The <see cref="DependencyObject"/> to get the associated `bool`</param>
        /// <returns>The `bool` associated with the <see cref="DependencyObject"/></returns>
        public static bool GetEnableMiddleClickScrolling(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableMiddleClickScrollingProperty);
        }

        /// <summary>
        /// Set <see cref="EnableMiddleClickScrollingProperty"/>. `true` to enable middle click scrolling
        /// </summary>
        /// <param name="obj">The <see cref="DependencyObject"/> to associate the `bool` with</param>
        /// <param name="value">The `bool` for binding to the <see cref="DependencyObject"/></param>
        public static void SetEnableMiddleClickScrolling(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableMiddleClickScrollingProperty, value);
        }
#pragma warning restore CS0419 // Ambiguous reference in cref attribute

        /// <summary>
        /// Function will be called when <see cref="EnableMiddleClickScrollingProperty"/> is updated
        /// </summary>
        /// <param name="d">Holds the dependency object</param>
        /// <param name="e">Holds the dependency object args</param>
        private static void OnEnableMiddleClickScrollingChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var mouseCapabilities = new MouseCapabilities();
            if (mouseCapabilities.MousePresent == 0
                || mouseCapabilities.NumberOfButtons < 3
                || UWPAppInformation.Instance.DeviceFamily != DeviceFamily.Desktop)
            {
                return;
            }

            if (d is ScrollViewer scrollViewer)
            {
                UpdateChange(scrollViewer, (bool)e.NewValue);
            }
            else if (d is FrameworkElement element)
            {
                if (element.FindVisualChildren<ScrollViewer>().FirstOrDefault() is ScrollViewer childScrollViewer)
                {
                    UpdateChange(childScrollViewer, (bool)e.NewValue);
                }
                else
                {
                    element.Loaded += ElementLoaded;
                }
            }

            void ElementLoaded(object sender, object args)
            {
                if (sender is FrameworkElement loadedElement)
                {
                    loadedElement.Loaded -= ElementLoaded;

                    if (loadedElement.FindVisualChildren<ScrollViewer>().FirstOrDefault() is ScrollViewer
                        childScrollViewer)
                    {
                        UpdateChange(childScrollViewer, (bool)e.NewValue);
                    }
                }
            }
        }

        /// <summary>
        /// Function to update changes in <see cref="EnableMiddleClickScrollingProperty"/>
        /// </summary>
        /// <param name="newValue">New value from the <see cref="EnableMiddleClickScrollingProperty"/></param>
        private static void UpdateChange(ScrollViewer scrollViewer, bool newValue)
        {
            if (newValue)
            {
                scrollViewer.PointerPressed -= ScrollViewer_PointerPressed;
                scrollViewer.PointerPressed += ScrollViewer_PointerPressed;
            }
            else
            {
                scrollViewer.PointerPressed -= ScrollViewer_PointerPressed;
                UnsubscribeMiddleClickScrolling();
            }
        }

        /// <summary>
        /// Function to set default value and subscribe to events
        /// </summary>
        private static void SubscribeMiddleClickScrolling(ScrollViewer scrollViewer)
        {
            isMoved = false;
            isDeferredMovingStarted = false;
            isCursorAvailable = IsCursorResourceAvailable();
            startPosition = Window.Current.CoreWindow.PointerPosition;

            scrollTimer?.Dispose();
            scrollTimer = new Timer(Scroll, scrollViewer, 5, 5);

            Window.Current.CoreWindow.PointerMoved -= CoreWindow_PointerMoved;
            Window.Current.CoreWindow.PointerReleased -= CoreWindow_PointerReleased;

            Window.Current.CoreWindow.PointerMoved += CoreWindow_PointerMoved;
            Window.Current.CoreWindow.PointerReleased += CoreWindow_PointerReleased;
        }

        /// <summary>
        /// Function to set default value and unsubscribe to events
        /// </summary>
        private static void UnsubscribeMiddleClickScrolling()
        {
            startPosition = default;
            isMoved = false;
            isDeferredMovingStarted = false;
            scrollTimer?.Dispose();
            scrollTimer = null;

            Window.Current.CoreWindow.PointerMoved -= CoreWindow_PointerMoved;
            Window.Current.CoreWindow.PointerReleased -= CoreWindow_PointerReleased;

            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
        }

        /// <summary>
        /// This function will be called for every small interval by <see cref="Timer"/>
        /// </summary>
        /// <param name="state">Default param for <see cref="Timer"/>. In this function it will be `null`</param>
        private static async void Scroll(object state)
        {
            if (state is ScrollViewer scrollViewer)
            {
                await scrollViewer.Dispatcher.TryRunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (scrollTimer == null)
                    {
                        return;
                    }

                    var currentPosition = Window.Current.CoreWindow.PointerPosition;

                    var offsetX = currentPosition.X - startPosition.X;
                    var offsetY = currentPosition.Y - startPosition.Y;

                    SetCursorType(offsetX, offsetY);

                    if (Math.Abs(offsetX) > scrollThreshold || Math.Abs(offsetY) > scrollThreshold)
                    {
                        offsetX = Math.Abs(offsetX) < scrollThreshold ? 0 : offsetX;
                        offsetY = Math.Abs(offsetY) < scrollThreshold ? 0 : offsetY;

                        offsetX /= scrollFactor;
                        offsetY /= scrollFactor;

                        offsetX = offsetX > 0 ? Math.Pow(offsetX, 2) : -Math.Pow(offsetX, 2);
                        offsetY = offsetY > 0 ? Math.Pow(offsetY, 2) : -Math.Pow(offsetY, 2);

                        offsetX = offsetX > scrollMaxSpeed ? scrollMaxSpeed : offsetX;
                        offsetY = offsetY > scrollMaxSpeed ? scrollMaxSpeed : offsetY;

                        scrollViewer?.ChangeView(scrollViewer.HorizontalOffset + offsetX,
                            scrollViewer.VerticalOffset + offsetY, null, true);
                    }
                });
            }
        }

        private static void ScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Unsubscribe if deferred moving is started
            if (isDeferredMovingStarted)
            {
                UnsubscribeMiddleClickScrolling();
                return;
            }

            var pointer = e.Pointer;

            if (pointer.PointerDeviceType == PointerDeviceType.Mouse
                && sender is ScrollViewer scrollViewer)
            {
                if (scrollViewer.ScrollableHeight == 0
                    && scrollViewer.ScrollableWidth == 0)
                {
                    return;
                }

                var pointerPoint = e.GetCurrentPoint(scrollViewer);

                // SubscribeMiddle if middle button is pressed
                if (pointerPoint.Properties.IsMiddleButtonPressed)
                {
                    SubscribeMiddleClickScrolling(scrollViewer);
                }
            }
        }

        private static void CoreWindow_PointerMoved(CoreWindow sender, PointerEventArgs args)
        {
            var isPressed = args.CurrentPoint.Properties.IsMiddleButtonPressed;

            // If condution that occures before scrolling begins
            if (isPressed && !isMoved)
            {
                var pointerPoint = args.CurrentPoint;

                if (pointerPoint.Properties.IsMiddleButtonPressed)
                {
                    var currentPosition = Window.Current.CoreWindow.PointerPosition;

                    var offsetX = currentPosition.X - startPosition.X;
                    var offsetY = currentPosition.Y - startPosition.Y;

                    // Settign _isMoved if pointer goes out of threshold value
                    if (Math.Abs(offsetX) > scrollThreshold || Math.Abs(offsetY) > scrollThreshold)
                    {
                        isMoved = true;
                    }
                }
            }

            //// Update current position of the pointer if scrolling started
            //if (_isDeferredMovingStarted || isPressed)
            //{
            //    _currentPosition = Window.Current.CoreWindow.PointerPosition;
            //}
        }

        private static void CoreWindow_PointerReleased(CoreWindow sender, PointerEventArgs args)
        {
            var wasPressed = args.CurrentPoint.Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonReleased;

            // Start deferred moving if the pointer is pressed and not moved
            if (wasPressed && !isMoved)
            {
                isDeferredMovingStarted = true;

                // Event to stop deferred scrolling if pointer exited
                Window.Current.CoreWindow.PointerExited -= CoreWindow_PointerExited;
                Window.Current.CoreWindow.PointerExited += CoreWindow_PointerExited;

                // Event to stop deferred scrolling if pointer pressed
                Window.Current.CoreWindow.PointerPressed -= CoreWindow_PointerPressed;
                Window.Current.CoreWindow.PointerPressed += CoreWindow_PointerPressed;

                SetCursorType(0, 0);
            }
            else
            {
                isDeferredMovingStarted = false;
            }

            // Unsubscribe if the pointer is pressed and not DeferredMoving
            if (wasPressed && !isDeferredMovingStarted)
            {
                UnsubscribeMiddleClickScrolling();
            }
        }

        private static void CoreWindow_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            Window.Current.CoreWindow.PointerPressed -= CoreWindow_PointerPressed;
            Window.Current.CoreWindow.PointerExited -= CoreWindow_PointerExited;
            UnsubscribeMiddleClickScrolling();
        }

        private static void CoreWindow_PointerExited(CoreWindow sender, PointerEventArgs args)
        {
            Window.Current.CoreWindow.PointerPressed -= CoreWindow_PointerPressed;
            Window.Current.CoreWindow.PointerExited -= CoreWindow_PointerExited;
            UnsubscribeMiddleClickScrolling();
        }

        /// <summary>
        /// Change cursor type depend upon offset from starting position
        /// </summary>
        /// <param name="offsetX">Horizontal offset from starting position</param>
        /// <param name="offsetY">Vertical offset from starting position</param>
        private static void SetCursorType(double offsetX, double offsetY)
        {
            if (Window.Current.CoreWindow is not CoreWindow coreWindow)
            {
                return;
            }

            if (!isCursorAvailable)
            {
                coreWindow.PointerCursor = new CoreCursor(CoreCursorType.Cross, 0);
                return;
            }

            uint cursorID = 101;

            if (Math.Abs(offsetX) < scrollThreshold && Math.Abs(offsetY) < scrollThreshold)
            {
                cursorID = 101;
            }
            else if (Math.Abs(offsetX) < scrollThreshold && offsetY < -scrollThreshold)
            {
                cursorID = 102;
            }
            else if (offsetX > scrollThreshold && offsetY < -scrollThreshold)
            {
                cursorID = 103;
            }
            else if (offsetX > scrollThreshold && Math.Abs(offsetY) < scrollThreshold)
            {
                cursorID = 104;
            }
            else if (offsetX > scrollThreshold && offsetY > scrollThreshold)
            {
                cursorID = 105;
            }
            else if (Math.Abs(offsetX) < scrollThreshold && offsetY > scrollThreshold)
            {
                cursorID = 106;
            }
            else if (offsetX < -scrollThreshold && offsetY > scrollThreshold)
            {
                cursorID = 107;
            }
            else if (offsetX < -scrollThreshold && Math.Abs(offsetY) < scrollThreshold)
            {
                cursorID = 108;
            }
            else if (offsetX < -scrollThreshold && offsetY < -scrollThreshold)
            {
                cursorID = 109;
            }

            if (coreWindow.PointerCursor?.Id != cursorID)
            {
                coreWindow.PointerCursor = new CoreCursor(CoreCursorType.Custom, cursorID);
            }
        }

        /// <summary>
        /// Function to check the availability of cursor resource
        /// </summary>
        /// <returns>Returns `true` if the cursor resource is available</returns>
        private static bool IsCursorResourceAvailable()
        {
            var isCursorAvailable = true;

            try
            {
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Custom, 101);
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Custom, 102);
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Custom, 103);
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Custom, 104);
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Custom, 105);
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Custom, 106);
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Custom, 107);
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Custom, 108);
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Custom, 109);
            }
            catch (Exception)
            {
                isCursorAvailable = false;
            }
            finally
            {
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
            }

            return isCursorAvailable;
        }
    }
}
