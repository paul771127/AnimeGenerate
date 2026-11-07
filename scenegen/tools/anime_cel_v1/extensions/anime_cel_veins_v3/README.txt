006 texture-only cel volcano revision

styleId: anime_cel_volcano_veins_v3; source: 005 volcano_cel_v2, version 5.
Use cel_kit_control.py API v1: capabilities -> plan -> validate -> generate -> inspect.
The source FBX files are copied unchanged. Runtime atlas and geometry budgets remain 005-compatible.
style.json in job 006 provides portable inputs; generator_request.json records the actual local run.
Set BLENDER_PATH to Blender 4.5's executable when replaying from the mirrored repository tools.
Always choose a new outputFolder. The draft option changes preview size, retaining the source atlas sizes.

Implementation: vein_graph.py follows projected real mesh fold edges, with deterministic branching, tapering and small offsets.
cel_veins_builder.py restores old hot pixels, then bakes the new fissures.
texture_io.py writes exact RGB8 PNG values while converting Blender bottom-up buffers to PNG scanlines.
validate_exports.py reimports all FBX, checks actual flow-band pixels and recomputes direction statistics.
audit_preserved.py is an additional read-only check of unchanged door seals, flow sheets, cave pool, crater pools and grounds:
  blender -b --python-exit-code 1 --python audit_preserved.py -- --run ABSOLUTE_RUN

reference/cold_main_v1.png and cold_emissive_v1.png are existing project artwork from Git commit
c3c5d02faaeeed0b34e15ff5864e4b5f4d4c68b0, original SceneKit/volcano_cel_v1 files.
They restore cold rock under the removed 005 lines. No new AI image or third-party artwork is introduced.

5-9% emission is area-weighted across ordinary wall variants, allowing intentionally quiet and dense variants.
Segment angles use metre-scale surface H/Y charts; <=15 degrees from vertical. Both count and length fractions must be <=40%.
Preserved flow sheets, door seals and crater pools are excluded from new-vein angles.
Phone FPS and heat are not measured. Android/iOS import caps are unchanged from 005.
