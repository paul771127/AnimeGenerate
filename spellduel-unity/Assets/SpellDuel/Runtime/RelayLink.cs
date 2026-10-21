using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 網路連線（不同地方的人也能玩，2～6 人）：Unity Relay 中繼＋Unity Transport。
    /// 房主「建立房間」→ 拿到 6 碼房間代碼；其他人輸入代碼「加入」。不用在同一個 Wi-Fi、不用輸入 IP。
    /// 規則和 NetLink 一樣：星狀連線（加入的人只連房主，房主轉送）、pid（房主 0、加入的人 1～5）、to 欄位、
    /// 合成訊息 _open／_close／_hello／_full。位置、狀態這類高頻訊息走不可靠通道（掉了就算了），其他走可靠通道。
    /// 需要：Unity Cloud 專案（BuildScript 從 CI 的 UNITY_CLOUD_PROJECT_ID 寫入）並開啟 Relay；玩家匿名登入。
    /// 全部在主執行緒：GameRoot 每幀呼叫 TryReceive 時順便更新連線。
    /// </summary>
    public class RelayLink : ILink
    {
        public const int MaxClients = NetLink.MaxClients;
        /// <summary>觀戰的人（不算玩家人數）：連線數上限＝玩家 5＋觀戰 4；玩家滿了由 GameRoot 拒絕（join）</summary>
        public const int MaxWatchers = 4, MaxConnections = MaxClients + MaxWatchers;

        public bool IsHost { get; private set; }
        public int MyPid { get; private set; } = -1;
        public string Status { get; private set; } = "未連線";
        /// <summary>房間代碼（房主建立後才有）</summary>
        public string JoinCode { get; private set; }
        public bool Busy { get; private set; }   // 正在建立／加入（等伺服器回應）

        NetworkDriver driver;
        NetworkPipeline reliable, unreliable;
        readonly List<NetworkConnection> conns = new List<NetworkConnection>();   // 房主：加入的人
        readonly Dictionary<NetworkConnection, int> pidOf = new Dictionary<NetworkConnection, int>();
        NetworkConnection server;                                                 // 加入的人：連到房主
        bool serverUp;
        readonly Queue<string> inbox = new Queue<string>();
        int lastTick = -1;
        bool disposed;

        public bool Connected => driver.IsCreated && (IsHost ? conns.Count > 0 : serverUp);
        public int LinkCount => IsHost ? conns.Count : (serverUp ? 1 : 0);

        // ---------------------------------------------------------------- 建立／加入
        static async System.Threading.Tasks.Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static string Explain(Exception e)
        {
            string m = e.Message ?? "";
            if (string.IsNullOrEmpty(Application.cloudProjectId)) return "這個版本沒有設定 Unity Cloud 專案（見 README「遠端連線」設定步驟）";
            if (m.Contains("join code") || m.Contains("Not Found") || m.Contains("404")) return "找不到這個房間代碼（打錯了，或房主已經離開）";
            if (m.Contains("Relay") && m.Contains("not enabled")) return "Unity Cloud 專案還沒開啟 Relay";
            return m.Length > 80 ? m.Substring(0, 80) : m;
        }

        public async void Host()
        {
            if (Busy || driver.IsCreated) return;
            Busy = true; IsHost = true; MyPid = 0; JoinCode = null;
            Status = "連線到 Unity 伺服器…";
            try
            {
                await SignIn();
                var alloc = await RelayService.Instance.CreateAllocationAsync(MaxConnections);
                JoinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                if (disposed) return;
                var data = new RelayServerData(alloc, "dtls");
                Create(ref data);
                if (driver.Bind(NetworkEndpoint.AnyIpv4) != 0 || driver.Listen() != 0) { Status = "建立房間失敗（無法連上中繼伺服器）"; Close(); return; }
                Status = $"房間代碼：{JoinCode}（等其他人加入）";
            }
            catch (Exception e) { Status = "建立房間失敗：" + Explain(e); Close(); }
            finally { Busy = false; }
        }

        public async void Join(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (Busy || driver.IsCreated || code.Length == 0) return;
            Busy = true; IsHost = false; MyPid = -1;
            Status = $"加入房間 {code}…";
            try
            {
                await SignIn();
                var ja = await RelayService.Instance.JoinAllocationAsync(code);
                if (disposed) return;
                var data = new RelayServerData(ja, "dtls");
                Create(ref data);
                if (driver.Bind(NetworkEndpoint.AnyIpv4) != 0) { Status = "加入失敗（無法連上中繼伺服器）"; Close(); return; }
                server = driver.Connect(data.Endpoint);
                Status = "連線到房主…";
            }
            catch (Exception e) { Status = "加入失敗：" + Explain(e); Close(); }
            finally { Busy = false; }
        }

        void Create(ref RelayServerData data)
        {
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref data);
            settings.WithReliableStageParameters(windowSize: 64);
            settings.WithFragmentationStageParameters(payloadCapacity: 32 * 1024);
            driver = NetworkDriver.Create(settings);
            reliable = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            unreliable = driver.CreatePipeline(typeof(FragmentationPipelineStage));
        }

        // ---------------------------------------------------------------- 每幀
        void Tick()
        {
            if (!driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();
            if (IsHost)
            {
                NetworkConnection c;
                while ((c = driver.Accept()) != default)
                {
                    int pid = FreePid();
                    if (pid < 0) { SendRaw(c, "{\"t\":\"_full\"}", true); driver.Disconnect(c); continue; }
                    conns.Add(c); pidOf[c] = pid;
                    SendRaw(c, "{\"t\":\"_hello\",\"id\":" + pid + "}", true);
                    inbox.Enqueue("{\"t\":\"_open\",\"id\":" + pid + "}");
                    Status = $"房間代碼：{JoinCode}（{conns.Count + 1} 人）";
                }
                for (int i = conns.Count - 1; i >= 0; i--)
                {
                    var cn = conns[i];
                    NetworkEvent.Type ev;
                    while ((ev = driver.PopEventForConnection(cn, out var reader)) != NetworkEvent.Type.Empty)
                    {
                        if (ev == NetworkEvent.Type.Data)
                        {
                            var line = Read(ref reader);
                            inbox.Enqueue(line);
                            Relay(cn, line);
                        }
                        else if (ev == NetworkEvent.Type.Disconnect)
                        {
                            int pid = pidOf.TryGetValue(cn, out var pp) ? pp : -1;
                            conns.RemoveAt(i); pidOf.Remove(cn);
                            inbox.Enqueue("{\"t\":\"_close\",\"id\":" + pid + "}");
                            Status = $"房間代碼：{JoinCode}（有人離開，剩 {conns.Count + 1} 人）";
                            break;
                        }
                    }
                }
            }
            else if (server.IsCreated)
            {
                NetworkEvent.Type ev;
                while ((ev = driver.PopEventForConnection(server, out var reader)) != NetworkEvent.Type.Empty)
                {
                    switch (ev)
                    {
                        case NetworkEvent.Type.Connect:
                            serverUp = true; Status = "已連線";
                            inbox.Enqueue("{\"t\":\"_open\",\"id\":0}");
                            break;
                        case NetworkEvent.Type.Data:
                        {
                            var line = Read(ref reader);
                            if (line.StartsWith("{\"t\":\"_full\"")) { Status = $"房間已滿（最多 {MaxClients + 1} 位玩家、{MaxWatchers} 位觀眾）"; Close(); return; }
                            if (line.StartsWith("{\"t\":\"_hello\""))
                            {
                                int i = line.IndexOf("\"id\":", StringComparison.Ordinal);
                                if (i >= 0 && int.TryParse(Digits(line, i + 5), out var me)) { MyPid = me; Status = $"已加入（我是 {me} 號）"; }
                            }
                            inbox.Enqueue(line);
                            break;
                        }
                        case NetworkEvent.Type.Disconnect:
                            bool was = serverUp;
                            serverUp = false; server = default;
                            Status = was ? "房主已斷線" : "連不上房主（代碼過期或房間已滿）";
                            inbox.Enqueue("{\"t\":\"_close\",\"id\":0}");
                            return;
                    }
                }
            }
        }

        int FreePid()
        {
            if (conns.Count >= MaxConnections) return -1;
            for (int p = 1; p <= MaxConnections; p++) if (!pidOf.ContainsValue(p)) return p;
            return -1;
        }

        static string Read(ref DataStreamReader reader)
        {
            using (var bytes = new NativeArray<byte>(reader.Length, Allocator.Temp))
            {
                reader.ReadBytes(bytes);
                return Encoding.UTF8.GetString(bytes.ToArray());
            }
        }

        static string Digits(string s, int at)
        {
            int j = at;
            while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '-')) j++;
            return s.Substring(at, j - at);
        }

        static int ToOf(string line)
        {
            int i = line.IndexOf("\"to\":", StringComparison.Ordinal);
            return i >= 0 && int.TryParse(Digits(line, i + 5), out var v) ? v : 0;
        }

        /// <summary>高頻訊息（位置、狀態、Boss／小怪位置）：掉了下一則就補上，不用重送</summary>
        static bool Lossy(string json) =>
            json.StartsWith("{\"t\":\"pose\"") || json.StartsWith("{\"t\":\"state\"") || json.StartsWith("{\"t\":\"boss\"") || json.StartsWith("{\"t\":\"mins\"");

        void SendRaw(NetworkConnection c, string json, bool forceReliable = false)
        {
            if (!driver.IsCreated || !c.IsCreated) return;
            var pipe = !forceReliable && Lossy(json) ? unreliable : reliable;
            var bytes = Encoding.UTF8.GetBytes(json);
            if (driver.BeginSend(pipe, c, out var w) != 0) return;
            using (var na = new NativeArray<byte>(bytes, Allocator.Temp))
            {
                if (!w.WriteBytes(na)) { driver.AbortSend(w); return; }
            }
            driver.EndSend(w);
        }

        /// <summary>房主：把加入者送來的訊息轉給其他加入者（給房主自己的不轉）</summary>
        void Relay(NetworkConnection from, string line)
        {
            int to = ToOf(line);
            if (to == 1) return;
            foreach (var c in conns)
            {
                if (c == from) continue;
                if (to != 0 && (!pidOf.TryGetValue(c, out var p) || p != to - 1)) continue;
                SendRaw(c, line);
            }
        }

        // ---------------------------------------------------------------- ILink
        public void Send(string json)
        {
            if (IsHost)
            {
                int to = ToOf(json);
                foreach (var c in conns) { if (to != 0 && pidOf[c] != to - 1) continue; SendRaw(c, json); }
            }
            else if (serverUp) SendRaw(server, json);
        }

        public void SendTo(int pid, string json)
        {
            if (IsHost) { foreach (var c in conns) if (pidOf[c] == pid) SendRaw(c, json); }
            else if (serverUp && pid == 0) SendRaw(server, json);
        }

        public bool TryReceive(out string line)
        {
            if (lastTick != Time.frameCount) { lastTick = Time.frameCount; Tick(); }
            if (inbox.Count > 0) { line = inbox.Dequeue(); return true; }
            line = null; return false;
        }

        void Close()
        {
            if (driver.IsCreated)
            {
                foreach (var c in conns) driver.Disconnect(c);
                if (server.IsCreated) driver.Disconnect(server);
                driver.ScheduleUpdate().Complete();
                driver.Dispose();
            }
            driver = default;
            conns.Clear(); pidOf.Clear(); server = default; serverUp = false;
        }

        public void Dispose() { disposed = true; Close(); }
    }
}
