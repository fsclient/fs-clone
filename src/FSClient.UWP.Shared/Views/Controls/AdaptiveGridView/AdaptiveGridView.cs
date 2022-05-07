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

namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

    using Windows.Foundation.Collections;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Data;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Data;
    using Windows.UI.Xaml.Controls;
#endif

    /// <summary>
    /// The AdaptiveGridView control allows to present information within a Grid View perfectly adjusting the
    /// total display available space. It reacts to changes in the layout as well as the content so it can adapt
    /// to different form factors automatically.
    /// </summary>
    /// <remarks>
    /// The number and the width of items are calculated based on the
    /// screen resolution in order to fully leverage the available screen space. The property ItemsHeight define
    /// the items fixed height and the property DesiredWidth sets the minimum width for the elements to add a
    /// new column.</remarks>
    public partial class AdaptiveGridView : GridView
    {
        private bool _needContainerMarginForLayout;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveGridView"/> class.
        /// </summary>
        public AdaptiveGridView()
        {
            IsTabStop = false;
            UseLayoutRounding = false;
            SizeChanged += OnSizeChanged;
            Items.VectorChanged += ItemsOnVectorChanged;
        }

        /// <summary>
        /// Prepares the specified element to display the specified item.
        /// </summary>
        /// <param name="obj">The element that's used to display the specified item.</param>
        /// <param name="item">The item to display.</param>
        protected override void PrepareContainerForItemOverride(DependencyObject obj, object item)
        {
            base.PrepareContainerForItemOverride(obj, item);
            if (obj is FrameworkElement element)
            {
                var heightBinding = new Binding
                {
                    Source = this, Path = new PropertyPath("ItemHeight"), Mode = BindingMode.TwoWay
                };

                var widthBinding = new Binding
                {
                    Source = this, Path = new PropertyPath("ItemWidth"), Mode = BindingMode.TwoWay
                };

                element.SetBinding(HeightProperty, heightBinding);
                element.SetBinding(WidthProperty, widthBinding);
            }

            if (obj is ContentControl contentControl)
            {
                contentControl.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                contentControl.VerticalContentAlignment = VerticalAlignment.Stretch;
            }

            if (_needContainerMarginForLayout)
            {
                _needContainerMarginForLayout = false;
                RecalculateLayout(ActualWidth);
            }
        }

        /// <summary>
        /// Calculates the width of the grid items.
        /// </summary>
        /// <param name="containerWidth">The width of the container control.</param>
        /// <returns>The calculated item width.</returns>
        protected virtual double CalculateItemWidth(double containerWidth)
        {
            var desiredWidth = double.IsNaN(DesiredWidth)
                ? containerWidth
                : DesiredWidth;
            var columns = CalculateColumns(containerWidth, desiredWidth);

            // If there's less items than there's columns, reduce the column count (if requested);
            if (Items?.Count > 0 && Items.Count < columns && StretchContentForSingleRow)
            {
                columns = Items.Count;
            }

            var fallbackThickness = default(Thickness);
            var itemMargin = GetItemMargin(this, fallbackThickness);
            if (itemMargin == fallbackThickness)
                // No style explicitly defined, or no items or no container for the items
                // We need to get an actual margin for proper layout
            {
                _needContainerMarginForLayout = true;
            }

            return (containerWidth / columns) - itemMargin.Left - itemMargin.Right;
        }

        private void ItemsOnVectorChanged(IObservableVector<object> sender, IVectorChangedEventArgs @event)
        {
            if (!double.IsNaN(ActualWidth))
            {
                RecalculateLayout(ActualWidth);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (HorizontalAlignment != HorizontalAlignment.Stretch)
            {
                var prevColumns = CalculateColumns(e.PreviousSize.Width, DesiredWidth);
                var newColumns = CalculateColumns(e.NewSize.Width, DesiredWidth);

                // If the width of the internal list view changes, check if more or less columns needs to be rendered.
                if (prevColumns != newColumns)
                {
                    RecalculateLayout(e.NewSize.Width);
                }
            }
            else if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > double.Epsilon)
            {
                // We need to recalculate width as our size changes to adjust internal items.
                RecalculateLayout(e.NewSize.Width);
            }
        }

        private void RecalculateLayout(double containerWidth)
        {
            var panelMargin = ItemsPanelRoot is Panel itemsPanel ? itemsPanel.Margin.Left + itemsPanel.Margin.Right : 0;

            containerWidth = containerWidth - Padding.Left - Padding.Right - panelMargin;

            if (containerWidth > 0)
            {
                var newWidth = CalculateItemWidth(containerWidth);
                ItemWidth = Math.Floor(newWidth);
            }
        }
    }
}
