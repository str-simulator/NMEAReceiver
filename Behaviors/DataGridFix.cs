using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using NMEAReceiver.Converters;

namespace NMEAReceiver.Behaviors;

public static class DataGridFix
{
    public static readonly DependencyProperty FixCellsPanelOffsetProperty =
        DependencyProperty.RegisterAttached(
            "FixCellsPanelOffset",
            typeof(bool),
            typeof(DataGridFix),
            new PropertyMetadata(false, OnChanged));

    public static void SetFixCellsPanelOffset(DependencyObject obj, bool value)
        => obj.SetValue(FixCellsPanelOffsetProperty, value);

    public static bool GetFixCellsPanelOffset(DependencyObject obj)
        => (bool)obj.GetValue(FixCellsPanelOffsetProperty);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DataGrid dg && (bool)e.NewValue)
            dg.Loaded += OnLoaded;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid dg)
            Walk(dg);
    }

    private static void Walk(DependencyObject node)
    {
        if (node is Button btn)
        {
            var b = BindingOperations.GetBinding(btn, FrameworkElement.WidthProperty);
            if (b?.Path?.Path == "CellsPanelHorizontalOffset")
            {
                BindingOperations.SetBinding(btn, FrameworkElement.WidthProperty,
                    new Binding("CellsPanelHorizontalOffset")
                    {
                        RelativeSource = b.RelativeSource,
                        Converter = NonNegativeDoubleConverter.Instance,
                        FallbackValue = 0.0,
                        Mode = BindingMode.OneWay
                    });
                return;
            }
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            Walk(VisualTreeHelper.GetChild(node, i));
    }
}
