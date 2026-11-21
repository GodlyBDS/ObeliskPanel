using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace ObeliskPanel;

public static class ConPty
{
    [StructLayout(LayoutKind.Sequential)] struct Coord { public short X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct StartupInfo
    {
        public int cb; public string? lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)] struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr lpAttributeList; }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInformation { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern int CreatePseudoConsole(Coord size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);
    [DllImport("kernel32.dll")] static extern void ClosePseudoConsole(IntPtr hPC);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);
    [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcessW(string? lpApplicationName, StringBuilder lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
        bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory, ref StartupInfoEx lpStartupInfo, out ProcessInformation lpProcessInformation);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    const uint ExtendedStartupInfoPresent = 0x00080000;
    static readonly IntPtr ProcThreadAttributePseudoConsole = (IntPtr)0x00020016;
    static readonly Regex Ansi = new(@"\x1B\[[0-?]*[ -/]*[@-~]|\x1B\][^\x07\x1B]*(\x07|\x1B\\)|\x1B[=>()][0-9A-Za-z]?|[\x00-\x08\x0B\x0C\x0E-\x1F]", RegexOptions.Compiled);

    public static async Task<int> Run(string exe, string args, string workDir, Action<string> onLine)
    {
        if (!CreatePipe(out var inRead, out var inWrite, IntPtr.Zero, 0) || !CreatePipe(out var outRead, out var outWrite, IntPtr.Zero, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var hr = CreatePseudoConsole(new Coord { X = 220, Y = 50 }, inRead, outWrite, 0, out var hPC);
        if (hr != 0) throw new Win32Exception(hr);
        inRead.Dispose();
        outWrite.Dispose();

        var size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        var attrList = Marshal.AllocHGlobal(size);
        ProcessInformation pi;
        try
        {
            if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref size) ||
                !UpdateProcThreadAttribute(attrList, 0, ProcThreadAttributePseudoConsole, hPC, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var si = new StartupInfoEx { lpAttributeList = attrList };
            si.StartupInfo.cb = Marshal.SizeOf<StartupInfoEx>();
            si.StartupInfo.dwFlags = 0x00000100;
            var cmd = new StringBuilder($"\"{exe}\" {args}");
            if (!CreateProcessW(null, cmd, IntPtr.Zero, IntPtr.Zero, false, ExtendedStartupInfoPresent, IntPtr.Zero, workDir, ref si, out pi))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch
        {
            ClosePseudoConsole(hPC);
            inWrite.Dispose(); outRead.Dispose();
            throw;
        }
        finally
        {
            DeleteProcThreadAttributeList(attrList);
            Marshal.FreeHGlobal(attrList);
        }
        CloseHandle(pi.hThread);

        var reader = Task.Run(() => Pump(outRead, onLine));
        using var proc = Process.GetProcessById(pi.dwProcessId);
        await proc.WaitForExitAsync();
        int code = 0;
        try { code = proc.ExitCode; } catch { }
        CloseHandle(pi.hProcess);
        await Task.Delay(300);
        ClosePseudoConsole(hPC);
        await Task.WhenAny(reader, Task.Delay(3000));
        inWrite.Dispose();
        outRead.Dispose();
        return code;
    }

    static void Pump(SafeFileHandle output, Action<string> onLine)
    {
        using var fs = new FileStream(output, FileAccess.Read, 1);
        var decoder = Encoding.UTF8.GetDecoder();
        var bytes = new byte[4096];
        var chars = new char[4096];
        var line = new StringBuilder();
        string last = "";
        void Flush()
        {
            var text = Ansi.Replace(line.ToString(), "").Trim();
            line.Clear();
            if (text.Length == 0 || text == last) return;
            last = text;
            onLine(text);
        }
        try
        {
            int n;
            while ((n = fs.Read(bytes, 0, bytes.Length)) > 0)
            {
                int c = decoder.GetChars(bytes, 0, n, chars, 0);
                for (int i = 0; i < c; i++)
                {
                    if (chars[i] == '\n' || chars[i] == '\r') Flush();
                    else line.Append(chars[i]);
                }
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        Flush();
    }
}
