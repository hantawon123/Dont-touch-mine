# Current bear animation rebuild

The hand-edited bear with rounded heels is now exported to all 119 FBX assets in
`Assets/Scenes/CharacterTest/First`. These assets serve 139 states in
`FirstCharacterPreview.controller`. The original Unity `.meta` files, clip names,
durations, and controller references are unchanged.

The rebuild evaluates the original motion first, then derives each target bone's
local transform from the complete parent and child pose matrices. This avoids
frame-order-dependent baking artifacts. Source key ranges are resampled into the
existing Unity clip durations. Locomotion cycles receive mild hand/finger/toe
filtering; gestures and one-shot actions retain their intended movement. Loop
end mismatches are blended over the last few frames and closed exactly. Belly,
crouch and crawl shape animation is transferred to the current mesh.

The editable mesh, weights and rest skeleton are retained in the Blender output.
FBX exports contain the evaluated surface (9,212 Body vertices) and five
corrective blend shapes. The idle model also carries the current hood and face.

## Artifacts and recovery

Output directory:
`C:/Users/SSAFY/.codex/visualizations/2026/09/14/01a09e00-d780-7f01-a5d2-f47dc854742f/current-bear-all-actions`

- `BasicPlayerCapsule_Bear_AllActions.blend`: all rebuilt rig and shape actions.
- `originals/`: original FBXs, import settings, controller and scene backup.
- `fbx/`: installed replacement FBXs.
- `CurrentBear_Motions.png`: representative pose renders.
- `bake-report.json`: 4,019 source-to-target frame checks.
- `roundtrip-report.json`: all 119 exported files reimported successfully;
  loop closure and corrective shapes checked.
- `installation-verification.json`: installed file hashes and unchanged Unity metadata.

For rollback, copy the FBXs from `originals/` back to the First asset directory.
No controller or scene edits are required to restore the previous references.

## Unity verification

Open `Assets/Scenes/CharacterTest.unity`, allow the FBXs to reimport, then Play.
The existing motion picker uses the same states as before. If setup is needed,
use `Game > Setup > Apply CharacterTest Blender Preview`.

Unity automated verification was attempted but the installed Editor exited with
code 198: no valid Unity Editor license. Consequently the C# checks below have
not compiled or executed in this session, and Play Mode is not yet verified.

After restoring the local Unity license, run the Editor with:

```text
-batchmode -nographics -projectPath <project> -executeMethod Game.Editor.CurrentBearMotionValidation.Run -bearReport <output.json> -logFile <editor.log>
```

The Editor check samples imported clips on the CharacterTest scene character,
checks binding paths and deformed meshes, and saves the scene only on success.
The Play Mode test `CurrentBearMotionPlaybackTests.AllCharacterTestStatesEvaluateOnCurrentBear`
then exercises all 139 states through an actual Animator, including loop crossing.

## Reproduction

Run `Tools/retarget_current_bear_motions.py` using Blender with `-- CHARACTER.blend OUTPUT_DIR`.
It backs up inputs and stages outputs without installing them. Use a fresh output
directory for a new source generation. Run `Tools/verify_current_bear_exports.py`
before `Tools/install_current_bear_motions.py`; the installer checks that inputs
have not changed since backup and preserves all Unity metadata.
