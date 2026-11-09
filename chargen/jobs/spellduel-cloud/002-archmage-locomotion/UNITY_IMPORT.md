# Job 002 import notes

The FBX contains all 18 clips. The original ten action curves and mesh/UV/weight data were preserved.

New loops: `walk_back`, `walk_left`, `walk_right`, `run_back`, `run_left`, `run_right`, `cast_staff_loop`.
`cast_staff` is a non-looping 0.8-second right-hand staff thrust. A suggested release cue is 0.44 s; no gameplay event is inserted.

## Humanoid extra bones

Unity 2022.3 Humanoid's default imported mask can discard `Staff` and `Book` Transform animation while retaining the right-arm muscle animation. This produces a hand thrust but a wrongly oriented staff.

The verified import used a full Transform AvatarMask, all humanoid body parts enabled, and `ModelImporterClipAnimation.ConfigureClipFromMask(mask)` plus `maskType=CopyFromOther` and a persistent `maskSource` for the eight new clips. Both Staff rotation and position curves were confirmed present in the imported clips. The original ten clip settings need not be changed.

A small Editor-only configuration helper is provided as `generation/ConfigureArchmage002Import.cs.txt`. Copy it into a temporary Unity Editor folder or invoke its logic from the existing build/import process. It generates its mask locally, outside Resources; no .meta is supplied or required in Git. No Runtime modification is needed.

Tested with Unity 2022.3.62f2, the job 001 Humanoid mapping and the unchanged Built-in toon shader. Game-side `HeroModel` Playables can select the new names already specified by this work order. Preserve existing .meta/GUIDs if the consuming project already has them.

The static orb emission mask is unchanged. Bloom, projectile spawning and charge-intensity modulation remain game-side.

See Unity's official `ClipAnimationMaskType` and `ModelImporterClipAnimation.maskSource` API references for import mask behavior:
https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ClipAnimationMaskType.html
https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ModelImporterClipAnimation-maskSource.html
