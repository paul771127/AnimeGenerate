#if UNITY_EDITOR
namespace SpellDuel.EditorTools {
public static partial class Snapshot { static void PendingArtShots(UnityEngine.Camera cam){PendingArtAudit.Run();NeckHairAudit.Run();NeckHairShots(cam);PendingAllyShots(cam);} }
}
#endif
