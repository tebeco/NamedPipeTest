using System.Buffers;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Text;

namespace Server.IPC;

public class PipeServer
{
    public event EventHandler<DataEventArgs>? DataReceived;

    private static readonly Encoding _encoding = Encoding.UTF8;
    private readonly static byte[] NewLine = Encoding.UTF8.GetBytes(Environment.NewLine);

    private readonly NamedPipeServerStream _pipeServer;
    private readonly ILogger<ServerService> _logger;


    public PipeServer(string pipeName, ILogger<ServerService> logger)
    {
        _logger = logger;
        _pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 4, PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);

        _logger.LogInformation("Pipe Name: {pipeName}", pipeName);
    }


    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("Waiting for connecction...");
        await _pipeServer.WaitForConnectionAsync(ct);
        _logger.LogInformation("Connected...");

        _ = ReadData(ct);
    }

    public void Stop()
    {
        _pipeServer.Close();
    }

    private async Task ReadData(CancellationToken ct = default)
    {
        var pipeReader = PipeReader.Create(_pipeServer, new StreamPipeReaderOptions(leaveOpen: true));

        try
        {
            while (_pipeServer.IsConnected)
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
        var pipeWriter = PipeWriter.Create(_pipeServer, new StreamPipeWriterOptions(leaveOpen: true));

        try
        {
            if (_pipeServer.IsConnected)
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