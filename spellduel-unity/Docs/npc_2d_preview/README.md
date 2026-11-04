# NPC sprite delivery

User override (2026-11-04): all NPCs use refined Japanese fantasy **2.5-head proportions**, superseding the brief's normal-height anatomy. Clothing and character identity follow Story2D references. Atlas cell size and world-height placement remain unchanged.

## Batch 1: Erin

- Eight directions: idle 4, walk 6, gesture 4 (112 cells).
- Gesture: gently brush silver hair beneath hood, returning to idle.
- Generated using built-in image_gen; exact prompts and original sheets are in Tools/sprite_npcs/sources/erin/.
- Packing performs only cropping, uniform group resampling and placement; no mirrored views or synthetic poses.
- Adult sprite height approximately 179 px; feet at y=241.
- check_npc_sprites.py --partial: 1 NPC, 112 cells, 0 errors.
- No runtime or gameplay code changed. Full 22-NPC validation remains pending until later batches.

## Batch 2: Xiaotie, Laoyan, Cui, Feng, Haido

Five additional NPCs with full idle/walk/gesture atlases, eight-direction previews, animation GIFs and Story2D comparisons. Total: 6 NPCs, 672 cells; partial validator reports 0 errors. Laoyan source edge inspection confirmed complete shoes; alpha below 64 is excluded only from edge-contact diagnosis, never removed from artwork.

## Batch 3: Lingyin, Ark, Xiaomai, Oldsmith shade, Gearmerchant

Full eight-direction idle/walk/gesture sets and previews. Total 11 NPCs, 1232 cells, 0 partial validation errors. Ghost retains blue ethereal appearance; mechanical merchant follows the existing automaton portrait.
