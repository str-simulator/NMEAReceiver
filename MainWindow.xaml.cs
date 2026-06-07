using NMEAReceiver.ViewModels.Shell;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System.Windows;
using System.Windows.Media;

namespace NMEAReceiver;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SetSvgIcon();
    }

    private void SetSvgIcon()
    {
        var uri = new Uri("/NMEAReceiver;component/res/NMEAReceiver.svg", UriKind.Relative);
        var streamInfo = Application.GetResourceStream(uri);
        if (streamInfo is null) return;

        using var stream = streamInfo.Stream;
        var settings = new WpfDrawingSettings { IncludeRuntime = true, TextAsGeometry = false };
        var reader = new FileSvgReader(settings);
        var drawing = reader.Read(stream);
        if (drawing is not null)
            Icon = new DrawingImage(drawing);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
        base.OnClosed(e);
    }
}
