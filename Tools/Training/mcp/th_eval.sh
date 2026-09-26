#!/bin/bash
# Thief evaluation: th_eval.sh None Rule Random "Model:<seek.onnx>,<hide.onnx>" ...  (EP, SEED, HIDING env vars)
S="$(cd "$(dirname "$0")" && pwd)"
L="$LOCALAPPDATA/Unity/Editor/Editor.log"; export PYTHONIOENCODING=utf-8; cd $S
EP=${EP:-200}; SEED=${SEED:-777001}; HIDING=${HIDING:-Rule}
for thief in "$@"; do
  until python mcp.py res '{"uri":"mcpforunity://editor/state"}' 2>/dev/null | grep -q '"is_playing":false,"is_paused":false,"is_changing":false'; do sleep 2; done
  python th_set.py "$thief" $EP $SEED $HIDING; python run_cs.py th_set.cs
  start=$(wc -l < "$L")
  python mcp.py tool '{"_name":"manage_editor","action":"play"}' >/dev/null
  end=$(( $(date +%s) + 1500 ))
  until tail -n +$start "$L" | grep -a -q "\[Thief EVAL\]\|\[Thief\] no hiding" || [ $(date +%s) -gt $end ]; do sleep 3; done
  sleep 2; tail -n +$start "$L" | grep -a "\[Thief EVAL" | head -2
  python mcp.py tool '{"_name":"manage_editor","action":"stop"}' >/dev/null; sleep 4
done
echo done
