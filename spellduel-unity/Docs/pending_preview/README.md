# Pending art — facilities and clockwork cast

Five town maps retain their old hotspots and add separate alchemist / crafter buildings. map_snow deliberately remains the ochre Wind-sand Waystation.

Six facility identities × three expressions and seven clockwork identities × three expressions are 768×1152 RGBA. Body pixels and alpha are locked to the normal master outside the recorded facial masks. The oldsmith ghost uses 65% alpha. Expressions missing from a three-expression set use the existing normal fallback.

Two 1080×1920 flame interiors serve as fallback for towns without their own interior. Recipe payment, stats, result rolling and inventory transactions remain in the existing methods. A separate cosmetic RNG chooses NPC lines; the potion “quality” line is flavour, with no extra potion bonus. Craft light uses the actual item rarity.

21 supply PNGs come from SkillIcons' procedural math. Local .NET contract audit: all 21 distinct/deterministic at 8/32/48/128px; 111 legacy skill icons remain pixel-identical to 99901f5. This is not a Unity compilation test.

Local validation: check_village2d.py, check_story2d.py and check_dungeons.py. The original dialogue batch remains 37.45 MiB within its original 40 MiB budget; the newly requested 21 dungeon portraits add 13.08 MiB (separate 16 MiB ceiling). Facility staff are counted with village art. No story text or stage settings were edited.

Native PendingArtAudit additionally checks ten map entry routes, five-town interior fallback, all 39 new sprites, 21 imported/generated icons, and actual Story.ParseLine speaker lookup. It skips the player placeholder and narrator; no classtest implementation exists in this checkout.

prompts.json records the image_gen prompts. *_expressions.png and faces_review.jpg show the delivered assets; map_*_hotspots.jpg overlays their click targets. Native CI results are recorded after running, not inferred from these art sheets.

## Final delivery

- [Player 2D/3D lineup](players_2d_3d.jpg), [head lineup](player_heads.jpg), [before/after](player_before_after.jpg); individual native views in [neck/hair gallery](../neck_hair_preview/README.md).
- [Six-ally states](allies_four_states.jpg) and [label layout diagnostic](allies_label_layout.jpg). The latter overlays actual exported runtime rectangles on the native 3D frame.
- [World boss posters](worldboss_posters.jpg); map marker/layout JPGs and facility layout JPGs are labelled mockups.
- `ci_validation.json`, `native_source.json`, `native_audit.log` and `native_png_manifest.json` provide exact native and platform evidence. Native PNG hashes are verified.

Astra checked the final native back-of-head coverage, bishop crown and shoulder clasps after fixes. All required local validators, native audits, Android APK, iOS export/IPA and TestFlight succeeded. Phone AR FPS and a live six-device network session were not measured.
