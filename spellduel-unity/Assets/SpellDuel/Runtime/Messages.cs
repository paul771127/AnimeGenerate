using System;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 所有網路訊息共用一個結構（JsonUtility 序列化），用 t 區分種類：
    ///   ping / pong  對時（以房主的時鐘為共同時間）
    ///   pose         我的手機在世界座標的位置與朝向（每秒 20 次）
    ///   shot         發射法術：起點、方向、速度、半徑、發射時間（共同時間）
    ///   hit / miss   被攻擊方判定的結果
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

        public string ToJson() => JsonUtility.ToJson(this);
        public static Msg FromJson(string json) => JsonUtility.FromJson<Msg>(json);
    }
}
