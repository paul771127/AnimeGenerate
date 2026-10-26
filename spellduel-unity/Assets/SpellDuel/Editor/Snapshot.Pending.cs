#if UNITY_EDITOR
namespace SpellDuel.EditorTools {
public static partial class Snapshot { static void PendingArtShots(UnityEngine.Camera cam){PendingArtAudit.Run();} }
}
#endif
