using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public partial class MinionRig
    {
        // MinionLook attachments only: no joint moves, new animation bones or collision changes.
        static readonly Dictionary<string, Mesh> swiftMeshes = new Dictionary<string, Mesh>();
        internal bool SwiftArtAlive => !dead;
        internal Transform SwiftArtAnchor => spine != null ? spine : body;
        static Mesh SwiftMesh(string key, Action<PMesh> build)
        {
            if (!swiftMeshes.TryGetValue(key, out var mesh) || !mesh)
            {
                var b = new PMesh(1); build(b); mesh = b.Build("Swift " + key, true, true);
                RareSmooth(mesh); swiftMeshes[key] = mesh;
            }
            return mesh;
        }
        Transform SwiftPart(int id, Transform parent, string name, Color colour, Action<PMesh> build, bool luminous = false)
        {
            var t = Part(parent, SwiftMesh(id + "/" + name, build), Vector3.zero, Vector3.one, Vector3.zero, colour, .32f);
            t.name = "Swift " + name;
            if (luminous) t.GetComponent<Renderer>().sharedMaterial = glowMat;
            return t;
        }
        // Render children can change silhouette; their parent Pivot is deliberately untouched.
        static void SwiftScale(Transform joint, Vector3 scale, Predicate<MeshFilter> select = null)
        {
            if (!joint) return;
            foreach (Transform t in joint)
            {
                var f = t.GetComponent<MeshFilter>();
                if (f && (select == null || select(f))) t.localScale = Vector3.Scale(t.localScale, scale);
            }
        }
        static void SwiftRibbon(PMesh b, Vector3 a, Vector3 mid, Vector3 end, float width)
        {
            var side = Vector3.right * width;
            var p = new[] { a - side, a + side, mid + side * .6f, end, mid - side * .6f };
            for (int i = 1; i < 4; i++) { b.Tri(p[0], p[i], p[i + 1], Color.white); b.Tri(p[0], p[i + 1], p[i], Color.white); }
        }
        static void SwiftEar(PMesh b, float s, Vector3 at, float height)
        {
            var a = at + R(-.065f, 0, 0); var c = at + R(.065f, 0, 0);
            var tip = at + R(s * .045f, height, -.04f); var back = at + R(0, .04f, -.07f);
            b.Tri(a, c, tip, Color.white); b.Tri(c, back, tip, Color.white);
            b.Tri(back, a, tip, Color.white); b.Tri(a, back, c, Color.white);
        }
        internal Renderer[] AddSwiftArt(int id)
        {
            int first = rockList.Count;
            // The bespoke harness / scarf replaces the generic chapter collar. Keep its
            // original transform for callers, but avoid drawing duplicate accessories.
            if (dressRoot) dressRoot.gameObject.SetActive(false);
            // Some old eye/core parts used Unity's 768-triangle sphere. Same silhouette,
            // shared 80-triangle replacement frees the budget for identifiable equipment.
            foreach (var f in GetComponentsInChildren<MeshFilter>())
                if (f.sharedMesh && f.sharedMesh.name == "Sphere")
                    f.sharedMesh = SwiftMesh("small rounded glow", b => b.Lathe(new[] { -.5f, 0f, -.35f, .35f, 0f, .5f, .35f, .35f, .5f, 0f }, 8, null));
            while (rockGlows.Count < rockList.Count) rockGlows.Add(.25f);
            Color gold = new Color(1f, .73f, .18f), cream = new Color(1f, .92f, .65f), teal = new Color(.17f, .75f, .63f);
            if (id == 171 || id == 177 || id == 180)
            {
                SwiftScale(spine, R(.85f, .96f, 1.16f));
                SwiftScale(shL, R(.85f, 1, 1)); SwiftScale(shR, R(.85f, 1, 1));
                for (int s = -1; s <= 1; s += 2)
                {
                    var leg = s < 0 ? hipL : hipR;
                    SwiftPart(id, leg, "Hoof shoe " + s, id == 171 ? new Color(.95f, .36f, .08f) : gold, b => {
                        RarePlate(b, R(0, -.28f, .045f), R(.23f, .035f, .24f));
                        for (int i = -1; i <= 1; i += 2) RareRod(b, R(i * .065f, -.265f, .14f), R(i * .065f, -.20f, .14f), .015f, .008f);
                    });
                    if (id == 171) SwiftPart(id, leg, "Swept heel flames " + s, gold, b => {
                        for (int i = 0; i < 3; i++) RareTube(b, new[] { R((i - 1) * .055f, -.26f, -.04f), R((i - 1) * .065f, -.16f, -.15f), R((i - 1) * .08f, -.10f + .06f * i, -.27f) }, new[] { .035f, .024f, .001f }, 3, 5);
                    }, true);
                }
                if (id == 177)
                {
                    SwiftPart(id, head, "Golden bridle", gold, b => {
                        for (int s = -1; s <= 1; s += 2) RareTube(b, new[] { R(s * .125f, .17f, -.03f), R(s * .155f, .07f, .13f), R(s * .10f, -.035f, .18f) }, new[] { .018f, .018f }, 4, 5);
                        RareRod(b, R(-.11f, .01f, .20f), R(.11f, .01f, .20f), .018f, .018f);
                    });
                    SwiftPart(id, spine, "Swept crimson mane", new Color(.76f, .15f, .19f), b => {
                        for (int i = 0; i < 7; i++) RareTube(b, new[] { R(0, .42f - i * .045f, -.07f - i * .018f), R(0, .50f - i * .04f, -.20f - i * .02f), R(0, .43f - i * .045f, -.32f - i * .02f) }, new[] { .055f, .04f, .001f }, 3, 5, .65f);
                    });
                    SwiftPart(id, spine, "Chest harness", gold, b => {
                        for (int s = -1; s <= 1; s += 2) RareRod(b, R(s * .19f, .36f, .15f), R(s * .065f, .09f, .22f), .022f, .022f);
                        RarePlate(b, R(0, .17f, .23f), R(.13f, .05f, .12f));
                    });
                }
                else if (id == 180)
                {
                    SwiftPart(id, head, "Desert wind goggles", new Color(.22f, .55f, .64f), b => {
                        for (int s = -1; s <= 1; s += 2) { var ring = new Vector3[9]; for (int i = 0; i < 9; i++) { float a = i * Mathf.PI / 4; ring[i] = R(s * .065f + Mathf.Cos(a) * .047f, .08f + Mathf.Sin(a) * .028f, .225f); } RareTube(b, ring, new[] { .009f, .009f }, 8, 4, caps: false); }
                        RareRod(b, R(-.02f, .08f, .225f), R(.02f, .08f, .225f), .009f, .009f, 4);
                    });
                    SwiftPart(id, spine, "Split sand mantle", new Color(.89f, .65f, .29f), b => {
                        for (int s = -1; s <= 1; s += 2) SwiftRibbon(b, R(s * .10f, .36f, -.16f), R(s * .14f, .23f, -.34f), R(s * .20f, .28f, -.56f), .075f);
                    });
                }
                else SwiftPart(id, head, "Aerodynamic ember horns", cream, b => {
                    for (int s = -1; s <= 1; s += 2) RareTube(b, new[] { R(s * .12f, .14f, .02f), R(s * .14f, .24f, -.08f), R(s * .16f, .22f, -.27f) }, new[] { .038f, .023f, .001f }, 4, 5);
                });
            }
            else if (id == 172 || id == 179)
            {
                SwiftScale(spine, R(.86f, 1f, 1f));
                if (id == 179)
                {
                    if (flame) flame.gameObject.SetActive(false);
                    SwiftPart(id, head, "Fennec ears", cream, b => { for (int s = -1; s <= 1; s += 2) SwiftEar(b, s, R(s * .11f, .14f, .025f), .23f); });
                    SwiftPart(id, head, "Fennec inner ears", new Color(.78f, .38f, .21f), b => { for (int s = -1; s <= 1; s += 2) { var a = R(s * .11f, .17f, .028f); b.Tri(a + R(-.035f, 0, .003f), a + R(.035f, 0, .003f), a + R(s * .034f, .15f, -.026f), Color.white); } });
                    SwiftPart(id, spine, "Curling desert fox tail", new Color(.98f, .64f, .28f), b => RareTube(b, new[] { R(0, .02f, -.12f), R(.12f, .04f, -.32f), R(.20f, .24f, -.46f), R(.12f, .40f, -.41f), R(.015f, .43f, -.32f) }, new[] { .05f, .095f, .085f, .001f }, 10, 7));
                    SwiftPart(id, spine, "Ivory tail tip", cream, b => RareTube(b, new[] { R(.17f, .34f, -.45f), R(.12f, .40f, -.41f), R(.015f, .43f, -.32f) }, new[] { .075f, .055f, .001f }, 4, 6));
                }
                else
                {
                    SwiftPart(id, spine, "Spark comet tail", new Color(1, .32f, .06f), b => RareTube(b, new[] { R(0, .10f, -.13f), R(.035f, .12f, -.32f), R(-.035f, .25f, -.55f), R(.02f, .32f, -.69f) }, new[] { .085f, .06f, .035f, .001f }, 7, 6));
                    SwiftPart(id, spine, "Comet white hot crest", cream, b => { for (int s = -1; s <= 1; s += 2) SwiftRibbon(b, R(s * .03f, .16f, -.16f), R(s * .05f, .27f, -.39f), R(s * .025f, .34f, -.61f), .025f); }, true);
                    SwiftPart(id, head, "Swept spark crest", gold, b => { for (int s = -1; s <= 1; s += 2) RareRod(b, R(s * .11f, .16f, .02f), R(s * .18f, .24f, -.17f), .04f); });
                }
            }
            else if (id == 173 || id == 178)
            {
                SwiftScale(spine, R(.83f, .88f, 1.08f));
                SwiftScale(head, R(.82f, .92f, 1.02f));
                // Replace the boar's tusks with feline/canine muzzle details, without touching Head.
                foreach (Transform t in head) { var f = t.GetComponent<MeshFilter>(); if (f && f.sharedMesh == pCone) t.gameObject.SetActive(false); }
                foreach (var leg in new[] { hipL, hipR, hipBL, hipBR })
                {
                    foreach (Transform t in leg) { var f = t.GetComponent<MeshFilter>(); if (f && f.sharedMesh == pStem)
                        f.sharedMesh = SwiftMesh("Lean digitigrade limb", b => b.Lathe(new[] { 0f, 0f, 0f, .48f, .16f, .48f, .38f, .29f, .66f, .34f, 1f, .45f, 1f, 0f }, 8, null, rot: Quaternion.Euler(-90, 0, 0))); }
                    SwiftPart(id, leg, "Runner toes " + leg.name, id == 173 ? new Color(.63f, .86f, .49f) : new Color(.52f, .48f, .71f), b => {
                        for (int toe = -1; toe <= 1; toe++) RareTube(b, new[] { R(toe * .035f, -.255f, .015f), R(toe * .04f, -.26f, .07f), R(toe * .04f, -.265f, .105f) }, new[] { .022f, .024f, .003f }, 2, 5);
                    });
                }
                SwiftPart(id, head, "Ivory muzzle pads", id == 173 ? new Color(.77f, .92f, .62f) : new Color(.67f, .63f, .81f), b => {
                    for (int side = -1; side <= 1; side += 2) RareTube(b, new[] { R(side * .047f, -.045f, .18f), R(side * .047f, -.035f, .24f), R(side * .025f, -.035f, .275f) }, new[] { .035f, .045f, .008f }, 3, 6);
                });
                SwiftPart(id, head, "Animal nose", new Color(.13f, .17f, .23f), b => RarePlate(b, R(0, -.005f, .265f), R(.065f, .025f, .045f)));
                foreach (var f in spine.GetComponentsInChildren<MeshFilter>())
                    if (f.sharedMesh == pThorns) f.sharedMesh = SwiftMesh(id + "/Swept thorns", b => {
                        for (int i = 0; i < (id == 173 ? 7 : 4); i++) RareRod(b, R((i % 2 == 0 ? -.13f : .13f), .22f, .25f - i * .085f), R((i % 2 == 0 ? -.17f : .17f), id == 173 ? .31f : .26f, -.01f - i * .085f), id == 173 ? .04f : .055f);
                    });
                // Replacement thorn mesh is in local metres, not the old unit blob coordinates.
                foreach (Transform t in spine) { var f = t.GetComponent<MeshFilter>(); if (f && f.sharedMesh.name.EndsWith("Swept thorns")) { t.localPosition = Vector3.zero; t.localScale = Vector3.one; } }
                SwiftPart(id, head, id == 173 ? "Leopard ears" : "Hound ears", id == 173 ? teal : new Color(.42f, .36f, .68f), b => { for (int s = -1; s <= 1; s += 2) SwiftEar(b, s, R(s * .095f, .11f, .035f), id == 173 ? .10f : .17f); });
                SwiftPart(id, spine, "Long balancing tail", id == 173 ? teal : new Color(.32f, .27f, .52f), b => RareTube(b, new[] { R(0, .04f, -.41f), R(.07f, .06f, -.57f), R(.15f, .16f, -.68f), R(.10f, .24f, -.71f) }, new[] { .04f, .027f, .017f, .003f }, 7, 6));
                SwiftPart(id, spine, id == 173 ? "Jade leopard rosettes" : "Moonlit flank marks", id == 173 ? new Color(.12f, .34f, .23f) : new Color(.70f, .78f, 1f), b => {
                    for (int s = -1; s <= 1; s += 2) for (int i = 0; i < 4; i++)
                    {
                        if (id == 173) { var ring = new Vector3[7]; for (int n = 0; n < 7; n++) { float a = n * Mathf.PI / 3; ring[n] = R(s * (.229f - .009f * Mathf.Abs(Mathf.Sin(a))), .025f + Mathf.Sin(a) * .035f, .18f - i * .12f + Mathf.Cos(a) * .024f); } RareTube(b, ring, new[] { .009f, .009f }, 6, 4, caps: false); }
                        else RareTube(b, new[] { R(s * .22f, .075f, .20f - i * .12f), R(s * .235f, .035f, .18f - i * .12f), R(s * .22f, -.005f, .20f - i * .12f) }, new[] { .012f, .012f }, 2, 4);
                    }
                });
            }
            else if (id == 174)
            {
                SwiftScale(head, R(.69f, 1.05f, .69f));
                SwiftPart(id, head, "Folded cap ribs", cream, b => {
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; var d = R(Mathf.Cos(a), 0, Mathf.Sin(a)); var path = new Vector3[6]; for (int j = 0; j < 6; j++) { int n = 7 - j; path[j] = R(0, .02f + CapProf[n * 2] * .735f, 0) + d * (CapProf[n * 2 + 1] * .5244f + .007f); } RareTube(b, path, new[] { .007f, .009f, .006f }, 6, 4); }
                });
                SwiftPart(id, head, "Spore spindle jewel", new Color(.5f, 1f, .78f), b => RareTube(b, new[] { R(0, .24f, 0), R(0, .34f, 0), R(0, .42f, 0) }, new[] { .04f, .027f, .001f }, 3, 6), true);
                SwiftPart(id, spine, "Trailing leaf skirt", teal, b => { for (int i = -1; i <= 1; i++) SwiftRibbon(b, R(i * .08f, .22f, -.08f), R(i * .11f, .14f, -.20f), R(i * .15f, .19f, -.37f), .045f); });
            }
            else if (id == 175)
            {
                foreach (var fin in new[] { finL, finR })
                {
                    int s = fin == finL ? -1 : 1;
                    SwiftPart(id, fin, "Flying pectoral sail " + s, new Color(.36f, .91f, 1f), b => {
                        var a = R(0, 0, .035f); var c = R(s * .43f, .055f, -.21f); var d = R(s * .17f, -.025f, -.30f);
                        b.Tri(a, c, d, Color.white); b.Tri(a, d, c, Color.white);
                        b.Tri(a, d, R(0, 0, -.12f), Color.white); b.Tri(a, R(0, 0, -.12f), d, Color.white);
                    }).localRotation = Quaternion.Euler(0, 0, s * 108f);
                    SwiftPart(id, fin, "Pectoral rays " + s, cream, b => { for (int i = 0; i < 4; i++) RareRod(b, R(0, .004f, .03f), R(s * (.43f - i * .09f), .06f - i * .023f, -.21f - i * .03f), .006f, .002f, 4); }).localRotation = Quaternion.Euler(0, 0, s * 108f);
                }
                SwiftPart(id, spine, "Ocean brow streamers", cream, b => { for (int s = -1; s <= 1; s += 2) RareTube(b, new[] { R(s * .06f, .07f, .25f), R(s * .10f, .13f, .08f), R(s * .14f, .11f, -.12f) }, new[] { .012f, .009f, .001f }, 4, 4); });
            }
            else if (id == 176)
            {
                Transform shell = null;
                foreach (Transform t in spine) { var f = t.GetComponent<MeshFilter>(); if (f && f.sharedMesh == sShell) {
                    shell = t; t.localScale = Vector3.Scale(t.localScale, R(.76f, .78f, .80f));
                    f.sharedMesh = SwiftMesh("176/light conch", b => b.Lathe(new[] { .12f, 0f, .12f, .38f, .25f, .43f, .42f, .35f, .58f, .23f, .74f, .10f, .82f, 0f }, 12, null, rot: Quaternion.Euler(-90, 0, 0)));
                } }
                if (shell) SwiftPart(id, shell, "Pearl conch spiral", cream, b => {
                    var path = new Vector3[25]; for (int i = 0; i < path.Length; i++) { float t = i / 24f, a = t * Mathf.PI * 4; float y = .25f + .49f * t; float radius = y < .42f ? Mathf.Lerp(.43f, .35f, (y - .25f) / .17f) : y < .58f ? Mathf.Lerp(.35f, .23f, (y - .42f) / .16f) : Mathf.Lerp(.23f, .10f, (y - .58f) / .16f); path[i] = R(Mathf.Cos(a) * (radius + .008f), y, Mathf.Sin(a) * (radius + .008f)); }
                    RareTube(b, path, new[] { .012f, .009f }, 24, 4);
                });
                foreach (var leg in new[] { hipL, hipR, hipBL, hipBR })
                {
                    SwiftScale(leg, R(1.2f, 1, .63f));
                    float s = leg == hipL || leg == hipBL ? -1 : 1;
                    SwiftPart(id, leg, "Runner leg strut " + leg.name, cream, b => RareTube(b, new[] { R(0, 0, 0), R(s * .20f, .015f, -.04f), R(s * .29f, -.17f, -.08f) }, new[] { .022f, .018f, .006f }, 3, 5));
                }

            }
            rockR = rockList.ToArray(); rockC = rockCols.ToArray(); rockG = rockGlows.ToArray();
            var added = new Renderer[rockList.Count - first]; rockList.CopyTo(first, added, 0, added.Length);
            return added;
        }
    }
}
