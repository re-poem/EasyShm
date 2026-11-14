# EasyShm

A C# named shared memory library built on MemoryMappedFile (Windows) and libc (macOS and Linux).

## Usage

```csharp
using ISharedMemory<float> shm = 
	SharedMemory<float>.CreateOrOpen("MySharedMemory", 1000);
var span = shm.Span;
// Use the span...
```