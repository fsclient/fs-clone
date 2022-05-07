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
    using System.Linq;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
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
    public partial class AdaptiveGridView
    {
        /// <summary>
        /// Identifies the <see cref="ItemHeight"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(AdaptiveGridView),
                new PropertyMetadata(double.NaN));

        /// <summary>
        /// Identifies the <see cref="ItemWidth"/> dependency property.
        /// </summary>
        private static readonly DependencyProperty ItemWidthProperty =
            DependencyProperty.Register(nameof(ItemWidth), typeof(double), typeof(AdaptiveGridView),
                new PropertyMetadata(double.NaN));

        /// <summary>
        /// Identifies the <see cref="DesiredWidth"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty DesiredWidthProperty =
            DependencyProperty.Register(nameof(DesiredWidth), typeof(double), typeof(AdaptiveGridView),
                new PropertyMetadata(double.NaN, RecalculateLayoutOnPropertyChanged));

        /// <summary>
        /// Identifies the <see cref="StretchContentForSingleRow"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty StretchContentForSingleRowProperty =
            DependencyProperty.Register(nameof(StretchContentForSingleRow), typeof(bool), typeof(AdaptiveGridView),
                new PropertyMetadata(false, RecalculateLayoutOnPropertyChanged));

        /// <summary>
        /// Gets or sets the desired width of each item
        /// </summary>
        /// <value>The width of the desired.</value>
        public double DesiredWidth
        {
            get => (double)GetValue(DesiredWidthProperty);
            set => SetValue(DesiredWidthProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the control should stretch the content to fill at least one row.
        /// </summary>
        /// <remarks>
        /// If set to <c>true</c> (default) and there is only one row of items, the items will be stretched to fill the complete row.
        /// If set to <c>false</c>, items will have their normal size, which means a gap can exist at the end of the row.
        /// </remarks>
        /// <value>A value indicating whether the control should stretch the content to fill at least one row.</value>
        public bool StretchContentForSingleRow
        {
            get => (bool)GetValue(StretchContentForSingleRowProperty);
            set => SetValue(StretchContentForSingleRowProperty, value);
        }

        /// <summary>
        /// Gets or sets the height of each item in the grid.
        /// </summary>
        /// <value>The height of the item.</value>
        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        /// <summary>
        /// Gets the template that defines the panel that controls the layout of items.
        /// </summary>
        /// <remarks>
        /// This property overrides the base ItemsPanel to prevent changing it.
        /// </remarks>
        /// <returns>
        /// An ItemsPanelTemplate that defines the panel to use for the layout of the items.
        /// The default value for the ItemsControl is an ItemsPanelTemplate that specifies
        /// a StackPanel.
        /// </returns>
        public new ItemsPanelTemplate ItemsPanel => base.ItemsPanel;

        private static void RecalculateLayoutOnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = d as AdaptiveGridView;
            self?.RecalculateLayout(self.ActualWidth);
        }

        private double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        private static int CalculateColumns(double containerWidth, double itemWidth)
        {
            return Math.Max(1, (int)Math.Round(containerWidth / itemWidth));
        }

        private static Thickness GetItemMargin(GridView view, Thickness fallback = default)
        {
            var setter = view.ItemContainerStyle?.Setters.OfType<Setter>()
                .FirstOrDefault(s => s.Property == MarginProperty);
            if (setter != null)
            {
                return (Thickness)setter.Value;
            }

            if (view.Items.Count > 0)
            {
                var container = (GridViewItem)view.ContainerFromIndex(0);
                if (container != null)
                {
                    return container.Margin;
                }
            }

            // Use the default thickness for a GridViewItem
            return fallback;
        }
    }
}
