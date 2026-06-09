using System.Windows.Controls;

namespace NMEAReceiver.Views.Panels;

public partial class DiagnosticView : UserControl
{
    public DiagnosticView() => InitializeComponent();

    private void RawSentenceBox_TextChanged(object sender, TextChangedEventArgs e)
        => ((TextBox)sender).ScrollToEnd();

    private void LogBox_TextChanged(object sender, TextChangedEventArgs e)
        => ((TextBox)sender).ScrollToEnd();
}
