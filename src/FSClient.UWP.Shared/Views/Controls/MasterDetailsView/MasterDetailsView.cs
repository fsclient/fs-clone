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
    using System.Collections.Generic;

    using Windows.ApplicationModel;

    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Navigation;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Navigation;
#endif

    /// <summary>
    /// Panel that allows for a Master/Details pattern.
    /// </summary>
    [TemplatePart(Name = PartDetailsPresenter, Type = typeof(ContentPresenter))]
    [TemplatePart(Name = PartDetailsPanel, Type = typeof(FrameworkElement))]
    [TemplateVisualState(Name = NoSelectionNarrowState, GroupName = SelectionStates)]
    [TemplateVisualState(Name = NoSelectionWideState, GroupName = SelectionStates)]
    [TemplateVisualState(Name = NarrowState, GroupName = WidthStates)]
    [TemplateVisualState(Name = WideState, GroupName = WidthStates)]
    public partial class MasterDetailsView : ItemsControl
    {
        private const string PartDetailsPresenter = "DetailsPresenter";
        private const string PartDetailsPanel = "DetailsPanel";
        private const string PartHeaderContentPresenter = "HeaderContentPresenter";
        private const string NarrowState = "NarrowState";
        private const string WideState = "WideState";
        private const string WidthStates = "WidthStates";
        private const string SelectionStates = "SelectionStates";
        private const string HasSelectionState = "HasSelection";
        private const string NoSelectionNarrowState = "NoSelectionNarrow";
        private const string NoSelectionWideState = "NoSelectionWide";

        private ContentPresenter? _detailsPresenter;
        private VisualStateGroup? _stateGroup;
        private VisualState? _narrowState;
        private Frame? _frame;
        private bool _loaded;

        /// <summary>
        /// Initializes a new instance of the <see cref="MasterDetailsView"/> class.
        /// </summary>
        public MasterDetailsView()
        {
            DefaultStyleKey = typeof(MasterDetailsView);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Invoked whenever application code or internal processes (such as a rebuilding layout pass) call
        /// ApplyTemplate. In simplest terms, this means the method is called just before a UI element displays
        /// in your app. Override this method to influence the default post-template logic of a class.
        /// </summary>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _detailsPresenter = (ContentPresenter)GetTemplateChild(PartDetailsPresenter);
            SetDetailsContent();

            SetMasterHeaderVisibility();
            OnDetailsCommandBarChanged();
            OnMasterCommandBarChanged();

            if (_loaded && _stateGroup == null)
            {
                _stateGroup = (VisualStateGroup)GetTemplateChild(WidthStates);
                if (_stateGroup != null)
                {
                    _stateGroup.CurrentStateChanged += OnVisualStateChanged;
                    _narrowState = GetTemplateChild(NarrowState) as VisualState;
                    UpdateView(true);
                }
            }
        }

        /// <summary>
        /// Fired when the SelectedItem changes.
        /// </summary>
        /// <param name="d">The sender</param>
        /// <param name="e">The event args</param>
        /// <remarks>
        /// Sets up animations for the DetailsPresenter for animating in/out.
        /// </remarks>
        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (MasterDetailsView)d;

            view.OnSelectionChanged(new SelectionChangedEventArgs(new List<object> {e.OldValue},
                new List<object> {e.NewValue}));

            view.UpdateView(true);

            // If there is no selection, do not remove the DetailsPresenter content but let it animate out.
            if (view.SelectedItem != null)
            {
                view.SetDetailsContent();
            }
        }

        /// <summary>
        /// Fired when the <see cref="MasterHeader"/> is changed.
        /// </summary>
        /// <param name="d">The sender</param>
        /// <param name="e">The event args</param>
        private static void OnMasterHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (MasterDetailsView)d;
            view.SetMasterHeaderVisibility();
        }

        /// <summary>
        /// Fired when the DetailsCommandBar changes.
        /// </summary>
        /// <param name="d">The sender</param>
        /// <param name="e">The event args</param>
        private static void OnDetailsCommandBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (MasterDetailsView)d;
            view.OnDetailsCommandBarChanged();
        }

        /// <summary>
        /// Fired when the MasterCommandBar changes.
        /// </summary>
        /// <param name="d">The sender</param>
        /// <param name="e">The event args</param>
        private static void OnMasterCommandBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (MasterDetailsView)d;
            view.OnMasterCommandBarChanged();
        }

        // Have to wait to get the VisualStateGroup until the control has Loaded
        // If we try to get the VisualStateGroup in the OnApplyTemplate the
        // CurrentStateChanged event does not fire properly
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DesignMode.DesignModeEnabled == false)
            {
                ViewModelLocator.Current.NavigationService.GoBackRequested += OnBackRequested;
                var frame = GetFrame();
                if (frame != null)
                {
                    frame.Navigating += OnFrameNavigating;
                }
            }

            if (_stateGroup != null)
            {
                _stateGroup.CurrentStateChanged -= OnVisualStateChanged;
            }

            _stateGroup = (VisualStateGroup)GetTemplateChild(WidthStates);
            if (_stateGroup != null)
            {
                _stateGroup.CurrentStateChanged += OnVisualStateChanged;
                _narrowState = GetTemplateChild(NarrowState) as VisualState;
                UpdateView(true);
            }

            _loaded = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DesignMode.DesignModeEnabled == false)
            {
                ViewModelLocator.Current.NavigationService.GoBackRequested -= OnBackRequested;
                var frame = GetFrame();
                if (frame != null)
                {
                    frame.Navigating -= OnFrameNavigating;
                }
            }

            if (_stateGroup != null)
            {
                _stateGroup.CurrentStateChanged -= OnVisualStateChanged;
                _stateGroup = null;
            }
        }

        /// <summary>
        /// Fires when the addaptive trigger changes state.
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The event args</param>
        /// <remarks>
        /// Handles showing/hiding the back button when the state changes
        /// </remarks>
        private void OnVisualStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            UpdateView(false);
        }

        /// <summary>
        /// Closes the details pane if we are in narrow state
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="args">The event args</param>
        private void OnFrameNavigating(object sender, NavigatingCancelEventArgs args)
        {
            if ((args.NavigationMode == NavigationMode.Back) && (ViewState == MasterDetailsViewState.Details))
            {
                SelectedItem = null;
                args.Cancel = true;
            }
        }

        /// <summary>
        /// Closes the details pane if we are in narrow state
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="args">The event args</param>
        private void OnBackRequested(object sender, GoBackRequestedEventArgs args)
        {
            if (ViewState == MasterDetailsViewState.Details)
            {
                SelectedItem = null;
                args.Handled = true;
            }
        }

        private void SetMasterHeaderVisibility()
        {
            if (GetTemplateChild(PartHeaderContentPresenter) is FrameworkElement headerPresenter)
            {
                headerPresenter.Visibility = MasterHeader != null
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UpdateView(bool animate)
        {
            var currentState = ViewState;
            UpdateViewState();
            if (_stateGroup != null)
            {
                SetVisualState(_stateGroup.CurrentState, animate);
            }
        }

        private Frame? GetFrame()
        {
            return _frame ?? (_frame = this.FindAscendant<Frame>());
        }

        private void UpdateViewState()
        {
            if (_stateGroup == null)
            {
                return;
            }

            var before = ViewState;

            if (_stateGroup.CurrentState == _narrowState || _stateGroup.CurrentState == null)
            {
                ViewState = SelectedItem == null ? MasterDetailsViewState.Master : MasterDetailsViewState.Details;
            }
            else
            {
                ViewState = MasterDetailsViewState.Both;
            }

            var after = ViewState;

            if (before != after)
            {
                ViewStateChanged?.Invoke(this, after);
            }
        }

        private void SetVisualState(VisualState state, bool animate)
        {
            var noSelectionState = state == _narrowState
                ? NoSelectionNarrowState
                : NoSelectionWideState;
            VisualStateManager.GoToState(this, SelectedItem == null ? noSelectionState : HasSelectionState, animate);
        }

        private void SetDetailsContent()
        {
            if (_detailsPresenter != null)
            {
                var item = MapDetails?.Invoke(SelectedItem) ?? SelectedItem;

                _detailsPresenter.ContentTemplate = DetailsTemplateSelector != null
                    ? DetailsTemplateSelector.SelectTemplate(item, _detailsPresenter)
                    : DetailsTemplate;
                _detailsPresenter.Content = item;
            }
        }

        private void OnMasterCommandBarChanged()
        {
            if (GetTemplateChild("DetailsCommandBarPanel") is Panel panel)
            {
                panel.Children.Clear();
                if (DetailsCommandBar != null)
                {
                    panel.Children.Add(DetailsCommandBar);
                }
            }
        }

        private void OnDetailsCommandBarChanged()
        {
            if (GetTemplateChild("MasterCommandBarPanel") is Panel panel)
            {
                panel.Children.Clear();
                if (MasterCommandBar != null)
                {
                    panel.Children.Add(MasterCommandBar);
                }
            }
        }
    }
}
