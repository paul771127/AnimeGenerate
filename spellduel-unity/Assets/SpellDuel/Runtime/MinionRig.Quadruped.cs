using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public partial class MinionRig
    {
        static readonly Dictionary<bool, Mesh> quadrupedLegMeshes = new Dictionary<bool, Mesh>();
        // Anatomical render geometry under the existing hip: front elbow bends back;
        // rear stifle points forward, then the hock back. No new animation Pivot.
        static Mesh QuadrupedLegMesh(bool rear)
        {
            if (quadrupedLegMeshes.TryGetValue(rear, out var mesh) && mesh) return mesh;
            var b = new PMesh(1);
            var knee = R(0, -.115f, rear ? .085f : -.055f);
            var ankle = R(0, -.225f, rear ? -.045f : .012f);
            RareTube(b, new[] { R(0, .015f, 0), R(0, -.045f, rear ? .025f : -.02f), knee },
                new[] { rear ? .078f : .065f, rear ? .082f : .06f, .044f }, 4, 7, .83f);
            RareTube(b, new[] { knee, Vector3.Lerp(knee, ankle, .5f) + R(0, 0, rear ? -.012f : .008f), ankle, R(0, -.26f, .025f) },
                new[] { .043f, .033f, .026f, .037f }, 5, 6, .9f);
            RareTube(b, new[] { R(0, -.26f, -.014f), R(0, -.26f, .035f), R(0, -.267f, .10f) },
                new[] { .028f, .057f, .012f }, 3, 7, .42f);
            mesh = b.Build(rear ? "Quadruped thigh shin hock paw" : "Quadruped upper arm forearm paw", true, true);
            RareSmooth(mesh); quadrupedLegMeshes[rear] = mesh; return mesh;
        }
    }
}
