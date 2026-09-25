#!/bin/bash
# Copy the newest HideSelect / SeekSelect checkpoint of a run into a git-ignored Assets folder so the editor can
# import them as ModelAssets for evaluation. Prints the asset paths (use them as Model:<path> in hs_eval.sh).
# usage: hs_checkpoints.sh <run-id>
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
RUN=${1:?run id}
DST="$ROOT/Assets/_Game/Content/Training/Local/HideSeekCheckpoints/$RUN"
mkdir -p "$DST"
for b in HideSelect SeekSelect; do
  f=$(ls -t "$ROOT/results/$RUN/$b"/$b-*.onnx 2>/dev/null | head -1)
  if [ -z "$f" ]; then echo "none:$b"; continue; fi
  cp "$f" "$DST/"
  echo "Assets/_Game/Content/Training/Local/HideSeekCheckpoints/$RUN/$(basename "$f")"
done
