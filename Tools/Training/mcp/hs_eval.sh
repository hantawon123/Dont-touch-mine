#!/bin/bash
S="$(cd "$(dirname "$0")" && pwd)"
L="$LOCALAPPDATA/Unity/Editor/Editor.log"; export PYTHONIOENCODING=utf-8; cd $S
EP=${EP:-64}; SEED=${SEED:-777001}
for combo in "$@"; do
  h=${combo%%@*}; s=${combo##*@}
  until python mcp.py res '{"uri":"mcpforunity://editor/state"}' 2>/dev/null | grep -q '"is_playing":false,"is_paused":false,"is_changing":false'; do sleep 2; done
  python hs_set.py $h $s $EP $SEED; python run_cs.py hs_set.cs
  start=$(wc -l < "$L")
  python mcp.py tool '{"_name":"manage_editor","action":"play"}' >/dev/null
  end=$(( $(date +%s) + 900 ))
  until tail -n +$start "$L" | grep -a -q "\[HideSeek EVAL\]\|HideSeek\] no hiding" || [ $(date +%s) -gt $end ]; do sleep 3; done
  sleep 2; tail -n +$start "$L" | grep -a "\[HideSeek EVAL" | head -2
  python mcp.py tool '{"_name":"manage_editor","action":"stop"}' >/dev/null; sleep 4
done
echo done
