using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using WrightLogs.Models;
using WrightLogs.Services;

namespace WrightLogs.ViewModels;

public partial class UsageViewModel : ViewModelBase
{
    // Fixed palette so a metric keeps the same color regardless of which
    // other metrics are toggled on/off (avoids colors reshuffling on filter).
    private static readonly SKColor[] Palette =
    {
        new(0x36, 0xA2, 0xEB), new(0xFF, 0x63, 0x84), new(0x4B, 0xC0, 0xC0),
        new(0xFF, 0x9F, 0x40), new(0x93, 0x66, 0xCC), new(0xFF, 0xCD, 0x56),
        new(0xC9, 0xCB, 0xCF), new(0x2E, 0x7D, 0x32), new(0xD3, 0x2F, 0x2F),
        new(0x00, 0x96, 0x88),
    };

    private static readonly TimeSpan UsageDetailWindow = TimeSpan.FromMinutes(10);

    private readonly UsageFileParser _parser = new();
    private readonly UsageDetailFileParser _detailParser = new();
    private readonly Dictionary<string, SKColor> _metricColors = new();

    private IReadOnlyList<UsageSample> _samples = Array.Empty<UsageSample>();
    private string? _anyMemberPath;
    private bool _loadEntireFamily;
    private string? _usageDirectory;
    private string? _usageBaseName;

    public ObservableCollection<UsageMetricToggle> Metrics { get; } = new();

    public ObservableCollection<UsageDetailBlock> UsageDetailBlocks { get; } = new();

    /// <summary>Marks the point in time the last scan was centered on.</summary>
    public RectangularSection SelectionSection { get; } = new()
    {
        Stroke = new SolidColorPaint(new SKColor(0, 120, 215)) { StrokeThickness = 2 },
        IsVisible = false,
    };

    public IChartElement[] Sections { get; }

    public UsageViewModel()
    {
        Sections = new IChartElement[] { SelectionSection };
    }

    [ObservableProperty]
    private ISeries[] _series = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _xAxes = { new Axis() };

    [ObservableProperty]
    private string? _openFilePath;

    [ObservableProperty]
    private string _statusText = "No file open";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _hoveredMetricName;

    [ObservableProperty]
    private bool _isScanningUsageDetails;

    [ObservableProperty]
    private string _usageDetailStatusText = "No scan run yet.";

    [ObservableProperty]
    private bool _isAwaitingPointSelection;

    [ObservableProperty]
    private string _scanPromptText = "Click \"Scan Usage Details\", then click a point on the chart.";

    /// <summary>Which tab is active in the view (0 = Chart View, 1 = Usage Detail).</summary>
    [ObservableProperty]
    private int _activeTabIndex;

    partial void OnHoveredMetricNameChanged(string? value) => RebuildSeries();

    /// <summary>Arms point-selection mode; the next chart click triggers the actual scan.</summary>
    [RelayCommand]
    private void ArmScan()
    {
        if (_usageDirectory is null || _usageBaseName is null)
        {
            return;
        }

        IsAwaitingPointSelection = true;
        ScanPromptText = "Click a point on the chart to search ±10 minutes around it.";
    }

    /// <summary>Called by the view once the user clicks the chart while point-selection is armed.</summary>
    public async Task PointSelectedAsync(DateTime timestamp)
    {
        if (!IsAwaitingPointSelection || _usageDirectory is null || _usageBaseName is null)
        {
            return;
        }

        IsAwaitingPointSelection = false;
        ScanPromptText = "Click \"Scan Usage Details\", then click a point on the chart.";
        SelectionSection.Xi = timestamp.Ticks;
        SelectionSection.Xj = timestamp.Ticks;
        SelectionSection.IsVisible = true;

        var start = timestamp - UsageDetailWindow;
        var end = timestamp + UsageDetailWindow;

        // Switch tabs immediately so it's obvious the tool is working, before the (awaited) scan runs.
        ActiveTabIndex = 1;
        IsScanningUsageDetails = true;
        UsageDetailStatusText = "Searching...";
        UsageDetailBlocks.Clear();
        try
        {
            var blocks = await _detailParser
                .FindBlocksInRangeAsync(_usageDirectory, _usageBaseName, start, end)
                .ConfigureAwait(true);

            foreach (var block in blocks)
            {
                UsageDetailBlocks.Add(block);
            }

            UsageDetailStatusText = blocks.Count == 0
                ? $"No usage detail entries found within 10 minutes of {timestamp:yyyy-MM-dd HH:mm:ss}."
                : $"{blocks.Count:N0} entries found within 10 minutes of {timestamp:yyyy-MM-dd HH:mm:ss}.";
        }
        finally
        {
            IsScanningUsageDetails = false;
        }
    }

    /// <summary>Loads the given file plus its whole rollover family, aggregated into one series.</summary>
    public async Task OpenFamilyAsync(string anyMemberPath)
    {
        _anyMemberPath = anyMemberPath;
        _loadEntireFamily = true;
        OpenFilePath = anyMemberPath;
        await LoadAsync();
    }

    /// <summary>Loads exactly this file, ignoring any rollover siblings.</summary>
    public async Task OpenSingleFileAsync(string path)
    {
        _anyMemberPath = path;
        _loadEntireFamily = false;
        OpenFilePath = path;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_anyMemberPath is not null)
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        if (_anyMemberPath is null)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Loading...";
        IsAwaitingPointSelection = false;
        SelectionSection.IsVisible = false;
        try
        {
            _usageDirectory = Path.GetDirectoryName(_anyMemberPath);
            _usageBaseName = _parser.ResolveFamily(_anyMemberPath).BaseName;

            var result = _loadEntireFamily
                ? await _parser.ParseFamilyAsync(_anyMemberPath).ConfigureAwait(true)
                : await _parser.ParseSingleFileAsync(_anyMemberPath).ConfigureAwait(true);
            _samples = result.Samples;

            // Preserve which metrics the user had unchecked across a refresh/reload.
            var previouslyUnchecked = Metrics.Where(m => !m.IsChecked).Select(m => m.Name).ToHashSet();
            Metrics.Clear();
            foreach (var name in result.MetricNames)
            {
                var color = GetColorFor(name);
                var brush = new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
                var toggle = new UsageMetricToggle(name, brush) { IsChecked = !previouslyUnchecked.Contains(name) };
                toggle.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(UsageMetricToggle.IsChecked))
                    {
                        RebuildSeries();
                    }
                };
                Metrics.Add(toggle);
            }

            RebuildSeries();
            StatusText = $"{_samples.Count:N0} samples from {result.SourceFiles.Count} file(s)";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private SKColor GetColorFor(string metricName)
    {
        if (!_metricColors.TryGetValue(metricName, out var color))
        {
            color = Palette[_metricColors.Count % Palette.Length];
            _metricColors[metricName] = color;
        }

        return color;
    }

    private void RebuildSeries()
    {
        Series = Metrics
            .Where(m => m.IsChecked)
            .Select(m =>
            {
                var isHovered = HoveredMetricName == m.Name;
                var isDimmed = HoveredMetricName is not null && !isHovered;
                var color = GetColorFor(m.Name);
                if (isDimmed)
                {
                    color = color.WithAlpha(60);
                }

                return (ISeries)new LineSeries<DateTimePoint>
                {
                    Name = m.Name,
                    Values = _samples
                        .Select(s => new DateTimePoint(s.When, s.Metrics.TryGetValue(m.Name, out var v) ? v : null))
                        .ToArray(),
                    GeometrySize = 0,
                    LineSmoothness = 0,
                    Stroke = new SolidColorPaint(color) { StrokeThickness = isHovered ? 4 : 2 },
                    Fill = null,
                };
            })
            .ToArray();

        XAxes = new[]
        {
            new Axis
            {
                Labeler = value => new DateTime((long)value).ToString("MM/dd HH:mm"),
                LabelsRotation = 15,
            },
        };
    }
}
