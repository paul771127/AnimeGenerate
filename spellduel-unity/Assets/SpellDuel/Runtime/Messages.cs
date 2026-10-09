using System;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 所有網路訊息共用一個結構（JsonUtility 序列化），用 t 區分種類：
    ///   ping / pong  對時（以房主的時鐘為共同時間）
    ///   pose         我的手機在世界座標的位置與朝向（每秒 20 次）
    ///   shot         發射法術：起點、方向、速度、半徑、發射時間（共同時間）
    ///   hit / miss   攻擊方判定的結果（hit 帶傷害，被打的一方扣血）
    ///   hp           被打的一方回報最新血量
    ///   track        AR 追蹤狀態改變（中斷／恢復）
    ///   area         雙人畫場地模式：場地邊界（世界座標，pts）
    ///   seen         場地主的鏡頭看到對手站的位置（世界座標 p、拍攝時間 t0）→ 對齊樣本
    ///   align        對齊方回報對齊結果（ok、誤差 s、樣本數 id）
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

        public string ToJson() => JsonUtility.ToJson(this);
        public static Msg FromJson(string json) => JsonUtility.FromJson<Msg>(json);
    }
}
