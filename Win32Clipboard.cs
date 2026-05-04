using System.Runtime.InteropServices;
using System.Text;

namespace Math2Tex;

/// <summary>
/// Direct Win32 clipboard writer. Bypasses WPF's COM wrapper and attaches the
/// current thread's input queue to the foreground window's so we get priority
/// scheduling for OpenClipboard. Does NOT change which window is focused —
/// AttachThreadInput merges input queues but does not steal foreground.
/// </summary>
internal static class Win32Clipboard
{
    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint CF_UNICODETEXT = 13;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr hMem);

    public static bool TrySetText(string text)
    {
        var fg = GetForegroundWindow();
        var fgThread = fg == IntPtr.Zero ? 0u : GetWindowThreadProcessId(fg, out _);
        var myThread = GetCurrentThreadId();
        bool attached = false;
        try
        {
            if (fgThread != 0 && fgThread != myThread)
            {
                attached = AttachThreadInput(myThread, fgThread, true);
            }
            return TryWriteOnce(text);
        }
        finally
        {
            if (attached) AttachThreadInput(myThread, fgThread, false);
        }
    }

    private static bool TryWriteOnce(string text)
    {
        if (!OpenClipboard(IntPtr.Zero)) return false;

        IntPtr hMem = IntPtr.Zero;
        bool ownershipTransferred = false;
        try
        {
            EmptyClipboard();

            var bytes = Encoding.Unicode.GetBytes(text);
            var totalBytes = bytes.Length + 2; // null terminator (UTF-16)

            hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)totalBytes);
            if (hMem == IntPtr.Zero) return false;

            var pMem = GlobalLock(hMem);
            if (pMem == IntPtr.Zero) return false;
            try
            {
                Marshal.Copy(bytes, 0, pMem, bytes.Length);
                Marshal.WriteInt16(pMem, bytes.Length, 0);
            }
            finally
            {
                GlobalUnlock(hMem);
            }

            if (SetClipboardData(CF_UNICODETEXT, hMem) != IntPtr.Zero)
            {
                ownershipTransferred = true;
                return true;
            }
            return false;
        }
        finally
        {
            CloseClipboard();
            if (!ownershipTransferred && hMem != IntPtr.Zero) GlobalFree(hMem);
        }
    }
}
