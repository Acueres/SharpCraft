using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;

using SharpCraft.MathUtilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;

namespace SharpCraft.Persistence;

public class ChunkPersistenceService(string saveName)
{
    private const int chunkSaveCapacity = 100;

    private readonly ConcurrentDictionary<Chunk, int> pending = [];

    private readonly Channel<ChunkWriteJob> writerChannel = Channel.CreateBounded<ChunkWriteJob>(
        new BoundedChannelOptions(chunkSaveCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    
    private CancellationTokenSource cts;
    private PeriodicTimer timer;
    private Task flushTask;
    private Task writerTask;

    public void Start(TimeSpan flushInterval)
    {
        if (cts != null) throw new InvalidOperationException("ChunkPersistenceService already started.");

        cts = new CancellationTokenSource();
        timer = new PeriodicTimer(flushInterval);

        writerTask = Task.Run(WriteChunksAsync);
        flushTask = Task.Run(() => FlushPendingAsync(cts.Token));
    }

    public async Task StopAsync()
    {
        if (cts == null) return;
        
        await cts.CancelAsync();
        timer.Dispose();
        
        if (flushTask != null)
        {
            try { await flushTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        
        await writerTask.ConfigureAwait(false);

        cts.Dispose();
        cts = null;
        timer = null;
        flushTask = null;
        writerTask = null;
    }
    
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
    
    public bool TryLoadChunk(Vec3<int> index, out Block[,,] buffer)
        => ChunkDiskStorage.TryLoadChunk(saveName, index, out buffer);

    public void AddToPending(Chunk chunk)
    {
        pending.AddOrUpdate(chunk, 1, (_, rev) => unchecked(rev + 1));
    }

    private async Task FlushPendingAsync(CancellationToken ct)
    {
        try
        {
            while (timer != null && await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await FlushOnceAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { } // normal shutdown
        finally
        {
            try { await FlushOnceAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (OperationCanceledException) { } // shutting down

            writerChannel.Writer.TryComplete();
        }
    }
    
    private async Task FlushOnceAsync(CancellationToken token)
    {
        var snapshot = pending.ToArray();

        foreach (var kvp in snapshot)
        {
            token.ThrowIfCancellationRequested();

            Chunk chunk = kvp.Key;

            if (!pending.TryRemove(kvp))
            {
                continue;
            }

            byte[] data;
            lock (chunk.SyncRoot)
            {
                chunk.RebuildPalette();
                var serializer = new ChunkSerializer(chunk);
                data = serializer.SerializeChunk();
            }

            var job = new ChunkWriteJob(chunk.Index, data);
            await writerChannel.Writer.WriteAsync(job, token).ConfigureAwait(false);
        }
    }

    private async Task WriteChunksAsync()
    {
        await foreach (var job in writerChannel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            ChunkDiskStorage.WriteChunkAtomic(saveName, job.Index, job.Data);
        }
    }

    private readonly record struct ChunkWriteJob(Vec3<int> Index, byte[] Data);
}