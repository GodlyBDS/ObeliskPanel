using System.IO;
using System.Net.Sockets;
using System.Text;

namespace ObeliskPanel;

public static class RconClient
{
    const int Auth = 3, Exec = 2;

    public static async Task<string> Send(string host, int port, string password, string command)
    {
        using var tcp = new TcpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await tcp.ConnectAsync(host, port, cts.Token);
        var s = tcp.GetStream();

        await Write(s, 1, Auth, password);
        var (id, _, _) = await Read(s);
        if (id == -1) (id, _, _) = await Read(s);
        if (id == -1) throw new UnauthorizedAccessException("Wrong RCON password.");

        await Write(s, 2, Exec, command);
        var (_, _, body) = await Read(s);
        return string.IsNullOrWhiteSpace(body) ? "(no response)" : body.Trim();
    }

    static async Task Write(Stream s, int id, int type, string body)
    {
        var b = Encoding.UTF8.GetBytes(body);
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(4 + 4 + b.Length + 2);
        w.Write(id);
        w.Write(type);
        w.Write(b);
        w.Write((short)0);
        await s.WriteAsync(ms.ToArray());
    }

    static async Task<(int id, int type, string body)> Read(Stream s)
    {
        var size = BitConverter.ToInt32(await ReadExact(s, 4));
        var data = await ReadExact(s, size);
        return (BitConverter.ToInt32(data, 0), BitConverter.ToInt32(data, 4),
                Encoding.UTF8.GetString(data, 8, Math.Max(0, size - 10)));
    }

    static async Task<byte[]> ReadExact(Stream s, int n)
    {
        var buf = new byte[n];
        int read = 0;
        while (read < n)
        {
            int r = await s.ReadAsync(buf.AsMemory(read, n - read));
            if (r == 0) throw new IOException("Connection closed.");
            read += r;
        }
        return buf;
    }
}
