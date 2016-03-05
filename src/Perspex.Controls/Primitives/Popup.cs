﻿// Copyright (c) The Perspex Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using System.Linq;
using Perspex.Input;
using Perspex.Interactivity;
using Perspex.LogicalTree;
using Perspex.Metadata;
using Perspex.Rendering;
using Perspex.VisualTree;

namespace Perspex.Controls.Primitives
{
    /// <summary>
    /// Displays a popup window.
    /// </summary>
    public class Popup : Control, IVisualTreeHost
    {
        /// <summary>
        /// Defines the <see cref="Child"/> property.
        /// </summary>
        public static readonly StyledProperty<Control> ChildProperty =
            PerspexProperty.Register<Popup, Control>(nameof(Child));

        /// <summary>
        /// Defines the <see cref="IsOpen"/> property.
        /// </summary>
        public static readonly DirectProperty<Popup, bool> IsOpenProperty =
            PerspexProperty.RegisterDirect<Popup, bool>(
                nameof(IsOpen),
                o => o.IsOpen,
                (o, v) => o.IsOpen = v);

        /// <summary>
        /// Defines the <see cref="PlacementMode"/> property.
        /// </summary>
        public static readonly StyledProperty<PlacementMode> PlacementModeProperty =
            PerspexProperty.Register<Popup, PlacementMode>(nameof(PlacementMode), defaultValue: PlacementMode.Bottom);

        /// <summary>
        /// Defines the <see cref="PlacementTarget"/> property.
        /// </summary>
        public static readonly StyledProperty<Control> PlacementTargetProperty =
            PerspexProperty.Register<Popup, Control>(nameof(PlacementTarget));

        /// <summary>
        /// Defines the <see cref="StaysOpen"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> StaysOpenProperty =
            PerspexProperty.Register<Popup, bool>(nameof(StaysOpen), true);

        private bool _isOpen;
        private PopupRoot _popupRoot;
        private TopLevel _topLevel;

        /// <summary>
        /// Initializes static members of the <see cref="Popup"/> class.
        /// </summary>
        static Popup()
        {
            IsHitTestVisibleProperty.OverrideDefaultValue<Popup>(false);
            ChildProperty.Changed.AddClassHandler<Popup>(x => x.ChildChanged);
            IsOpenProperty.Changed.AddClassHandler<Popup>(x => x.IsOpenChanged);
        }

        /// <summary>
        /// Raised when the popup closes.
        /// </summary>
        public event EventHandler Closed;

        /// <summary>
        /// Raised when the popup opens.
        /// </summary>
        public event EventHandler Opened;

        /// <summary>
        /// Raised when the popup root has been created, but before it has been shown.
        /// </summary>
        public event EventHandler PopupRootCreated;

        /// <summary>
        /// Gets or sets the control to display in the popup.
        /// </summary>
        [Content]
        public Control Child
        {
            get { return GetValue(ChildProperty); }
            set { SetValue(ChildProperty, value); }
        }

        /// <summary>
        /// Gets or sets a dependency resolver for the <see cref="PopupRoot"/>.
        /// </summary>
        /// <remarks>
        /// This property allows a client to customize the behaviour of the popup by injecting
        /// a specialized dependency resolver into the <see cref="PopupRoot"/>'s constructor.
        /// </remarks>
        public IPerspexDependencyResolver DependencyResolver
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the popup is currently open.
        /// </summary>
        public bool IsOpen
        {
            get { return _isOpen; }
            set { SetAndRaise(IsOpenProperty, ref _isOpen, value); }
        }

        /// <summary>
        /// Gets or sets the placement mode of the popup in relation to the <see cref="PlacementTarget"/>.
        /// </summary>
        public PlacementMode PlacementMode
        {
            get { return GetValue(PlacementModeProperty); }
            set { SetValue(PlacementModeProperty, value); }
        }

        /// <summary>
        /// Gets or sets the control that is used to determine the popup's position.
        /// </summary>
        public Control PlacementTarget
        {
            get { return GetValue(PlacementTargetProperty); }
            set { SetValue(PlacementTargetProperty, value); }
        }

        /// <summary>
        /// Gets the root of the popup window.
        /// </summary>
        public PopupRoot PopupRoot => _popupRoot;

        /// <summary>
        /// Gets or sets a value indicating whether the popup should stay open when the popup is
        /// pressed or loses focus.
        /// </summary>
        public bool StaysOpen
        {
            get { return GetValue(StaysOpenProperty); }
            set { SetValue(StaysOpenProperty, value); }
        }

        /// <summary>
        /// Gets the root of the popup window.
        /// </summary>
        IVisual IVisualTreeHost.Root => _popupRoot;

        /// <summary>
        /// Opens the popup.
        /// </summary>
        public void Open()
        {
            if (_popupRoot == null)
            {
                _popupRoot = new PopupRoot(DependencyResolver)
                {
                    [~ContentControl.ContentProperty] = this[~ChildProperty],
                    [~WidthProperty] = this[~WidthProperty],
                    [~HeightProperty] = this[~HeightProperty],
                    [~MinWidthProperty] = this[~MinWidthProperty],
                    [~MaxWidthProperty] = this[~MaxWidthProperty],
                    [~MinHeightProperty] = this[~MinHeightProperty],
                    [~MaxHeightProperty] = this[~MaxHeightProperty],
                };

                ((ISetLogicalParent)_popupRoot).SetParent(this);
            }

            _popupRoot.Position = GetPosition();

            if (_topLevel == null && PlacementTarget != null)
            {
                _topLevel = PlacementTarget.GetSelfAndLogicalAncestors().First(x => x is TopLevel) as TopLevel;
            }

            if (_topLevel != null)
            {
                _topLevel.Deactivated += TopLevelDeactivated;
                _topLevel.AddHandler(PointerPressedEvent, PointerPressedOutside, RoutingStrategies.Tunnel);
            }

            PopupRootCreated?.Invoke(this, EventArgs.Empty);

            _popupRoot.Show();
            IsOpen = true;
            Opened?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Closes the popup.
        /// </summary>
        public void Close()
        {
            if (_popupRoot != null)
            {
                if (_topLevel != null)
                {
                    _topLevel.RemoveHandler(PointerPressedEvent, PointerPressedOutside);
                    _topLevel.Deactivated -= TopLevelDeactivated;
                }

                _popupRoot.Hide();
            }

            IsOpen = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Measures the control.
        /// </summary>
        /// <param name="availableSize">The available size for the control.</param>
        /// <returns>A size of 0,0 as Popup itself takes up no space.</returns>
        protected override Size MeasureCore(Size availableSize)
        {
            return new Size();
        }

        /// <inheritdoc/>
        protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnAttachedToLogicalTree(e);
            _topLevel = e.Root as TopLevel;
        }

        /// <inheritdoc/>
        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            _topLevel = null;
            _popupRoot?.Dispose();
            _popupRoot = null;
        }

        /// <summary>
        /// Called when the <see cref="IsOpen"/> property changes.
        /// </summary>
        /// <param name="e">The event args.</param>
        private void IsOpenChanged(PerspexPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                Open();
            }
            else
            {
                Close();
            }
        }

        /// <summary>
        /// Called when the <see cref="Child"/> property changes.
        /// </summary>
        /// <param name="e">The event args.</param>
        private void ChildChanged(PerspexPropertyChangedEventArgs e)
        {
            LogicalChildren.Clear();

            ((ISetLogicalParent)e.OldValue)?.SetParent(null);

            if (e.NewValue != null)
            {
                ((ISetLogicalParent)e.NewValue).SetParent(this);
                LogicalChildren.Add((ILogical)e.NewValue);
            }
        }

        /// <summary>
        /// Gets the position for the popup based on the placement properties.
        /// </summary>
        /// <returns>The popup's position in screen coordinates.</returns>
        protected virtual Point GetPosition()
        {
            var zero = default(Point);
            var target = PlacementTarget ?? this.GetVisualParent<Control>();

            switch (PlacementMode)
            {
                case PlacementMode.Pointer:
                    return MouseDevice.Instance.Position;

                case PlacementMode.Bottom:
                    return target?.PointToScreen(new Point(0, target.Bounds.Height)) ?? zero;

                case PlacementMode.Right:
                    return target?.PointToScreen(new Point(target.Bounds.Width, 0)) ?? zero;

                default:
                    throw new InvalidOperationException("Invalid value for Popup.PlacementMode");
            }
        }

        private void PointerPressedOutside(object sender, PointerPressedEventArgs e)
        {
            if (!StaysOpen)
            {
                var root = ((IVisual)e.Source).GetVisualRoot();

                if (root != this.PopupRoot)
                {
                    Close();
                    e.Handled = true;
                }
            }
        }

        private void TopLevelDeactivated(object sender, EventArgs e)
        {
            if (!StaysOpen)
            {
                Close();
            }
        }
    }
}