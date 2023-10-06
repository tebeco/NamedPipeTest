using System.Buffers;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Text;

namespace Client.IPC;

public class PipeClient
{
    private static readonly Encoding _encoding = Encoding.UTF8;
    private static ReadOnlySpan<byte> NewLine => _encoding.GetBytes(Environment.NewLine);

    private NamedPipeClientStream _pipeClient;
    private PipeReader _pipeReader;
    private PipeWriter _pipeWriter;

    private readonly string _pipeName;

    private Task readTask;
    private bool threadRunning = false;
    private CancellationTokenSource _cts = new();

    private readonly ILogger<ClientService> _logger;

    public event EventHandler<DataEventArgs> DataReceived;

    public PipeClient(string pipeName, ILogger<ClientService> logger)
    {
        _logger = logger;
        _pipeName = pipeName;

        _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);

        _logger.LogInformation("Pipe Name: {pipeName}", pipeName);
    }

    public async Task Start()
    {
        _cts = new CancellationTokenSource();

        await Task.Run(async () =>
        {
            do
            {
                if (!threadRunning)
                {
                    if (_pipeClient.IsConnected)
                    {
                        await Task.Run(async () =>
                        {
                            readTask = Task.Run(async () => { await ReadData(); });

                            await readTask;
                        });
                    }
                    else
                    {
                        _pipeClient.Dispose();
                        _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);

                        _logger.LogInformation("Waiting for connecction...");
                        await _pipeClient.ConnectAsync(_cts.Token);
                        _logger.LogInformation("Connected...");
                    }
                }
            } while (!_cts.IsCancellationRequested);
        });
    }

    public void Stop()
    {
        _cts.Cancel();
        _pipeClient.Close();
    }

    private async Task ReadData(CancellationToken ct = default)
    {
        threadRunning = true;
        _pipeReader = PipeReader.Create(_pipeClient, new StreamPipeReaderOptions(leaveOpen: true));

        try
        {
            while (true)
            {
                if (_pipeClient.IsConnected)
                {
                    ReadResult readResult = await _pipeReader.ReadAsync(ct);
                    ReadOnlySequence<byte> buffer = readResult.Buffer;

                    try
                    {
                        if (readResult.IsCanceled)
                        {
                            break;
                        }

                        while (TryParseLine(ref buffer, out ReadOnlySequence<byte> line))
                        {
                            DataReceived?.Invoke(this, new DataEventArgs(_encoding.GetString(line)));
                        }

                        if (readResult.IsCompleted)
                        {
                            if (!buffer.IsEmpty)
                            {
                                _logger.LogError("Incomplete pipe read");
                            }

                            break;
                        }
                    }
                    finally
                    {
                        _pipeReader.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception occured when reading from pipe", ex);
        }
        finally
        {
            await _pipeReader.CompleteAsync();
        }

        threadRunning = false;
    }

    private static bool TryParseLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> message)
    {
        SequenceReader<byte> reader = new(buffer);

        if (reader.TryReadTo(out ReadOnlySequence<byte> data, NewLine))
        {
            buffer = buffer.Slice(reader.Position);
            message = data;
            return true;
        }

        message = default;
        return false;
    }

    public async Task Send(string title, string message)
    {
        _pipeWriter = PipeWriter.Create(_pipeClient, new StreamPipeWriterOptions(leaveOpen: true));

        try
        {
            if (_pipeClient.IsConnected)
            {
                await _pipeWriter.WriteAsync(_encoding.GetBytes(title + "," + message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception occured when writing to the pipe", ex);
        }
    }
}