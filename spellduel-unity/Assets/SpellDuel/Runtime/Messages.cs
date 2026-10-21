using System;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 所有網路訊息共用一個結構（JsonUtility 序列化），用 t 區分種類：
    ///   多人（2～4 人）：每則訊息帶發送者 pid，to 指定收件人（見 NetLink）；peers＝主機公告目前在房間的 pid（ks）
    ///   ping / pong  對時（以房主的時鐘為共同時間）
    ///   pose         我的手機在世界座標的位置與朝向（每秒 20 次）
    ///   shot         發射法術：起點、方向、速度、半徑、發射時間（共同時間）
    ///   hit / miss   攻擊方判定的結果（hit 帶傷害，被打的一方扣血）
    ///   hp           被打的一方回報最新血量
    ///   track        AR 追蹤狀態改變（中斷／恢復）
    ///   area         雙人畫場地模式：場地邊界（世界座標，pts）
    ///   seen         場地主的鏡頭看到對手站的位置（世界座標 p、拍攝時間 t0）→ 對齊樣本
    ///   align        對齊方回報對齊結果（ok、誤差 s、樣本數 id）
    ///   雙人職業對戰（SoloBattle 的連線模式）：
    ///   ready        準備好了：職業 k、技能 ks、進階職業 a
    ///   pet / petgone   召喚物出現（id、種類 k、位置 p、存在秒數 s）／消失
    ///   oath         守護誓言：我受到的傷害轉給聖騎士（to＝聖騎士，dmg）
    ///   cast2        放出法術：id、技能 k、起點 p、方向 d、時間 t0、ok＝反擊打回去的
    ///   hit2         我的法術／陷阱打到你：id（陷阱為負）、技能 k、傷害 dmg、位置 p
    ///   hp2          被打的一方回報血量 hp、ok＝反擊成功
    ///   trap2 / trapgone   設置／移除陷阱：id、技能 k、位置 p、s＝幾秒後生效
    ///   state        每秒 10 次：詠唱中的技能 k、蓄力進度 s、血量 hp、狀態旗標 e
    ///   觀戰（GameRoot.Watch.cs、SoloBattle.Watch.cs）：
    ///   join         加入的人收到 _hello 後告訴房主：ok＝觀眾（不算玩家人數）；玩家滿了房主回 _full
    ///   wply         房主→觀眾：玩家 id 的職業 k、技能 ks、進階職業 a
    ///   wstart       房主→觀眾：開打（或中途加入時正在打）：模式 e（0 對戰、1 合作 Boss、2 無盡、3 副本）、副本 k、難度 hp、場地 pts
    ///   wover / wend 房主→觀眾：結算（k＝勝負文字）／回到準備畫面
    /// 座標一律是世界座標（以標記圖為原點，單位公尺）。
    /// </summary>
    [Serializable]
    public class Msg
    {
        public string t;
        public int id;
        public Vector3 p;        // 位置
        public Quaternion r;     // 朝向
        public Vector3 d;        // 方向
        public float s;          // 速度 (m/s)
        public float rad;        // 法術半徑 (m)
        public int dmg;
        public int hp;
        public double t0;        // 共同時間（秒）
        public double c;         // ping：送出時的本機時間
        public double h;         // pong：房主收到時的時間
        public bool ok;          // track：AR 追蹤是否正常
        public Vector3[] pts;    // area：場地邊界
        public string k;         // 雙人對戰：技能 id／職業 id
        public string[] ks;      // 雙人對戰：裝備的技能
        public int e;            // 雙人對戰：狀態旗標（1 格擋、2 護盾、4 反擊）
        public int pid;          // 發送者編號（主機 0、加入的人 1～3）
        public int to;           // 0＝給所有人；n＝只給 pid n−1（主機轉送時看這個）
        public string a;         // ready：進階職業 id（沒轉職＝空）

        public string ToJson() => JsonUtility.ToJson(this);
        public static Msg FromJson(string json) => JsonUtility.FromJson<Msg>(json);
    }
}
