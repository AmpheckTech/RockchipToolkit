using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Ampheck.RockchipToolkit.Server.Firmware;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server;

internal sealed class ToolkitChannel : IAsyncDisposable {
    private readonly IReadOnlyList<ToolkitAttachment> _attachments;
    private readonly string _root;
    private readonly ToolkitTransferLimits _limits;
    private readonly ILogger _logger;
    private readonly Channel<byte[]> _input =
        Channel.CreateBounded<byte[]>(
            new BoundedChannelOptions(256) {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    private FileStream? _attachmentStream;
    private long _attachmentLength;
    private long _expandedBytes;
    private int _attachmentIndex;
    private int _started;
    private long _sequence;
    private CancellationTokenSource? _operationStop;
    private int _disposed;

    public ToolkitChannel(
        uint channelId,
        uint requestId,
        ToolkitRequest request,
        ToolkitTransferLimits limits,
        ILogger logger) {
        ChannelId = channelId;
        RequestId = requestId;
        Request = request;
        _attachments = request.Attachments;
        _limits = limits;
        _logger = logger;
        _root = Path.Combine(Path.GetTempPath(), $"ampheck-channel-{channelId}-{Guid.NewGuid():N}");
    }

    public ToolkitRequest Request { get; private set; }
    public uint ChannelId { get; }
    public uint RequestId { get; }
    public bool IsReady => _attachmentIndex >= _attachments.Count;
    public string? DownloadPath { get; private set; }
    public ChannelReader<byte[]> Input => _input.Reader;

    public CancellationTokenSource StartOperation(CancellationToken serverToken) {
        if (Interlocked.Exchange(ref _started, 1) != 0) {
            throw new InvalidOperationException("The toolkit channel already started.");
        }

        Directory.CreateDirectory(_root);
        if (Request.Operation == ToolkitOperation.Read) {
            DownloadPath = Path.Combine(_root, "read.bin");
            Request = Request with { File = DownloadPath };
        } else if (Request.Operation == ToolkitOperation.Write && Request.Backup is not null) {
            DownloadPath = Path.Combine(_root, "backup.bin");
            Request = Request with { Backup = DownloadPath };
        }

        _operationStop = CancellationTokenSource.CreateLinkedTokenSource(serverToken);
        return _operationStop;
    }

    public ulong NextSequence() => checked((ulong)Interlocked.Increment(ref _sequence));

    public async ValueTask AppendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) {
        if (Request.Operation == ToolkitOperation.SerialTerminal &&
            IsReady) {
            await _input.Writer.WriteAsync(
                    data.ToArray(),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (IsReady || _attachmentIndex >= _attachments.Count) {
            throw new InvalidDataException("Unexpected toolkit attachment data.");
        }

        if (_attachmentStream is null) {
            Directory.CreateDirectory(_root);
            var attachment = _attachments[_attachmentIndex];
            var fileName = Path.GetFileName(attachment.FileName);
            var path = Path.Combine(_root, $"{_attachmentIndex}-{fileName}");
            _attachmentStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            _logger.LogInformation(
                "Attachment receiving: channel={ChannelId} role={Role} file={FileName} bytes={ByteCount}",
                ChannelId,
                attachment.Role,
                attachment.FileName,
                attachment.Length);
        }

        _attachmentLength = checked(_attachmentLength + data.Length);
        if (_attachmentLength > _attachments[_attachmentIndex].Length) {
            throw new InvalidDataException("The toolkit attachment exceeded its declared size.");
        }

        await _attachmentStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> CompleteAttachmentAsync(CancellationToken cancellationToken) {
        if (IsReady || _attachmentStream is null || _attachmentIndex >= _attachments.Count) {
            throw new InvalidDataException("Unexpected toolkit attachment boundary.");
        }

        var attachment = _attachments[_attachmentIndex];
        var path = _attachmentStream.Name;
        await _attachmentStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        await _attachmentStream.DisposeAsync().ConfigureAwait(false);
        _attachmentStream = null;
        if (_attachmentLength != attachment.Length) {
            throw new InvalidDataException("The toolkit attachment length does not match.");
        }

        _logger.LogInformation(
            "Attachment received: channel={ChannelId} role={Role} file={FileName} bytes={ByteCount}",
            ChannelId,
            attachment.Role,
            attachment.FileName,
            _attachmentLength);

        if (attachment.Kind == ToolkitAttachmentKind.ZipDirectory) {
            var directory = Path.Combine(_root, $"extract-{_attachmentIndex}");
            Directory.CreateDirectory(directory);
            var archivePath = path;
            _expandedBytes += await FirmwareArchive.ExtractAsync(
                    archivePath,
                    directory,
                    _limits,
                    _expandedBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            path = FirmwareArchive.FindRoot(directory);
            File.Delete(archivePath);
        }

        Request = attachment.Role switch {
            ToolkitAttachmentRole.Source => Request with { Source = path },
            ToolkitAttachmentRole.Loader when Request.Operation == ToolkitOperation.Loader =>
                Request with { File = path },
            ToolkitAttachmentRole.Loader => Request with { Loader = path },
            ToolkitAttachmentRole.Input => Request with { File = path },
            _ => throw new InvalidDataException($"Unknown attachment role '{attachment.Role}'.")
        };
        _attachmentIndex++;
        _attachmentLength = 0;
        return IsReady;
    }

    public void RequestClose() {
        _input.Writer.TryComplete();
    }

    public void Cancel() {
        RequestClose();
        _operationStop?.Cancel();
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        Exception? failure = null;
        try {
            _operationStop?.Dispose();
        } catch (Exception error) {
            failure = error;
        }

        _input.Writer.TryComplete();
        if (_attachmentStream is not null) {
            try {
                await _attachmentStream.DisposeAsync()
                    .ConfigureAwait(false);
            } catch (Exception error) {
                failure ??= error;
            } finally {
                _attachmentStream = null;
            }
        }

        try {
            if (Directory.Exists(_root)) {
                Directory.Delete(_root, recursive: true);
            }
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
        }

        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
