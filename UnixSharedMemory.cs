using System;

namespace EasyShm;

public sealed class UnixSharedMemory<T> : ISharedMemory<T>
    where T : unmanaged
{
    private readonly string _name;
    private readonly int _fd;
    private readonly IntPtr _ptr;
    private readonly int _count;
    private readonly bool _own;

    private static readonly int ElementSize = sizeof(T);

    private volatile bool _disposed;

    private UnixSharedMemory(int fd, IntPtr ptr, string name, int count, bool own)
    {
        _name = name;
        _count = count;

        _fd = fd;
        _ptr = ptr;
        _own = own;
    }

    public unsafe Span<T> Span
    {
        get
        {
            ThrowIfDisposed();
            return new(_ptr.ToPointer(), _count);
        }
    }


    public static UnixSharedMemory<T> CreateNew(string name, int count)
    {
        var bytes = ByteSize(count);

        var fd = Libc.Open(name, Libc.Create | Libc.Exclusive | Libc.ReadWrite, Libc.Mode0666);
        if (fd == -1)
            throw new InvalidOperationException($"shm_open failed with errno {Libc.LastError}.");

        var ret = Libc.Truncate(fd, bytes);
        if (ret == -1)
        {
            var errno = Libc.LastError;
            Libc.Close(fd);
            Libc.Unlink(name);
            throw new InvalidOperationException($"ftruncate failed with errno {errno}.");
        }

        var ptr = Libc.Map(IntPtr.Zero, bytes, Libc.ProtRead | Libc.ProtWrite, Libc.MapShared, fd);
        if (ptr == Libc.MapFailed)
        {
            var errno = Libc.LastError;
            Libc.Close(fd);
            Libc.Unlink(name);
            throw new InvalidOperationException($"mmap failed with errno {errno}.");
        }

        return new UnixSharedMemory<T>(fd, ptr, name, count, own: true);
    }

    public static UnixSharedMemory<T> OpenExisting(string name, int count)
    {
        var bytes = ByteSize(count);

        var fd = Libc.Open(name, Libc.ReadWrite, Libc.Mode0666);
        if (fd == -1)
            throw new InvalidOperationException($"shm_open failed with errno {Libc.LastError}.");

        var ptr = Libc.Map(IntPtr.Zero, bytes, Libc.ProtRead | Libc.ProtWrite, Libc.MapShared, fd);
        if (ptr == Libc.MapFailed)
        {
            var errno = Libc.LastError;
            Libc.Close(fd);
            throw new InvalidOperationException($"mmap failed with errno {errno}.");
        }

        return new UnixSharedMemory<T>(fd, ptr, name, count, own: false);
    }

    ~UnixSharedMemory() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;

        Libc.Unmap(_ptr, (long)_count * ElementSize);
        Libc.Close(_fd);

        if (_own)
            Libc.Unlink(_name);
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
            throw new ObjectDisposedException(nameof(UnixSharedMemory<>));
    }
}
