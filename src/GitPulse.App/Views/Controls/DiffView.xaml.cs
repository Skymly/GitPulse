using GitPulse.App.Services;

namespace GitPulse.App.Views.Controls;

/// <summary>
/// Reusable diff rendering control. Wraps a <c>WebView</c> and generates
/// colored HTML from a unified diff patch string via
/// <see cref="DiffHtmlGenerator"/>.
/// </summary>
/// <remarks>
/// Set <see cref="Patch"/> to the GitHub patch text and the control
/// renders a syntax-colored unified diff. When <c>Patch</c> is null or
/// empty (binary files), a "binary file" notice is shown instead.
/// </remarks>
public partial class DiffView : ContentView
{
    /// <summary>Bindable property for the diff patch text.</summary>
    public static readonly BindableProperty PatchProperty =
        BindableProperty.Create(
            nameof(Patch),
            typeof(string),
            typeof(DiffView),
            null,
            propertyChanged: OnPatchChanged);

    /// <summary>Bindable property for the filename (shown for binary files).</summary>
    public static readonly BindableProperty FilenameProperty =
        BindableProperty.Create(
            nameof(Filename),
            typeof(string),
            typeof(DiffView),
            string.Empty,
            propertyChanged: OnPatchChanged);

    public string? Patch
    {
        get => (string?)GetValue(PatchProperty);
        set => SetValue(PatchProperty, value);
    }

    public string Filename
    {
        get => (string)GetValue(FilenameProperty);
        set => SetValue(FilenameProperty, value);
    }

    public DiffView()
    {
        InitializeComponent();
    }

    private static void OnPatchChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is DiffView view)
            view.RenderDiff();
    }

    private void RenderDiff()
    {
        var html = DiffHtmlGenerator.GenerateHtml(Patch, Filename);
        DiffWebView.Source = new HtmlWebViewSource { Html = html };
    }
}
