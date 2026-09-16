# Approved shoes and expressions in Unity

The original Blender files are preserved. The closet uses these approved versions:

| Option | Source |
| --- | --- |
| Rounded shoes (28 colour swatches) | `Shoes/BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes_Compact.blend` |
| `face_sparkle` / 반짝반짝 | `Expressions/BasicPlayerCapsule_Bear_SparkleEyes_Printed.blend` |
| `face_sleepy` / 나른한 눈 | `Expressions/BasicPlayerCapsule_Bear_SleepyEyes_Volume.blend` |
| `face_fierce` / 사나운 눈 | `Expressions/BasicPlayerCapsule_Bear_FierceEyes_Bigger.blend` |
| `face_brown` / 갈색 눈망울 | `Expressions/BasicPlayerCapsule_Bear_BrownGlossyEyes.blend` |
| `face_default` / 기본 | Original character eyes |

## Rebuild

1. Run Blender in background with `Tools/export_approved_wearables.py`.
2. In Unity, run **Game → Setup → Approved shoes and expressions** after compilation finishes.
3. Inspect `artifacts/smooth-bear-unity/wearables/result.json`, `validation.txt` and the rendered images.

The approved knit review files are in `Hoods/StraightKnit_Review/` and `Shoes/BasicPlayerCapsule_Bear_KnitShoes_Review.blend`.
Unity recreates their straight ribs with `Game/Character/Straight Knit`, using object-anchored axes rather than radial UVs.
Run **Game → Setup → Approved straight hood and shoe knit** to apply the shared fabric settings and regenerate hood thumbnails.
The original shoe geometry and all palette tint bindings remain unchanged. Blender and Unity lighting can still produce different apparent colours.

Export evaluates the approved modifiers at rest, preserves print UVs, removes unused material slots and triangulates before FBX import. Eye designs stay printed onto the sclera surface. Unity creates meshes in Head / Foot.L / Foot.R local coordinates, so no second skeleton or animation controller is needed.

`AvatarWearable` assets, meshes, materials and icons are under `Assets/_Game/Content/Characters/SmoothBear/Wearables`. `AvatarPartCatalog` drives the existing closet grid and stable saved selection IDs. Corresponding existing colour IDs are retained; removed colours resolve to the configured category default. Retired placeholder expressions resolve to the basic expression.

The applier caches accessory renderers per character, restores original eyes for the basic expression, and uses material property blocks for body-coloured eyelids and shoe colours. It shares meshes and materials between avatars. `AvatarPreview`, `SmoothBear` and `PlayerCharacter` prefabs have attachment bindings; `NetworkedPlayer` inherits the latter. This change does not introduce multiplayer appearance replication.

Regression coverage: closet selection/apply/cancel suite; face restoration; independent avatars; foot attachment and preview layer; repeated selections without duplicate renderers; missing-bone fallback. The editor validator also samples all 119 clips with each of five eye choices and checks shoe/body colour combinations.

## Approved palette and portrait controls

`SmoothBearPalette.json` contains the same approved 19 exact HEX colours plus a pale lilac and eight vivid options (28 total) for body, hood and shoes. All three share hue-family order (orange, yellow, green, blue, purple, pink, red), with light-to-dark neutrals at the end. Configured category defaults keep the yellow body / sky hood / blue shoes outfit independent of display order. `Tools/expand_smooth_bear_palette.py` reproduces this palette. Run **Game → Setup → Pastel customization palette** after changing this file to update the catalogue and regenerate shoe thumbnails.

On the character portrait: left-drag rotates horizontally through 360°, vertical motion is ignored, and double-click restores the front view. The portrait uses a fixed 1.32 orthographic size (approximately one former wheel step larger than the previous default) and a fixed 6° elevation. Wheel zoom is disabled. The target is raised to 1.20 so rabbit ears and soles stay inside the frame. Dragging is scaled by the portrait size so home and lobby views behave consistently. The colour grid retains its own scrolling. The portrait key and fill lights move with the camera to keep every horizontal angle readable.

The portrait uses neutral key, fill and lower frontal lights (10 / 9 / 3) to lift dark areas while keeping dimensional shading. The rotation hint is black; thumbnails also use fill lighting.

Brown glossy eyes: export only this addition with Blender using `Tools/export_approved_wearables.py -- --only Brown`, then run **Game → Setup → Brown glossy eyes**. This adds the face without rebuilding the character prefabs. Four body-tinted eyelids and two sclera meshes follow Head; the brown iris and highlights stay printed on the eye surface.
