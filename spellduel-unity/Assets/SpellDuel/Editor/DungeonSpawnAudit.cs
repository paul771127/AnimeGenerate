#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SpellDuel.EditorTools
{
    // Runs the real Battle spawn loop; no scene, camera, networking or Unity frame is needed.
    public static class DungeonSpawnAudit
    {
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Call(Battle b, string method) => typeof(Battle).GetMethod(method, Private).Invoke(b, null);
        static Battle Make(int wave, int theme, int party, bool perturb)
        {
            var p = new Fighter("test", Skills.Classes["mage"], new string[0], false);
            var e = new Fighter("boss", Skills.Classes["boss"], new string[0], false);
            var b = new Battle(p, e) { dungeonParty = party, themeOverride = theme };
            b.arenaPoly = new List<Vector3> { new Vector3(-4,0,-3), new Vector3(5,0,-3), new Vector3(5,0,6), new Vector3(-4,0,6) };
            b.edgePoint = () => { throw new Exception("Dungeon used random edge point"); };
            b.bossPoint = () => { throw new Exception("Dungeon used random boss point"); };
            if (perturb)
            {
                p.head = new Vector3(3, 2, 4); p.forward = new Vector3(-1, 0, 0);
                var rng = (System.Random)typeof(Battle).GetField("rng", Private).GetValue(b);
                for (int i = 0; i < 77; i++) rng.NextDouble();
            }
            b.StartDungeonMap(); b.EnterNode(wave, wave % 10 == 0, 0f);
            return b;
        }
        static List<(int kind, float x, float z)> Roster(Battle b)
        {
            var result = new List<(int,float,float)>();
            for (int guard = 0; b.waveToSpawn > 0 && guard < 200; guard++)
            {
                b.now += 10f; Call(b, "UpdateEndless");
                foreach (var m in b.minions) result.Add((m.minionKind, m.head.x, m.head.z));
                b.minions.Clear();
            }
            Require(b.waveToSpawn == 0, "Spawn loop stalled");
            Require(result.Count == b.waveTotal, "Incorrect enemy count");
            return result;
        }
        [MenuItem("SpellDuel/Audit/Dungeon fixed spawns")]
        public static void Run()
        {
            int cases = 0;
            for (int wave = 1; wave <= 60; wave++)
            for (int party = 2; party <= 4; party++)
            {
                int theme = Battle.ThemeOf(wave);
                var a = Make(wave, theme, party, false);
                var b = Make(wave, theme, party, true);
                var ar = Roster(a); var br = Roster(b);
                Require(ar.Count == br.Count, "Roster count changed");
                for (int i = 0; i < ar.Count; i++)
                {
                    Require(ar[i].Equals(br[i]), "Roster/position changed with RNG or player pose");
                    int kind = ar[i].kind;
                    Require(theme != 0 || kind < 7, "Wrong volcano roster");
                    Require(theme != 1 || Battle.IsPlant(kind), "Wrong forest roster");
                    Require(theme != 2 || Battle.IsSea(kind), "Wrong sea roster");
                    bool rangedUnlocked = !(theme == 2 && wave <= 50 && (wave - 1) % 10 < 2);
                    Require(Battle.IsRanged(kind) == (i % 5 == 4 && rangedUnlocked), "Wrong fixed ranged slots");
                }
                // Re-entering the wave must reset the sequence, including when the same wave is reused.
                a.EnterNode(wave, wave % 10 == 0, 0f);
                var repeat = Roster(a);
                for (int i = 0; i < ar.Count; i++) Require(ar[i].Equals(repeat[i]), "Re-entry did not reset roster");
                if (wave % 10 == 0)
                {
                    Call(a, "SpawnBoss"); Call(b, "SpawnBoss");
                    Require(a.enemy.head.x == b.enemy.head.x && a.enemy.head.z == b.enemy.head.z, "Boss position changed");
                    Require(a.enemy.cls.id == Battle.BossClassOf(theme), "Wrong boss type");
                }
                cases++;
            }
            // A high-strength room with an explicit theme keeps that theme's roster/boss.
            var cross = Make(40, 0, 2, false); Roster(cross); Call(cross, "SpawnBoss");
            Require(cross.enemy.cls.id == "boss", "Cross-theme boss changed");
            // Story/endless encounters must still use the existing spawn delegates.
            var normal = Make(5, 0, 2, false); normal.dungeonParty = 0;
            int edges = 0; normal.edgePoint = () => { edges++; return Vector3.zero; };
            normal.now = 10; Call(normal, "UpdateEndless");
            Require(edges > 0, "Non-dungeon spawning was changed");
            Debug.Log($"Dungeon spawn audit passed: {cases} theme/wave/party cases, re-entry, RNG/player independence, boss and non-dungeon isolation.");
        }
    }
}
#endif
