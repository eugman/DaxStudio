using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace DaxStudio.UI.AttachedProperties
{
    /// <summary>
    /// Attached property that enables smooth animation of Canvas.Left and Canvas.Top
    /// when the bound X/Y properties change. Uses a quick ease-out animation.
    /// </summary>
    public static class CanvasPositionAnimation
    {
        /// <summary>
        /// Animation duration in milliseconds
        /// </summary>
        private const int AnimationDurationMs = 200;

        /// <summary>
        /// Global flag to temporarily disable animations during bulk operations.
        /// Set to true during Expand All/Collapse All/Show Issues to prevent performance issues.
        /// </summary>
        public static bool SuspendAnimations { get; set; } = false;

        /// <summary>
        /// Gets the IsEnabled attached property value.
        /// </summary>
        public static bool GetIsEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsEnabledProperty);
        }

        /// <summary>
        /// Sets the IsEnabled attached property value.
        /// </summary>
        public static void SetIsEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsEnabledProperty, value);
        }

        /// <summary>
        /// Attached property to enable position animation on a Canvas child.
        /// </summary>
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(CanvasPositionAnimation),
                new PropertyMetadata(false, OnIsEnabledChanged));

        /// <summary>
        /// Gets the AnimatedLeft property (the target X position).
        /// </summary>
        public static double GetAnimatedLeft(DependencyObject obj)
        {
            return (double)obj.GetValue(AnimatedLeftProperty);
        }

        /// <summary>
        /// Sets the AnimatedLeft property (the target X position).
        /// </summary>
        public static void SetAnimatedLeft(DependencyObject obj, double value)
        {
            obj.SetValue(AnimatedLeftProperty, value);
        }

        /// <summary>
        /// The AnimatedLeft property - bind this instead of Canvas.Left.
        /// When this changes, Canvas.Left will animate to the new value.
        /// </summary>
        public static readonly DependencyProperty AnimatedLeftProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedLeft",
                typeof(double),
                typeof(CanvasPositionAnimation),
                new PropertyMetadata(0.0, OnAnimatedLeftChanged));

        /// <summary>
        /// Gets the AnimatedTop property (the target Y position).
        /// </summary>
        public static double GetAnimatedTop(DependencyObject obj)
        {
            return (double)obj.GetValue(AnimatedTopProperty);
        }

        /// <summary>
        /// Sets the AnimatedTop property (the target Y position).
        /// </summary>
        public static void SetAnimatedTop(DependencyObject obj, double value)
        {
            obj.SetValue(AnimatedTopProperty, value);
        }

        /// <summary>
        /// The AnimatedTop property - bind this instead of Canvas.Top.
        /// When this changes, Canvas.Top will animate to the new value.
        /// </summary>
        public static readonly DependencyProperty AnimatedTopProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedTop",
                typeof(double),
                typeof(CanvasPositionAnimation),
                new PropertyMetadata(0.0, OnAnimatedTopChanged));

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // When enabled, set initial positions without animation
            if (d is FrameworkElement element && (bool)e.NewValue)
            {
                double left = GetAnimatedLeft(element);
                double top = GetAnimatedTop(element);
                Canvas.SetLeft(element, left);
                Canvas.SetTop(element, top);
            }
        }

        private static void OnAnimatedLeftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && GetIsEnabled(element))
            {
                double newValue = (double)e.NewValue;
                double oldValue = Canvas.GetLeft(element);

                // Skip animation if: first set, values nearly equal, or animations suspended for bulk operations
                if (double.IsNaN(oldValue) || Math.Abs(oldValue - newValue) < 0.5 || SuspendAnimations)
                {
                    Canvas.SetLeft(element, newValue);
                    return;
                }

                AnimatePosition(element, Canvas.LeftProperty, oldValue, newValue);
            }
            else if (d is FrameworkElement elem)
            {
                // Animation disabled, set directly
                Canvas.SetLeft(elem, (double)e.NewValue);
            }
        }

        private static void OnAnimatedTopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && GetIsEnabled(element))
            {
                double newValue = (double)e.NewValue;
                double oldValue = Canvas.GetTop(element);

                // Skip animation if: first set, values nearly equal, or animations suspended for bulk operations
                if (double.IsNaN(oldValue) || Math.Abs(oldValue - newValue) < 0.5 || SuspendAnimations)
                {
                    Canvas.SetTop(element, newValue);
                    return;
                }

                AnimatePosition(element, Canvas.TopProperty, oldValue, newValue);
            }
            else if (d is FrameworkElement elem)
            {
                // Animation disabled, set directly
                Canvas.SetTop(elem, (double)e.NewValue);
            }
        }

        private static void AnimatePosition(FrameworkElement element, DependencyProperty property, double from, double to)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(AnimationDurationMs)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            element.BeginAnimation(property, animation);
        }
    }
}
