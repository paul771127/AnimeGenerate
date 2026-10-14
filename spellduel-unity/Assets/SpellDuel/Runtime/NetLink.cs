using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SpellDuel
{
    /// <summary>
    /// 區域網路連線（2～4 人）：TCP，一行一個 JSON 訊息。
    /// 一方「建立房間」當主機（顯示自己的 IP），其他人輸入 IP「加入」（最多 3 人加入）。所有手機需在同一個 Wi-Fi。
    /// 星狀連線：加入的人只連主機；主機把某個人送來的訊息轉給其他人（每則訊息帶發送者編號 pid）。
    ///   pid：主機＝0，加入的人依序 1～3（主機在連上時用 _hello 告訴對方）。
    ///   to：0＝給所有人；n＝只給 pid n−1（主機看到 to＝1 就不轉送，to＝k＋1 只轉給 k）。
    /// 讀取在背景執行緒，收到的訊息放進佇列，由主執行緒在 Update 裡處理。
    /// 合成訊息：_open（連上，id＝對方 pid）、_close（斷線，id＝對方 pid）、_hello（主機告訴我的 pid）。
    /// </summary>
    public class NetLink : IDisposable
    {
        public const int Port = 7777;
        public const int MaxClients = 3;      // 加主機最多 4 人

        public bool IsHost { get; private set; }
        /// <summary>我的編號：主機 0，加入的人 1～3（還沒收到 _hello 時是 −1）</summary>
        public int MyPid { get; private set; } = -1;
        /// <summary>主機：至少有一個人連著；加入的人：連著主機</summary>
        public bool Connected { get { lock (conns) { foreach (var c in conns) if (!c.closed) return true; } return false; } }
        /// <summary>連著的其他人數（主機＝加入的人數；加入的人＝1，只知道主機）</summary>
        public int LinkCount { get { int n = 0; lock (conns) { foreach (var c in conns) if (!c.closed) n++; } return n; } }
        public volatile string Status = "未連線";

        class Conn
        {
            public int pid;
            public TcpClient client;
            public NetworkStream stream;
            public volatile bool closed;
            public readonly object sendLock = new object();
        }

        TcpListener listener;
        readonly List<Conn> conns = new List<Conn>();
        volatile bool disposed;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();

        public void Host()
        {
            IsHost = true; MyPid = 0;
            try
            {
                listener = new TcpListener(IPAddress.Any, Port);
                listener.Start();
                Status = $"等待隊友加入… 我的 IP：{LocalIP()}";
                new Thread(AcceptLoop) { IsBackground = true }.Start();
            }
            catch (Exception e) { Status = "建立房間失敗：" + e.Message; }
        }

        void AcceptLoop()
        {
            while (!disposed)
            {
                TcpClient c;
                try { c = listener.AcceptTcpClient(); }
                catch (Exception e) { if (!disposed) Status = "建立房間失敗：" + e.Message; return; }
                int pid = FreePid();
                if (pid < 0)
                {
                    // 滿了：告訴對方再關掉
                    try { var b = Encoding.UTF8.GetBytes("{\"t\":\"_full\"}\n"); c.GetStream().Write(b, 0, b.Length); c.Close(); } catch (Exception) { }
                    continue;
                }
                var conn = Attach(c, pid);
                SendRaw(conn, "{\"t\":\"_hello\",\"id\":" + pid + "}");
                Status = $"房間：{LinkCount + 1} 人（我的 IP：{LocalIP()}）";
            }
        }

        int FreePid()
        {
            lock (conns)
            {
                conns.RemoveAll(x => x.closed);
                if (conns.Count >= MaxClients) return -1;
                for (int p = 1; p <= MaxClients; p++) if (!conns.Exists(x => x.pid == p)) return p;
            }
            return -1;
        }

        public void Join(string ip)
        {
            IsHost = false; MyPid = -1;
            Status = $"連線到 {ip}…";
            new Thread(() =>
            {
                try
                {
                    var c = new TcpClient();
                    c.Connect(ip.Trim(), Port);
                    Attach(c, 0);
                }
                catch (Exception e) { Status = "連線失敗：" + e.Message; }
            }) { IsBackground = true }.Start();
        }

        Conn Attach(TcpClient c, int pid)
        {
            c.NoDelay = true;
            var conn = new Conn { pid = pid, client = c, stream = c.GetStream() };
            lock (conns) conns.Add(conn);
            if (!IsHost) Status = "已連線";
            inbox.Enqueue("{\"t\":\"_open\",\"id\":" + pid + "}");
            new Thread(() => ReadLoop(conn)) { IsBackground = true }.Start();
            return conn;
        }

        void ReadLoop(Conn conn)
        {
            try
            {
                using (var reader = new StreamReader(conn.stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!IsHost && line.StartsWith("{\"t\":\"_full\"")) { Status = $"房間已滿（最多 {MaxClients + 1} 人）"; conn.closed = true; break; }
                        if (!IsHost && line.StartsWith("{\"t\":\"_hello\""))
                        {
                            int i = line.IndexOf("\"id\":", StringComparison.Ordinal);
                            if (i >= 0 && int.TryParse(Digits(line, i + 5), out var me)) MyPid = me;
                        }
                        inbox.Enqueue(line);
                        if (IsHost) Relay(conn, line);
                    }
                }
            }
            catch (Exception) { }
            if (!conn.closed && !disposed) Status = IsHost ? $"有隊友斷線（房間剩 {LinkCount + 1} 人）" : "主機已斷線";
            conn.closed = true;
            inbox.Enqueue("{\"t\":\"_close\",\"id\":" + conn.pid + "}");
        }

        static string Digits(string s, int at)
        {
            int j = at;
            while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '-')) j++;
            return s.Substring(at, j - at);
        }

        /// <summary>訊息的 to 欄位（沒有＝0）</summary>
        static int ToOf(string line)
        {
            int i = line.IndexOf("\"to\":", StringComparison.Ordinal);
            return i >= 0 && int.TryParse(Digits(line, i + 5), out var v) ? v : 0;
        }

        /// <summary>主機：把加入者送來的訊息轉給其他加入者（給主機自己的不轉）</summary>
        void Relay(Conn from, string line)
        {
            int to = ToOf(line);
            if (to == 1) return;
            List<Conn> list;
            lock (conns) list = new List<Conn>(conns);
            foreach (var c in list)
            {
                if (c == from || c.closed) continue;
                if (to != 0 && c.pid != to - 1) continue;
                SendRaw(c, line);
            }
        }

        void SendRaw(Conn c, string json)
        {
            if (c == null || c.closed) return;
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            lock (c.sendLock)
            {
                try { c.stream.Write(bytes, 0, bytes.Length); }
                catch (Exception) { c.closed = true; }
            }
        }

        /// <summary>送出：主機＝給所有人（to 指定時只給那個人）；加入的人＝給主機（主機再轉送）</summary>
        public void Send(string json)
        {
            List<Conn> list;
            lock (conns) list = new List<Conn>(conns);
            int to = IsHost ? ToOf(json) : 0;
            foreach (var c in list)
            {
                if (IsHost && to != 0 && c.pid != to - 1) continue;
                SendRaw(c, json);
            }
        }

        /// <summary>主機：只送給某一個人</summary>
        public void SendTo(int pid, string json)
        {
            List<Conn> list;
            lock (conns) list = new List<Conn>(conns);
            foreach (var c in list) if (c.pid == pid) SendRaw(c, json);
        }

        public bool TryReceive(out string line) => inbox.TryDequeue(out line);

        /// <summary>這支手機在區域網路的 IPv4 位址（給隊友輸入）</summary>
        public static string LocalIP()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        var a = ua.Address;
                        if (a.AddressFamily != AddressFamily.InterNetwork) continue;
                        var s = a.ToString();
                        if (s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("172.")) return s;
                    }
                }
            }
            catch (Exception) { }
            return "（找不到 IP，請確認已連上 Wi-Fi）";
        }

        public void Dispose()
        {
            disposed = true;
            List<Conn> list;
            lock (conns) list = new List<Conn>(conns);
            foreach (var c in list)
            {
                c.closed = true;
                try { c.stream?.Close(); } catch (Exception) { }
                try { c.client?.Close(); } catch (Exception) { }
            }
            try { listener?.Stop(); } catch (Exception) { }
        }
    }
}
