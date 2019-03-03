using System.Windows;
using System.Windows.Controls;

namespace clausewitz_performance_data_logger
{
    /// <summary>
    ///     Allows auto-scrolling to bottom on text update.
    /// </summary>
    public static class AutoScroll
    {
        /// <summary>
        ///     Custom dependency property.
        /// </summary>
        public static readonly DependencyProperty AutoScrollProperty = DependencyProperty.RegisterAttached(
                                                                                                           "AutoScroll",
                                                                                                           typeof(bool),
                                                                                                           typeof(AutoScroll),
                                                                                                           new PropertyMetadata(false, AutoScrollPropertyChanged)
                                                                                                          );

        #region Events

        /// <summary>
        ///     Custom property event.
        /// </summary>
        public static void AutoScrollPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var scrollViewer = obj as ScrollViewer;

            if (scrollViewer != null && (bool)args.NewValue)
            {
                scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
                scrollViewer.ScrollToEnd();
            }
            else
            {
                scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
            }
        }

        /// <summary>
        ///     ScrollViever event.
        /// </summary>
        private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange != 0)
            {
                var scrollViewer = sender as ScrollViewer;
                scrollViewer?.ScrollToBottom();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Custom property getter.
        /// </summary>
        public static bool GetAutoScroll(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoScrollProperty);
        }

        /// <summary>
        ///     Custom property setter.
        /// </summary>
        public static void SetAutoScroll(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoScrollProperty, value);
        }

        #endregion
    }
}
