using System;
using System.Runtime.InteropServices;

namespace EasyShm;

public static class SharedMemory
{
    /// <summary>
    /// Creates a region of <paramref name="count"/> elements, failing if <paramref name="name"/> is
    /// taken. The name lives as long as a handle is open, so disposing the creator retires it.
    /// </summary>
    public static ISharedMemory<T> CreateNew<T>(string name, int count)
        where T : unmanaged
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WinSharedMemory<T>.CreateNew(name, count);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return UnixSharedMemory<T>.CreateNew(name, count);
        else
            throw new PlatformNotSupportedException("Only Windows, macOS, and Linux platforms are supported.");
    }

    public static ISharedMemory<T> OpenExisting<T>(string name, int count)
        where T : unmanaged
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WinSharedMemory<T>.OpenExisting(name, count);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return UnixSharedMemory<T>.OpenExisting(name, count);
        else
            throw new PlatformNotSupportedException("Only Windows, macOS, and Linux platforms are supported.");
    }
}
