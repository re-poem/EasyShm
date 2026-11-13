using System;
using System.IO.MemoryMappedFiles;

namespace EasyShm;

public sealed class WinSharedMemory<T> : ISharedMemory<T>
    where T : unmanaged
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _mmv;
    private readonly int _count;

    private static readonly int ElementSize = sizeof(T);

    private volatile bool _disposed;

    private WinSharedMemory(MemoryMappedFile mmf, MemoryMappedViewAccessor mmv, int count)
    {
        _mmf = mmf;
        _mmv = mmv;
        _count = count;
    }

    public unsafe Span<T> Span
    {
        get
        {
            ThrowIfDisposed();
            return new(_mmv.SafeMemoryMappedViewHandle.DangerousGetHandle().ToPointer(), _count);
        }
    }


    public static WinSharedMemory<T> CreateNew(string name, int count)
    {
        var bytes = ByteSize(count);

        var mmf = MemoryMappedFile.CreateNew(name, bytes, MemoryMappedFileAccess.ReadWrite);
        try
        {
            var mmv = mmf.CreateViewAccessor(0, bytes, MemoryMappedFileAccess.ReadWrite);
            return new WinSharedMemory<T>(mmf, mmv, count);
        }
        catch
        {
            mmf.Dispose();
            throw;
        }
    }

    public static WinSharedMemory<T> OpenExisting(string name, int count)
    {
        var bytes = ByteSize(count);

#pragma warning disable CA1416 // Validate platform compatibility
        var mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.ReadWrite);
#pragma warning restore CA1416
        try
        {
            var mmv = mmf.CreateViewAccessor(0, bytes, MemoryMappedFileAccess.ReadWrite);
            return new WinSharedMemory<T>(mmf, mmv, count);
        }
        catch
        {
            mmf.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            _mmv.Dispose();
        }
        finally
        {
            _mmf.Dispose();
        }
    }



    /// <summary>The region length in bytes, validating <paramref name="count"/> on the way.</summary>
    private static long ByteSize(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "The count must be positive.");

        return (long)count * ElementSize;
    }
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WinSharedMemory<>));
    }
}
