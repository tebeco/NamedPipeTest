using System.Buffers;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Text;

namespace Client.IPC;

public class PipeClient
{
    public event EventHandler<DataEventArgs>? DataReceived;

    private static readonly Encoding _encoding = Encoding.UTF8;
    private static byte[] NewLine = _encoding.GetBytes(Environment.NewLine);

    private readonly NamedPipeClientStream _pipeClient;
    private readonly string _pipeName;
    private readonly ILogger<ClientService> _logger;

    public PipeClient(string pipeName, ILogger<ClientService> logger)
    {
        _logger = logger;
        _pipeName = pipeName;

        _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);

        _logger.LogInformation("Pipe Name: {pipeName}", pipeName);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _logger.LogInformation("Waiting for connecction...");
            await _pipeClient.ConnectAsync(ct);
            _logger.LogInformation("Connected...");

            await Task.Run(() => ReadDataAsync(ct));
        }
    }

    public void Stop()
    {
        _pipeClient.Close();
    }

    private async Task ReadDataAsync(CancellationToken ct = default)
    {
        var pipeReader = PipeReader.Create(_pipeClient, new StreamPipeReaderOptions(leaveOpen: true));

        try
        {
            while (_pipeClient.IsConnected)
            {
                ReadResult readResult = await pipeReader.ReadAsync(ct);
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
                    pipeReader.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occured when reading from pipe");
        }
        finally
        {
            await pipeReader.CompleteAsync();
        }
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

    public async Task SendAsync(string title, string message, CancellationToken ct)
    {
        var pipeWriter = PipeWriter.Create(_pipeClient, new StreamPipeWriterOptions(leaveOpen: true));

        try
        {
            if (_pipeClient.IsConnected)
            {
                await pipeWriter.WriteAsync(_encoding.GetBytes(title + "," + message), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occured when writing to the pipe");
        }
    }
}