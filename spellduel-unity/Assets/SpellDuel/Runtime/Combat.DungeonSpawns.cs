using UnityEngine;

namespace SpellDuel
{
    public partial class Battle
    {
        // Only dungeon encounters use this layout. AI, damage and rewards retain their own rules.
        // The same theme/wave/party size always has the same roster and spawn-slot sequence.
        bool FixedDungeonSpawns => dungeonParty > 0;
        int dungeonSpawnOrdinal;

        int DungeonMinionKind(int ordinal)
        {
            if (Gallery) return GalleryRoster(ordinal);
            // Every fifth entry is ranged. Select from the wave's unlocked theme roster
            // in a stable order, without consuming the combat RNG (hits/AI cannot alter it).
            bool ranged = ordinal % 5 == 4;
            int theme = Theme;
            int local = (wave - 1) % 10 + 1;
            bool firstCycle = wave <= 10 * ThemeCount;
            bool Eligible(int k) => IsRanged(k) == ranged && (theme == 0
                ? k < 7 && wave >= UnlockWave[k]
                : theme >= ArenaTheme
                    ? System.Array.IndexOf(theme == ArenaTheme ? ArenaKinds : CanyonKinds, k) >= 0
                        && (!firstCycle || local >= LateUnlock(k))
                    : (theme == 1 ? IsPlant(k) : IsSea(k))
                        && (!firstCycle || local >= ThemeUnlock[k]));
            int count = 0;
            for (int k = 0; k < ThemeUnlock.Length; k++) if (Eligible(k)) count++;
            if (count == 0) return theme == 1 ? 10 : theme == 2 ? 13 : 0;
            int pick = (wave + (ranged ? ordinal / 5 : ordinal - ordinal / 5)) % count;
            for (int k = 0; k < ThemeUnlock.Length; k++)
                if (Eligible(k) && pick-- == 0) return k;
            return 0;
        }

        Vector3 DungeonSpawnPoint(int ordinal)
        {
            // 32 fixed slots around the scanned arena, anchored to its polygon rather
            // than the player/camera. Later groups reuse slots in exactly the same order.
            int slot = (wave * 3 + ordinal * 5) % 32;
            float u = (slot + 0.5f) / 32f;
            if (arenaPoly == null || arenaPoly.Count < 3)
            {
                float angle = u * Mathf.PI * 2f;
                return new Vector3(Mathf.Cos(angle) * 6f, 0f, Mathf.Sin(angle) * 6f);
            }
            Vector3 center = Vector3.zero;
            float perimeter = 0f;
            for (int i = 0; i < arenaPoly.Count; i++)
            {
                center += arenaPoly[i];
                perimeter += FlatDistance(arenaPoly[i], arenaPoly[(i + 1) % arenaPoly.Count]);
            }
            center /= arenaPoly.Count;
            float remaining = u * perimeter;
            for (int i = 0; i < arenaPoly.Count; i++)
            {
                var a = arenaPoly[i]; var b = arenaPoly[(i + 1) % arenaPoly.Count];
                float length = FlatDistance(a, b);
                if (remaining > length) { remaining -= length; continue; }
                var p = Vector3.Lerp(a, b, length > 0.0001f ? remaining / length : 0f);
                var outward = p - center; outward.y = 0f;
                p += outward.normalized * 1.15f;
                p.y = 0f;
                return p;
            }
            center.y = 0f;
            return center;
        }
    }
}
