
namespace ManagedObjectSize
{
    [Flags]
    public enum ObjectSizeOptions
    {
        Default = 0,
        DebugOutput = 1 << 3,
        UseRtHelpers = 1 << 4
    }
}