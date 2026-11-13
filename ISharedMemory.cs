using System;

namespace EasyShm;

public interface ISharedMemory<T> : IDisposable
    where T : unmanaged
{
    public Span<T> Span { get; }
}
