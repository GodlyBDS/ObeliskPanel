using System.IO;
using System.Runtime.InteropServices;

namespace ObeliskPanel;

public static class IoStore
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate long OodleLZ_Decompress(byte[] compBuf, long compSize, byte[] rawBuf, long rawSize,
        int fuzzSafe, int checkCrc, int verbosity, IntPtr decBufBase, long decBufSize,
        IntPtr fpCallback, IntPtr callbackUserData, IntPtr decoderMemory, long decoderMemorySize, int threadPhase);

    static OodleLZ_Decompress? _decompress;
    static string? _loadedPath;

    public static string? FindOodle(string? configured)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "oo2core_9_win64.dll"));
        foreach (var c in candidates)
            if (File.Exists(c)) return c;
        return null;
    }

    static void Load(string dllPath)
    {
        if (_decompress != null && _loadedPath == dllPath) return;
        var lib = NativeLibrary.Load(dllPath);
        _decompress = Marshal.GetDelegateForFunctionPointer<OodleLZ_Decompress>(NativeLibrary.GetExport(lib, "OodleLZ_Decompress"));
        _loadedPath = dllPath;
    }

    public static byte[] Unpack(string utocPath, string oodleDll)
    {
        var toc = File.ReadAllBytes(utocPath);
        if (toc.Length < 144 || System.Text.Encoding.ASCII.GetString(toc, 0, 16) != "-==--==--==--==-")
            throw new InvalidDataException("No IoStore TOC: " + utocPath);

        uint U32(int o) => BitConverter.ToUInt32(toc, o);
        int headerSize = (int)U32(20), entryCount = (int)U32(24), blockCount = (int)U32(28), blockEntrySize = (int)U32(32);
        int methCount = (int)U32(36), methLen = (int)U32(40);
        byte flags = toc[20 + 36 + 8 + 16];
        if ((flags & 0x2) != 0) throw new InvalidDataException("Container is encrypted.");
        int seeds = (int)U32(20 + 36 + 8 + 16 + 4);
        int noHash = (int)U32(20 + 36 + 8 + 16 + 4 + 4 + 8);

        int pos = headerSize + entryCount * 12 + entryCount * 10 + seeds * 4 + noHash * 4;
        var blocks = new (long off, int csize, int usize, byte meth)[blockCount];
        long total = 0;
        for (int i = 0; i < blockCount; i++)
        {
            int b = pos + i * blockEntrySize;
            long off = 0; for (int k = 0; k < 5; k++) off |= (long)toc[b + k] << (8 * k);
            int cs = toc[b + 5] | toc[b + 6] << 8 | toc[b + 7] << 16;
            int us = toc[b + 8] | toc[b + 9] << 8 | toc[b + 10] << 16;
            blocks[i] = (off, cs, us, toc[b + 11]);
            total += us;
        }
        pos += blockCount * blockEntrySize;
        var methods = Enumerable.Range(0, methCount)
            .Select(i => System.Text.Encoding.ASCII.GetString(toc, pos + i * methLen, methLen).TrimEnd('\0')).ToArray();

        var ucas = File.ReadAllBytes(Path.ChangeExtension(utocPath, ".ucas"));
        var output = new byte[total];
        long w = 0;
        foreach (var (off, cs, us, meth) in blocks)
        {
            if (meth == 0)
            {
                Array.Copy(ucas, off, output, w, us);
            }
            else
            {
                if (!methods[meth - 1].Equals("Oodle", StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException("Unsupported compression: " + methods[meth - 1]);
                Load(oodleDll);
                var src = new byte[cs];
                Array.Copy(ucas, off, src, 0, cs);
                var dst = new byte[us];
                long n = _decompress!(src, cs, dst, us, 1, 0, 0, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, 3);
                if (n != us) throw new InvalidDataException($"Oodle failed at block {off} ({n}/{us}).");
                Array.Copy(dst, 0, output, w, us);
            }
            w += us;
        }
        return output;
    }
}
