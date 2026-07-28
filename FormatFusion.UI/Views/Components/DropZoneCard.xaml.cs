using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FormatFusion.UI.Views.Components;

public partial class DropZoneCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(DropZoneCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(DropZoneCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register("Icon", typeof(System.Windows.Media.Geometry), typeof(DropZoneCard), new PropertyMetadata(null));

    public static readonly DependencyProperty FileFilterProperty =
        DependencyProperty.Register(nameof(FileFilter), typeof(string), typeof(DropZoneCard), new PropertyMetadata(
            "All Supported Files|*.jpg;*.jpeg;*.png;*.webp;*.heic;*.heif;*.bmp;*.gif;*.tiff;*.tif;" +
            "*.cr2;*.cr3;*.nef;*.arw;*.dng;*.ico;" +
            "*.mp3;*.wav;*.flac;*.aac;*.ogg;*.opus;*.m4a;" +
            "*.mp4;*.mkv;*.avi;*.mov;*.webm;*.flv;*.wmv;*.3gp;" +
            "*.pdf;*.docx;*.txt;*.rtf;*.odt;*.epub;" +
            "*.zip;*.7z;*.rar;*.tar;*.gz|All Files|*.*"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public System.Windows.Media.Geometry Icon
    {
        get => (System.Windows.Media.Geometry)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string FileFilter
    {
        get => (string)GetValue(FileFilterProperty);
        set => SetValue(FileFilterProperty, value);
    }

    public event EventHandler<string[]>? FilesDropped;

    public DropZoneCard()
    {
        InitializeComponent();
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var brush = TryFindResource("DropZoneHoverBrush") as Brush ?? Brushes.DarkGray;
            var borderBrush = TryFindResource("AccentBrush") as Brush ?? Brushes.Blue;
            DropZoneBorder.Background = brush;
            DropZoneBorder.BorderBrush = borderBrush;
            e.Effects = DragDropEffects.Copy;
        }
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        var brush = TryFindResource("Surface2Brush") as Brush ?? Brushes.Gray;
        var borderBrush = TryFindResource("DropZoneBorderBrush") as Brush ?? Brushes.DarkGray;
        DropZoneBorder.Background = brush;
        DropZoneBorder.BorderBrush = borderBrush;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        DropZone_DragLeave(sender, e); // Reset colors
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            FilesDropped?.Invoke(this, files);
        }
        e.Handled = true;
    }

    private void DropZone_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Select files to convert or compress",
            Filter = FileFilter
        };

        if (dialog.ShowDialog() == true)
        {
            FilesDropped?.Invoke(this, dialog.FileNames);
        }
    }
}
