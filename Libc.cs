using System;
using System.Runtime.InteropServices;

namespace EasyShm;

/// <summary>
/// The libc entry points the POSIX side needs, with the constants of the running operating
/// system. Linux and macOS disagree on the <c>O_*</c> values, so those are resolved at runtime;
/// <c>PROT_*</c>, <c>MAP_SHARED</c> and the <c>errno</c> values are the same on both.
/// </summary>
internal static class Libc
{
    private const string MacLibrary = "libSystem";
    private const string LinuxLibrary = "libc";

    private const UnmanagedType Utf8 = UnmanagedType.LPUTF8Str;

    private static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    internal static readonly int Create = IsMacOS ? 0x200 : 0x40;
    internal static readonly int Exclusive = IsMacOS ? 0x800 : 0x80;
    internal const int ReadWrite = 0x2;

    /// <summary>C# has no octal literals, so <c>0666</c> spelled in decimal would be wrong.</summary>
    internal const int Mode0666 = 0b110_110_110;

    internal const int ProtRead = 0x1;
    internal const int ProtWrite = 0x2;
    internal const int MapShared = 0x1;

    /// <summary>
    /// What <c>mmap</c> returns on failure: <c>MAP_FAILED</c>, i.e. <c>(void*)-1</c>. It is
    /// <b>not</b> NULL, so testing for zero reports a failed mapping as a usable pointer.
    /// </summary>
    internal static readonly IntPtr MapFailed = new(-1);


    internal static int LastError => Marshal.GetLastWin32Error();

    internal static int Open(string name, int flags, int mode)
    {
        return IsMacOS
            ? Mac.shm_open(name, flags, mode)
            : Linux.shm_open(name, flags, mode);
    }

    internal static int Unlink(string name)
    {
        return IsMacOS
            ? Mac.shm_unlink(name)
            : Linux.shm_unlink(name);
    }

    internal static int Close(int fileDescriptor)
    {
        return IsMacOS
            ? Mac.close(fileDescriptor)
            : Linux.close(fileDescriptor);
    }

    internal static IntPtr Map(IntPtr address, long length, int protection, int flags, int fileDescriptor)
    {
        return IsMacOS
            ? Mac.mmap(address, (UIntPtr)length, protection, flags, fileDescriptor, 0)
            : Linux.mmap(address, (UIntPtr)length, protection, flags, fileDescriptor, 0);
    }

    internal static int Unmap(IntPtr address, long length)
    {
        return IsMacOS
            ? Mac.munmap(address, (UIntPtr)length)
            : Linux.munmap(address, (UIntPtr)length);
    }

    /// <summary>Linux keeps <c>ftruncate64</c> for objects beyond 2 GiB on 32-bit platforms.</summary>
    internal static int Truncate(int fileDescriptor, long length)
    {
        return IsMacOS
            ? Mac.ftruncate(fileDescriptor, length)
            : Linux.ftruncate64(fileDescriptor, length);
    }

    private static class Linux
    {
        [DllImport(LinuxLibrary, EntryPoint = "shm_open", SetLastError = true)]
        internal static extern int shm_open([MarshalAs(Utf8)] string name, int flags, int mode);

        [DllImport(LinuxLibrary, EntryPoint = "shm_unlink", SetLastError = true)]
        internal static extern int shm_unlink([MarshalAs(Utf8)] string name);

        [DllImport(LinuxLibrary, EntryPoint = "close", SetLastError = true)]
        internal static extern int close(int fileDescriptor);

        [DllImport(LinuxLibrary, EntryPoint = "mmap", SetLastError = true)]
        internal static extern IntPtr mmap(IntPtr address, UIntPtr length, int protection, int flags, int fileDescriptor, long offset);

        [DllImport(LinuxLibrary, EntryPoint = "munmap", SetLastError = true)]
        internal static extern int munmap(IntPtr address, UIntPtr length);

        [DllImport(LinuxLibrary, EntryPoint = "ftruncate64", SetLastError = true)]
        internal static extern int ftruncate64(int fileDescriptor, long length);
    }

    private static class Mac
    {
        [DllImport(MacLibrary, EntryPoint = "shm_open", SetLastError = true)]
        internal static extern int shm_open([MarshalAs(Utf8)] string name, int flags, int mode);

        [DllImport(MacLibrary, EntryPoint = "shm_unlink", SetLastError = true)]
        internal static extern int shm_unlink([MarshalAs(Utf8)] string name);

        [DllImport(MacLibrary, EntryPoint = "close", SetLastError = true)]
        internal static extern int close(int fileDescriptor);

        [DllImport(MacLibrary, EntryPoint = "mmap", SetLastError = true)]
        internal static extern IntPtr mmap(IntPtr address, UIntPtr length, int protection, int flags, int fileDescriptor, long offset);

        [DllImport(MacLibrary, EntryPoint = "munmap", SetLastError = true)]
        internal static extern int munmap(IntPtr address, UIntPtr length);

        [DllImport(MacLibrary, EntryPoint = "ftruncate", SetLastError = true)]
        internal static extern int ftruncate(int fileDescriptor, long length);
    }
}
