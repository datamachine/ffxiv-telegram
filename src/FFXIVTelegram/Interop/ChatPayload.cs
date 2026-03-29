namespace FFXIVTelegram.Interop;

using System.Runtime.InteropServices;
using System.Text;

[StructLayout(LayoutKind.Explicit)]
internal readonly struct ChatPayload : IDisposable
{
    [FieldOffset(0)]
    private readonly nint textPtr;

    [FieldOffset(8)]
    private readonly ulong unk1;

    [FieldOffset(16)]
    private readonly ulong textLen;

    [FieldOffset(24)]
    private readonly ulong unk2;

    public ChatPayload(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var bytes = Encoding.UTF8.GetBytes(text);
        this.textPtr = Marshal.AllocHGlobal(bytes.Length + 30);
        Marshal.Copy(bytes, 0, this.textPtr, bytes.Length);
        Marshal.WriteByte(this.textPtr + bytes.Length, 0);
        this.unk1 = 64;
        this.textLen = (ulong)(bytes.Length + 1);
        this.unk2 = 0;
    }

    public void Dispose()
    {
        Marshal.FreeHGlobal(this.textPtr);
    }
}
