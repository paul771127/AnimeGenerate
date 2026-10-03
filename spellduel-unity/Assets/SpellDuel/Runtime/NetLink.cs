using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SpellDuel
{
    /// <summary>
    /// 區域網路雙人連線：TCP，一行一個 JSON 訊息。
    /// 一方「建立房間」（顯示自己的 IP），另一方輸入 IP「加入」。兩支手機需在同一個 Wi-Fi。
    /// 讀取在背景執行緒，收到的訊息放進佇列，由主執行緒在 Update 裡處理。
    /// </summary>
    public class NetLink : IDisposable
    {
        public const int Port = 7777;

        public bool IsHost { get; private set; }
        public bool Connected => client != null && client.Connected && !closed;
        public volatile string Status = "未連線";

        TcpListener listener;
        TcpClient client;
        NetworkStream stream;
        volatile bool closed;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly object sendLock = new object();

        public void Host()
        {
            IsHost = true;
            try
            {
                listener = new TcpListener(IPAddress.Any, Port);
                listener.Start();
                Status = $"等待對手加入… 我的 IP：{LocalIP()}";
                new Thread(() =>
                {
                    try { Attach(listener.AcceptTcpClient()); }
                    catch (Exception e) { if (!closed) Status = "建立房間失敗：" + e.Message; }
                }) { IsBackground = true }.Start();
            }
            catch (Exception e) { Status = "建立房間失敗：" + e.Message; }
        }

        public void Join(string ip)
        {
            IsHost = false;
            Status = $"連線到 {ip}…";
            new Thread(() =>
            {
                try
                {
                    var c = new TcpClient();
                    c.Connect(ip.Trim(), Port);
                    Attach(c);
                }
                catch (Exception e) { Status = "連線失敗：" + e.Message; }
            }) { IsBackground = true }.Start();
        }

        void Attach(TcpClient c)
        {
            c.NoDelay = true;
            client = c;
            stream = c.GetStream();
            closed = false;
            Status = "已連線";
            inbox.Enqueue("{\"t\":\"_open\"}");
            new Thread(ReadLoop) { IsBackground = true }.Start();
        }

        void ReadLoop()
        {
            try
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null) inbox.Enqueue(line);
                }
            }
            catch (Exception) { }
            if (!closed) Status = "對手已斷線";
            closed = true;
            inbox.Enqueue("{\"t\":\"_close\"}");
        }

        public void Send(string json)
        {
            if (stream == null || closed) return;
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            lock (sendLock)
            {
                try { stream.Write(bytes, 0, bytes.Length); }
                catch (Exception) { closed = true; Status = "對手已斷線"; }
            }
        }

        public bool TryReceive(out string line) => inbox.TryDequeue(out line);

        /// <summary>這支手機在區域網路的 IPv4 位址（給對手輸入）</summary>
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
            closed = true;
            try { stream?.Close(); } catch (Exception) { }
            try { client?.Close(); } catch (Exception) { }
            try { listener?.Stop(); } catch (Exception) { }
        }
    }
}
