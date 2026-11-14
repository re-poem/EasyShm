using System;
using System.IO;

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
        name = NormalizeName(name);
        var bytes = ByteSize(count);

        var fd = Libc.Open(name, Libc.Create | Libc.Exclusive | Libc.ReadWrite, Libc.Mode0666);
        if (fd == -1)
            throw Failure(name, Libc.LastError, nameof(Libc.Open));

        var ret = Libc.Truncate(fd, bytes);
        if (ret == -1)
        {
            var errno = Libc.LastError;
            Libc.Close(fd);
            Libc.Unlink(name);
            throw Failure(name, errno, nameof(Libc.Truncate));
        }

        var ptr = Libc.Map(IntPtr.Zero, bytes, Libc.ProtRead | Libc.ProtWrite, Libc.MapShared, fd);
        if (ptr == Libc.MapFailed)
        {
            var errno = Libc.LastError;
            Libc.Close(fd);
            Libc.Unlink(name);
            throw Failure(name, errno, nameof(Libc.Map));
        }

        return new UnixSharedMemory<T>(fd, ptr, name, count, own: true);
    }

    public static UnixSharedMemory<T> OpenExisting(string name, int count)
    {
        name = NormalizeName(name);
        var bytes = ByteSize(count);

        var fd = Libc.Open(name, Libc.ReadWrite, Libc.Mode0666);
        if (fd == -1)
            throw Failure(name, Libc.LastError, nameof(Libc.Open));

        var ptr = Libc.Map(IntPtr.Zero, bytes, Libc.ProtRead | Libc.ProtWrite, Libc.MapShared, fd);
        if (ptr == Libc.MapFailed)
        {
            var errno = Libc.LastError;
            Libc.Close(fd);
            throw Failure(name, errno, nameof(Libc.Map));
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



    private static Exception Failure(string name, int errno, string call)
    {
        var message = $"{call} failed for shared memory '{name}' (errno {errno}).";

        if (errno == Libc.ErrorNameTooLong)
            return new PathTooLongException(message);

        return errno switch
        {
            Libc.ErrorNotFound => new FileNotFoundException(message),
            Libc.ErrorExists => new IOException(message),
            Libc.ErrorAccess => new UnauthorizedAccessException(message),
            Libc.ErrorInvalid => new ArgumentException(message, nameof(name)),
            Libc.ErrorNoMemory => new OutOfMemoryException(message),
            _ => new InvalidOperationException(message),
        };
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("The name must not be empty.", nameof(name));

        var normalized = name[0] == '/' ? name : "/" + name;

        if (normalized.IndexOf('/', 1) >= 0)
            throw new ArgumentException($"The name '{name}' may not contain a '/' after the leading one.", nameof(name));

        return normalized;
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
