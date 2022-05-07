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

#if WINUI3
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml.Controls;
#endif

    /// <summary>
    /// Panel that allows for a Master/Details pattern.
    /// </summary>
    /// <seealso cref="ItemsControl" />
    public partial class MasterDetailsView
    {
        /// <summary>
        /// Occurs when the currently selected item changes.
        /// </summary>
        public event SelectionChangedEventHandler? SelectionChanged;

        /// <summary>
        /// Occurs when the view state changes
        /// </summary>
        public event EventHandler<MasterDetailsViewState>? ViewStateChanged;

        private void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, e);
        }
    }
}
