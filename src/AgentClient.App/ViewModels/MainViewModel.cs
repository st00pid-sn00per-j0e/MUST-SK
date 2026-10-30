using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text;
using AgentClient.Core.Http;
using AgentClient.Core.Models;
using AgentClient.Core.WebSockets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AgentClient.App.ViewModels;

public sealed record WorkflowOption(string Id, string Name, string Description, string CsvSchema);

public partial class MainViewModel : ObservableObject
{
    private readonly AgentApiClient _api;
    private readonly UploadService _upload;
    private readonly ProgressStream _stream;
    private CancellationTokenSource? _cts;
    private DataTable? _outputTable;
    private string? _outputWorkflowId;

    public MainViewModel(AgentApiClient api, UploadService upload, ProgressStream stream)
    {
        _api = api;
        _upload = upload;
        _stream = stream;
        SelectedWorkflow = Workflows[0];
    }

    public ObservableCollection<WorkflowOption> Workflows { get; } =
    [
        new("inbox", "Inbox helper", "Group messages by intent and draft a short reply for each.", "CSV columns: message (or body), with optional id, sender, and subject."),
        new("attendance", "Attendance checker", "Flag unusual work-hour and leave patterns with a one-sentence explanation.", "CSV columns: employee, date, scheduled_hours, worked_hours, and leave_hours."),
        new("stalls", "Stall alerter", "Prioritize hiring candidates who have been waiting in a stage too long.", "CSV columns: candidate, stage, and last_moved_date (YYYY-MM-DD)."),
    ];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRunCommand))]
    [NotifyCanExecuteChangedFor(nameof(UseSampleCommand))]
    private WorkflowOption _selectedWorkflow = null!;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRunCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelRunCommand))]
    [NotifyCanExecuteChangedFor(nameof(UseSampleCommand))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadOutputCommand))]
    private bool _isCompleted;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRunCommand))]
    private string? _selectedFilePath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelRunCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadOutputCommand))]
    private string? _currentRunId;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRunCommand))]
    private int _thresholdDays = 7;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadOutputCommand))]
    private DataView? _outputRows;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private double _uploadProgress;

    public ObservableCollection<RunEvent> Events { get; } = new();

    [RelayCommand]
    private void PickFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Choose a CSV file",
        };
        if (dialog.ShowDialog() == true) SelectedFilePath = dialog.FileName;
    }

    [RelayCommand(CanExecute = nameof(CanUseSample))]
    private void UseSample()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", $"{SelectedWorkflow.Id}-sample.csv");
        if (!File.Exists(path))
        {
            ErrorMessage = $"Sample CSV was not found: {path}";
            return;
        }
        SelectedFilePath = path;
    }

    private bool CanUseSample() =>
        !IsRunning && File.Exists(Path.Combine(AppContext.BaseDirectory, "Samples", $"{SelectedWorkflow.Id}-sample.csv"));

    [RelayCommand(CanExecute = nameof(CanStartRun))]
    private async Task StartRunAsync()
    {
        if (SelectedFilePath is null) return;
        if (SelectedWorkflow.Id == "stalls" && ThresholdDays is < 1 or > 365)
        {
            ErrorMessage = "Stale-after days must be a whole number between 1 and 365.";
            return;
        }

        Events.Clear();
        OutputRows = null;
        _outputTable = null;
        _outputWorkflowId = SelectedWorkflow.Id;
        ErrorMessage = null;
        UploadProgress = 0;
        CurrentRunId = null;
        IsRunning = true;
        IsCompleted = false;
        Status = "Creating run";
        _cts = new CancellationTokenSource();

        try
        {
            Events.Add(new RunEvent(0, "info", $"{SelectedWorkflow.Name}: {SelectedWorkflow.Description}", DateTimeOffset.Now));
            var request = new CreateRunRequest(
                SelectedWorkflow.Id,
                SelectedWorkflow.Id == "stalls" ? ThresholdDays : null);
            var create = await _api.CreateRunAsync(request, _cts.Token);
            CurrentRunId = create.RunId;

            Status = "Uploading CSV";
            var size = new FileInfo(SelectedFilePath).Length;
            var progress = new Progress<long>(bytes => UploadProgress = size > 0 ? (double)bytes / size * 100 : 0);
            await _upload.UploadAsync(SelectedFilePath, create.UploadUrl, create.UploadMethod, progress, _cts.Token);

            Status = "Starting analysis";
            await _api.StartRunAsync(create.RunId, _cts.Token);
            Status = "Analyzing";

            await foreach (var runEvent in _stream.StreamAsync(create.RunId, 0, _cts.Token))
            {
                Events.Add(runEvent);
                Status = runEvent.Type;
                if (runEvent.Type == "failed")
                {
                    var snapshot = await _api.GetRunAsync(create.RunId, 0, _cts.Token);
                    ErrorMessage = snapshot.ErrorMessage ?? runEvent.Message;
                }
            }

            if (Status == "completed")
            {
                try
                {
                    var result = await _api.DownloadOutputAsync(create.RunId, "cleaned.csv", _cts.Token);
                    _outputTable = ParseTable(Encoding.UTF8.GetString(result));
                    OutputRows = _outputTable.DefaultView;
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"The run completed, but its preview could not be loaded: {ex.Message}";
                }
            }
        }
        catch (OperationCanceledException)
        {
            Status = "cancelled";
        }
        catch (AgentApiException ex)
        {
            ErrorMessage = $"{ex.StatusCode}: {ex.Body}";
            Status = "failed";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Status = "failed";
        }
        finally
        {
            IsRunning = false;
            IsCompleted = Status == "completed";
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanStartRun() =>
        !IsRunning && SelectedFilePath is not null && File.Exists(SelectedFilePath);

    [RelayCommand(CanExecute = nameof(CanCancelRun))]
    private async Task CancelRunAsync()
    {
        if (CurrentRunId is null) return;
        try
        {
            await _api.CancelRunAsync(CurrentRunId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not cancel the run: {ex.Message}";
        }
    }

    private bool CanCancelRun() => IsRunning && CurrentRunId is not null;

    [RelayCommand(CanExecute = nameof(CanDownloadOutput))]
    private async Task DownloadOutputAsync()
    {
        if (_outputTable is null) return;
        var dialog = new SaveFileDialog
        {
            FileName = $"{_outputWorkflowId}-results-{CurrentRunId}.csv",
            Filter = "CSV files (*.csv)|*.csv",
            Title = "Save reviewed CSV",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, SerializeTable(_outputTable), new UTF8Encoding(true));
            Status = "Output saved";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not save the output: {ex.Message}";
        }
    }

    private bool CanDownloadOutput() => IsCompleted && _outputTable is not null;

    private static DataTable ParseTable(string csv)
    {
        var records = ParseCsv(csv);
        if (records.Count == 0) throw new InvalidDataException("Output CSV is empty.");
        var table = new DataTable();
        foreach (var header in records[0]) table.Columns.Add(header, typeof(string));
        foreach (var record in records.Skip(1))
        {
            if (record.Length != table.Columns.Count) throw new InvalidDataException("Output CSV has inconsistent columns.");
            table.Rows.Add(record.Cast<object>().ToArray());
        }
        return table;
    }

    private static List<string[]> ParseCsv(string csv)
    {
        var records = new List<string[]>();
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < csv.Length && csv[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (character == '"') quoted = false;
                else field.Append(character);
            }
            else if (character == '"' && field.Length == 0) quoted = true;
            else if (character == ',')
            {
                record.Add(field.ToString());
                field.Clear();
            }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n') index++;
                record.Add(field.ToString());
                field.Clear();
                if (record.Any(value => value.Length > 0)) records.Add(record.ToArray());
                record.Clear();
            }
            else field.Append(character);
        }
        if (quoted) throw new InvalidDataException("Output CSV contains an unclosed quoted field.");
        record.Add(field.ToString());
        if (record.Any(value => value.Length > 0)) records.Add(record.ToArray());
        return records;
    }

    private static string SerializeTable(DataTable table)
    {
        static string Escape(string value)
        {
            if (value.IndexOfAny([',', '"', '\r', '\n']) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        var lines = new List<string>
        {
            string.Join(",", table.Columns.Cast<DataColumn>().Select(column => Escape(column.ColumnName))),
        };
        lines.AddRange(table.Rows.Cast<DataRow>().Select(row =>
            string.Join(",", row.ItemArray.Select(value => Escape(Convert.ToString(value) ?? "")))));
        return string.Join("\r\n", lines) + "\r\n";
    }
}
