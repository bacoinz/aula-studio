using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aula.Media;
using Aula.Protocol;
using AulaStudio.Localization;
using AulaStudio.Services;
using Microsoft.Win32;

namespace AulaStudio.Views.Pages;

public partial class ScreenPage : Page
{
    private static readonly string[] Extensions =
        [".gif", ".mp4", ".mov", ".webm", ".mkv", ".avi", ".m4v", ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".apng"];

    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer _animation = new();

    /// <summary>A file passed on the command line ("Open with"), opened when the page first loads.</summary>
    public static string? PendingFile { get; set; }

    private MediaInfo? _info;
    private Clip? _clip;
    private string _clipName = "";
    private List<BitmapSource> _previewFrames = [];
    private int _previewIndex;
    private CancellationTokenSource? _convertCts;
    private double _grabbedAt = double.NaN;
    private bool _updatingUi;
    private bool _sending;

    public ScreenPage()
    {
        InitializeComponent();
        Composer.Changed += ShowComposed;
        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await ConvertAsync();
        };
        _animation.Tick += (_, _) => NextPreviewFrame();
        Crop.CropChanged += () =>
        {
            _updatingUi = true;
            ZoomSlider.Value = Crop.Zoom;
            _updatingUi = false;
            Schedule();
        };
        FfmpegMissing.Visibility = Ffmpeg.IsAvailable ? Visibility.Collapsed : Visibility.Visible;
        App.Keyboard.PropertyChanged += (_, _) => UpdateSendButton();
        Loc.Instance.PropertyChanged += (_, _) => { UpdateTexts(); LoadRecent(); };
        Loaded += async (_, _) =>
        {
            LoadRecent();
            if (PendingFile is { } file)
            {
                PendingFile = null;
                await OpenAsync(file);
            }
        };
        Unloaded += (_, _) => _animation.Stop();
        Loaded += (_, _) => { if (_previewFrames.Count > 1) _animation.Start(); };
    }

    // ---------- Opening files ----------

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DroppedFile(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (DroppedFile(e) is { } path) await OpenAsync(path);
    }

    private static string? DroppedFile(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files
        && Extensions.Contains(Path.GetExtension(files[0]).ToLowerInvariant()) ? files[0] : null;

    private async void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{Loc.T("Screen_FileFilter")}|{string.Join(';', Extensions.Select(x => "*" + x))}",
        };
        if (dialog.ShowDialog() == true) await OpenAsync(dialog.FileName);
    }

    private async Task OpenAsync(string path)
    {
        if (!Ffmpeg.IsAvailable)
        {
            Notify.Error(Loc.T("Screen_NoFfmpeg"));
            return;
        }
        try
        {
            var info = await MediaInfo.ProbeAsync(path);
            _info = info;
            _clipName = Path.GetFileNameWithoutExtension(path);
            _grabbedAt = double.NaN;

            _updatingUi = true;
            var hasTime = info.Kind != MediaKind.Image;
            TimePanel.Visibility = hasTime ? Visibility.Visible : Visibility.Collapsed;
            if (hasTime)
            {
                StartSlider.Maximum = EndSlider.Maximum = info.DurationSeconds;
                StartSlider.Value = 0;
                EndSlider.Value = info.DurationSeconds;
            }
            FpsAuto.IsChecked = true;
            FpsSlider.IsEnabled = false;
            ZoomSlider.Value = 1;
            _updatingUi = false;

            DropZone.Visibility = Visibility.Collapsed;
            ComposerPanel.Visibility = Visibility.Collapsed;
            Editor.Visibility = Visibility.Visible;
            UpdateTexts();
            await GrabFrameAsync(resetCrop: true);
            await ConvertAsync();
        }
        catch (MediaException ex)
        {
            Notify.Error(ex.Message);
        }
    }

    // ---------- Editing ----------

    private double Start => _info?.Kind == MediaKind.Image ? 0 : StartSlider.Value;
    private double End => _info?.Kind == MediaKind.Image ? 0 : EndSlider.Value;

    private ClipOptions Options() => new(
        Start: Start,
        End: End,
        Mode: ModeFit.IsChecked == true ? FitMode.Fit : FitMode.Fill,
        Crop: Crop.Crop,
        Fps: FpsAuto.IsChecked == true ? null : Math.Round(FpsSlider.Value, 1));

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _updatingUi) return;
        var fill = ModeFill.IsChecked == true;
        Crop.ShowsCrop = fill;
        ZoomSlider.IsEnabled = fill;
        CropHint.Visibility = fill ? Visibility.Visible : Visibility.Collapsed;
        UpdateTexts();
        Schedule();
    }

    private void OnZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingUi || !IsLoaded) return;
        Crop.Zoom = ZoomSlider.Value;
        Schedule();
    }

    private void OnRangeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingUi || _info is null) return;
        _updatingUi = true;
        // Keep at least a tenth of a second between start and end.
        if (sender == StartSlider && StartSlider.Value > EndSlider.Value - 0.1) EndSlider.Value = Math.Min(EndSlider.Maximum, StartSlider.Value + 0.1);
        if (sender == EndSlider && EndSlider.Value < StartSlider.Value + 0.1) StartSlider.Value = Math.Max(0, EndSlider.Value - 0.1);
        _updatingUi = false;
        UpdateTexts();
        Schedule();
    }

    private void OnFpsAutoClicked(object sender, RoutedEventArgs e)
    {
        FpsSlider.IsEnabled = FpsAuto.IsChecked != true;
        UpdateTexts();
        Schedule();
    }

    private void Schedule()
    {
        if (_info is null) return;
        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>Refreshes the crop editor's background when the start time moved.</summary>
    private async Task GrabFrameAsync(bool resetCrop)
    {
        if (_info is null || (!resetCrop && Math.Abs(_grabbedAt - Start) < 0.01)) return;
        var keep = Crop.Crop;
        var png = await ClipConverter.GrabFrameAsync(_info, Start, 960);
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = new MemoryStream(png);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        Crop.SetSource(image, _info.Width, _info.Height, resetCrop);
        if (!resetCrop) Crop.Crop = keep;
        _grabbedAt = Start;
    }

    private async Task ConvertAsync()
    {
        if (_info is null) return;
        _convertCts?.Cancel();
        var cts = _convertCts = new CancellationTokenSource();
        Converting.Visibility = Visibility.Visible;
        SendButton.IsEnabled = false;
        try
        {
            await GrabFrameAsync(resetCrop: false);
            var clip = await ClipConverter.ConvertAsync(_info, Options(), cts.Token);
            if (cts.IsCancellationRequested) return;
            ShowClip(clip);
        }
        catch (OperationCanceledException)
        {
            // A newer conversion replaced this one.
        }
        catch (MediaException ex)
        {
            Notify.Error(ex.Message);
        }
        finally
        {
            if (_convertCts == cts) Converting.Visibility = Visibility.Collapsed;
            UpdateSendButton();
        }
    }

    // ---------- Preview ----------

    private void ShowClip(Clip clip)
    {
        _clip = clip;
        _previewFrames = clip.Frames.Select(FrameBitmaps.AsShownOnScreen).ToList();
        _previewIndex = -1;
        _animation.Stop();
        NextPreviewFrame();
        if (_previewFrames.Count > 1) _animation.Start();
        UpdateTexts();
        UpdateSendButton();
    }

    private void NextPreviewFrame()
    {
        if (_clip is null || _previewFrames.Count == 0) return;
        _previewIndex = (_previewIndex + 1) % _previewFrames.Count;
        Preview.Source = _previewFrames[_previewIndex];
        _animation.Interval = TimeSpan.FromMilliseconds(Math.Max(10, _clip.DelaysMs[_previewIndex]));
    }

    private void UpdateTexts()
    {
        if (_info is not null)
        {
            FileName.Text = _clipName;
            var kind = Loc.T(_info.Kind switch { MediaKind.Video => "Screen_Video", MediaKind.Animation => "Screen_Animation", _ => "Screen_Image" });
            FileInfo.Text = _info.Kind == MediaKind.Image
                ? $"{kind} · {_info.Width}×{_info.Height}"
                : $"{kind} · {_info.Width}×{_info.Height} · {_info.DurationSeconds:0.0} s · {_info.FrameRate:0.#} fps";
            StartText.Text = $"{StartSlider.Value:0.0} s";
            EndText.Text = $"{EndSlider.Value:0.0} s";

            if (_info.Kind != MediaKind.Image)
            {
                var auto = ClipConverter.AutoFps(_info, Start, End);
                FpsSlider.Maximum = Math.Max(1, Math.Min(ClipConverter.MaxFps, LcdImage.MaxFrames / Math.Max(0.1, End - Start)));
                if (FpsAuto.IsChecked == true)
                {
                    _updatingUi = true;
                    FpsSlider.Value = Math.Min(auto, FpsSlider.Maximum);
                    _updatingUi = false;
                }
            }
        }
        FrameCount.Text = _clip is null ? "" : Loc.T("Screen_FrameCount", _clip.Frames.Count, LcdImage.MaxFrames, _clip.TotalMs / 1000.0);
        Estimate.Text = _clip is null ? "" : Loc.T("Screen_Estimate", Duration(AulaKeyboard.EstimateUpload(_clip.Frames.Count)));
    }

    private static string Duration(TimeSpan t) => t.TotalSeconds < 60
        ? Loc.T("Time_Seconds", (int)Math.Ceiling(t.TotalSeconds))
        : Loc.T("Time_Minutes", (int)t.TotalMinutes, t.Seconds);

    // ---------- Sending ----------

    private void UpdateSendButton() =>
        SendButton.IsEnabled = _clip is not null && !_sending && App.Keyboard.IsConnected && Converting.Visibility != Visibility.Visible;

    private async void OnSend(object sender, RoutedEventArgs e)
    {
        if (_clip is not { } clip) return;
        _sending = true;
        UpdateSendButton();
        SendProgress.Value = 0;
        SendProgress.Visibility = Visibility.Visible;
        var progress = new Progress<double>(p =>
        {
            SendProgress.Value = p;
            SendStatus.Text = Loc.T("Screen_Sending", p * 100);
        });
        var watch = Stopwatch.StartNew();
        try
        {
            var image = clip.ToLcdImage();
            await App.Keyboard.RunAsync(kb => kb.UploadLcdImage(image, progress));
            SendStatus.Text = "";
            Notify.Success(Loc.T("Screen_Sent"), Loc.T("Screen_SentDetail", clip.Frames.Count, watch.Elapsed.TotalSeconds));
            await Task.Run(() => ClipLibrary.Save(_clipName, clip));
            LoadRecent();
        }
        catch (Exception ex) when (ex is AulaException or IOException)
        {
            SendStatus.Text = "";
            Notify.Error(ex.Message);
        }
        finally
        {
            _sending = false;
            SendProgress.Visibility = Visibility.Collapsed;
            UpdateSendButton();
        }
    }

    // ---------- Image composer ----------

    private void OnCompose(object sender, RoutedEventArgs e)
    {
        _convertCts?.Cancel();
        _info = null;
        DropZone.Visibility = Visibility.Collapsed;
        Editor.Visibility = Visibility.Collapsed;
        ComposerPanel.Visibility = Visibility.Visible;
        ShowComposed();
    }

    private void OnComposerBack(object sender, RoutedEventArgs e)
    {
        ComposerPanel.Visibility = Visibility.Collapsed;
        DropZone.Visibility = Visibility.Visible;
    }

    /// <summary>A still image is a one-frame clip, so preview, library and sending work unchanged.</summary>
    private void ShowComposed()
    {
        _clipName = Composer.Title.Length > 0 ? Composer.Title : Loc.T("Composer_DefaultName");
        ShowClip(new Clip([Composer.Render()], [1000]));
    }

    // ---------- Recent uploads ----------

    public sealed record RecentItem(ClipLibrary.Entry Entry, BitmapImage? Thumbnail, string Name, string Detail);

    private void LoadRecent()
    {
        var items = ClipLibrary.List().Select(e => new RecentItem(e, LoadThumbnail(e.Thumbnail), e.Name,
            Loc.T("Screen_FrameCount", e.Frames, LcdImage.MaxFrames, e.TotalMs / 1000.0))).ToList();
        Recent.ItemsSource = items;
        RecentEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static BitmapImage? LoadThumbnail(string path)
    {
        if (!File.Exists(path)) return null;
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path);
        image.CacheOption = BitmapCacheOption.OnLoad; // don't lock the file, so the entry can be deleted
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void OnUseRecent(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RecentItem item) return;
        try
        {
            _convertCts?.Cancel();
            _info = null;
            _clipName = item.Name;
            Editor.Visibility = Visibility.Collapsed;
            ComposerPanel.Visibility = Visibility.Collapsed;
            DropZone.Visibility = Visibility.Visible;
            ShowClip(ClipLibrary.Load(item.Entry));
        }
        catch (IOException ex)
        {
            Notify.Error(ex.Message);
        }
    }

    private void OnDeleteRecent(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RecentItem item) return;
        try
        {
            ClipLibrary.Delete(item.Entry);
        }
        catch (IOException ex)
        {
            Notify.Error(ex.Message);
        }
        LoadRecent();
    }
}
