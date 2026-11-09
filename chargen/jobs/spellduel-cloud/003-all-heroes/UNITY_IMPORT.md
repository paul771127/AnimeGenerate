# Job 003 Unity import

Each kit contains only one FBX, one 2048×2048 albedo and one 2048×2048 emission atlas. All 17 requested clips are embedded in the FBX. Use the game's existing `SpellDuel/CharCel` and `SpellDuel/CharOutline` shaders. The `OUTLINE_*` mesh is an inverted hull and needs back-face culling. No shader, Prefab or .meta is delivered.

The verified configuration is Unity 2022.3.62f2 Humanoid with the shared `J_Bip_*` mapping, root motion disabled and full Transform masks. Humanoid muscle animation alone does not retain the additional weapon transforms reliably. Include `Weapon_R`, `Weapon_L`, `Shield`, `Book`, `BowNock` and `Arrow` when present, together with their position/rotation/scale curves. Bow strings and arrows use these extra bones during drawing/release.

Copy `generation/ConfigureAllHeroes003Import.cs.txt` to an Editor folder as `ConfigureAllHeroes003Import.cs`. Run **SpellDuel → CharKit → Configure job 003 hero imports**. It applies the mapping, 17 named clips, loop flags and full masks to kits that are present. It creates masks under `Assets/Editor/CharKitImportMasks`; retain those masks locally for subsequent reimports. Unity generates its own .meta files. Preserve existing GUIDs when replacing an already imported kit. The helper modifies import settings only; no Runtime change is required.

Looping clips: `idle`, `walk`, `run`, `walk_back`, `walk_left`, `walk_right`, `run_back`, `run_left`, `run_right`, `skill_loop`, `crouch`.

Non-looping clips: `attack` (0.8 s), `skill` (1.0 s), `hit`, `jump`, `die`, `victory`.

Previews use the actual imported mesh, shared game shaders and CPU-baked skinned poses. `attack.gif` repeats as a review aid; the FBX attack clip remains non-looping. Particle systems, projectile events, companions, summoned creatures and cloth simulation are not embedded in these player meshes. Movement/facing and attack timing are game-side responsibilities. Phone FPS/thermal and full gameplay transitions were not measured in this asset work order.

Unity API references:
- https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ModelImporterClipAnimation.ConfigureClipFromMask.html
- https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ModelImporterClipAnimation-maskSource.html
