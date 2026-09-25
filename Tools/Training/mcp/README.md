# Unity MCP helpers (training measurements)

Direct HTTP client for the MCP for Unity server (`http://127.0.0.1:8080/mcp`), used when the session's MCP connector
is not connected. The editor must have the MCP for Unity server running (Window > MCP for Unity > Start Server).

- `mcp.py tool '{"_name":"<tool>", ...}'` / `mcp.py res '{"uri":"mcpforunity://editor/state"}'` (set `PYTHONIOENCODING=utf-8`)
- `run_cs.py file.cs` runs a C# method body in the editor (`execute_code`) and prints the result.
- `hs_eval.sh Rule@Rule Random@Rule "Model:Assets/.../HideSelect.onnx@Rule" ...` hide-seek evaluation matrix: for each `hider@seeker` pair (Rule, Random or Model:<onnx asset path>), sets the
  open Mansion_HideSeek scene to evaluation mode through reflection (in memory only, the scene file is not changed),
  plays until the `[HideSeek EVAL]` line, stops. `EP=200 SEED=777001` by default 64 / 777001.
  After using it, reopen the scene from disk before training (evaluation mode stays set in memory).
- Long multi-line `Debug.Log` messages are cut by `read_console`; read `%LOCALAPPDATA%\Unity\Editor\Editor.log` instead.
