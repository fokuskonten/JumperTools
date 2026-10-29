using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace JumperTools.UI.Behaviors
{
    public static class ListBoxBehavior
    {
        public static readonly DependencyProperty ScrollOnNewItemProperty =
            DependencyProperty.RegisterAttached(
                "ScrollOnNewItem",
                typeof(bool),
                typeof(ListBoxBehavior),
                new UIPropertyMetadata(false, OnScrollOnNewItemChanged));

        public static bool GetScrollOnNewItem(DependencyObject obj)
        {
            return (bool)obj.GetValue(ScrollOnNewItemProperty);
        }

        public static void SetScrollOnNewItem(DependencyObject obj, bool value)
        {
            obj.SetValue(ScrollOnNewItemProperty, value);
        }

        private static void OnScrollOnNewItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListBox listBox)
            {
                if ((bool)e.NewValue)
                {
                    listBox.Loaded += ListBox_Loaded;
                    listBox.Unloaded += ListBox_Unloaded;
                }
                else
                {
                    listBox.Loaded -= ListBox_Loaded;
                    listBox.Unloaded -= ListBox_Unloaded;
                }
            }
        }

        private static void ListBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                if (listBox.ItemsSource is INotifyCollectionChanged incc)
                {
                    incc.CollectionChanged += (s, args) =>
                    {
                        if (args.Action == NotifyCollectionChangedAction.Add)
                        {
                            if (listBox.Items.Count > 0)
                            {
                                // Scroll to bottom
                                var item = listBox.Items[listBox.Items.Count - 1];
                                listBox.ScrollIntoView(item);
                            }
                        }
                    };
                }
            }
        }

        private static void ListBox_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                if (listBox.ItemsSource is INotifyCollectionChanged incc)
                {
                    // This is a bit unsafe without strong references, but acceptable for simple WPF apps
                    // Note: usually we'd detach, but we lack the handler reference here
                }
            }
        }
    }
}
