using UnityEngine;
namespace SpellDuel
{
    public sealed partial class MinionLook
    {
        MinionRig swiftRig;
        Transform swiftAnchor;
        readonly Vector3[][] swiftHistory = new Vector3[3][];
        readonly LineRenderer[] swiftWind = new LineRenderer[3];
        Vector3 swiftLast;
        float swiftSample, swiftVisibility;
        bool swiftStarted;
        const int SwiftSamples = 12; // 3 * 11 * 2 = 66 triangles, bounded regardless of speed.

        void AddSwift(MinionRig rig)
        {
            swiftRig = rig; swiftAnchor = rig.SwiftArtAnchor;
            foreach (var r in rig.AddSwiftArt(def.id)) weapon.Add(r); // Preserve accessory palette; rig still flashes/fades it.
            Color c = def.id == 178 ? new Color(.60f, .65f, 1f) : def.tint;
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Swift wind ribbon " + i); go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = SpellFx.BlendMat(SpellFx.TexLine);
                line.useWorldSpace = true; line.positionCount = SwiftSamples;
                line.numCapVertices = 0; line.numCornerVertices = 0; line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
                line.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.Lerp(c, Color.white, .55f), 0), new GradientColorKey(c, 1) },
                    new[] { new GradientAlphaKey(def.id == 178 ? .32f : .65f, 0), new GradientAlphaKey(0, 1) });
                line.colorGradient = gradient; line.enabled = false;
                swiftWind[i] = line; swiftHistory[i] = new Vector3[SwiftSamples]; weapon.Add(line);
            }
            swiftLast = transform.position;
        }

        // Called by MinionLook after the rig's Update. Uses displacement, never modifies combat swift/speed.
        void TickSwift(float dt)
        {
            if (!swiftRig || !swiftAnchor || dt <= 0) return;
            var now = transform.position; var delta = now - swiftLast; swiftLast = now;
            float speed = delta.magnitude / Mathf.Max(.001f, dt);
            bool moving = swiftRig.SwiftArtAlive && !Sneak && speed > .18f && delta.magnitude < 3f;
            swiftVisibility = Mathf.MoveTowards(swiftVisibility, moving ? Mathf.Clamp01(speed / 1.4f) : 0f, dt * (moving ? 8f : 4f));
            swiftSample += dt;
            for (int i = 0; i < 3; i++)
            {
                var pos = swiftAnchor.position + transform.right * ((i - 1) * .13f) + transform.up * (.10f + (i == 1 ? .15f : 0)) - transform.forward * .16f;
                var points = swiftHistory[i];
                if (!swiftStarted || delta.magnitude >= 3f) for (int n = 0; n < SwiftSamples; n++) points[n] = pos;
                else if (swiftSample >= .025f) for (int n = SwiftSamples - 1; n > 0; n--) points[n] = points[n - 1];
                points[0] = pos;
                var line = swiftWind[i]; line.SetPositions(points); line.widthMultiplier = (i == 1 ? .075f : .035f) * swiftVisibility;
                line.enabled = swiftVisibility > .01f && !Sneak;
            }
            if (swiftSample >= .025f) swiftSample = 0;
            swiftStarted = true;
        }

        void OnDisable()
        {
            swiftStarted = false; swiftVisibility = 0; swiftSample = 0;
            foreach (var line in swiftWind) if (line) line.enabled = false;
        }
    }
}
