using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace NMEAReceiver.Views.Panels;

public partial class ChannelsTableView : UserControl
{
    public ChannelsTableView() => InitializeComponent();

    private void OnDataGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid dg) return;
        var portCol = dg.Columns.FirstOrDefault(c => "Port".Equals(c.Header));
        if (portCol is not null)
            portCol.SortDirection = ListSortDirection.Ascending;
    }
}
