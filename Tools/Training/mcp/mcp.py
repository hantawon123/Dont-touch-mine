import json, os, sys, urllib.request
URL = "http://127.0.0.1:8080/mcp"
SID = os.path.join(os.path.dirname(__file__), "mcp_sid.txt")
def post(body, sid=None):
    h = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
    if sid: h["mcp-session-id"] = sid
    req = urllib.request.Request(URL, json.dumps(body).encode(), h)
    with urllib.request.urlopen(req, timeout=float(os.environ.get("MCP_TIMEOUT", "120"))) as r:
        new = r.headers.get("mcp-session-id")
        raw = r.read().decode("utf-8", "replace")
    out = None
    for line in raw.splitlines():
        if line.startswith("data:"):
            out = json.loads(line[5:])
    if out is None and raw.strip():
        out = json.loads(raw)
    return new, out
def session():
    if os.path.exists(SID):
        return open(SID).read().strip()
    sid, _ = post({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"claude-curl","version":"1"}}})
    post({"jsonrpc":"2.0","method":"notifications/initialized"}, sid)
    open(SID,"w").write(sid)
    return sid
def call(method, params):
    sid = session()
    try:
        _, out = post({"jsonrpc":"2.0","id":2,"method":method,"params":params}, sid)
    except urllib.error.HTTPError as e:
        if e.code in (400, 404):
            os.remove(SID); sid = session()
            _, out = post({"jsonrpc":"2.0","id":2,"method":method,"params":params}, sid)
        else: raise
    return out
if __name__ == "__main__":
    method = sys.argv[1]
    params = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    if method == "tool":
        out = call("tools/call", {"name": params.pop("_name"), "arguments": params})
    elif method == "res":
        out = call("resources/read", {"uri": params["uri"]})
    else:
        out = call(method, params)
    res = out.get("result", out)
    if isinstance(res, dict) and "content" in res:
        for c in res["content"]:
            if c.get("type") == "image" and os.environ.get("MCP_IMG"):
                import base64
                open(os.environ["MCP_IMG"], "wb").write(base64.b64decode(c["data"]))
                print("image saved", os.environ["MCP_IMG"])
                continue
            text = c.get("text", json.dumps(c))
            if os.environ.get("MCP_IMG") and '"image_base64"' in text:
                import base64, re
                try:
                    d = json.loads(text)
                    def find(o):
                        if isinstance(o, dict):
                            for k, v in o.items():
                                if k in ("image_base64", "imageBase64") and isinstance(v, str): return v
                                r = find(v)
                                if r: return r
                        return None
                    b = find(d)
                    if b:
                        open(os.environ["MCP_IMG"], "wb").write(base64.b64decode(b))
                        print("image saved", os.environ["MCP_IMG"]); continue
                except Exception as e:
                    print("img parse failed", e)
            print(text[:int(os.environ.get("MCP_MAX","20000"))])
    elif isinstance(res, dict) and "contents" in res:
        for c in res["contents"]:
            print(c.get("text", "")[:int(os.environ.get("MCP_MAX","20000"))])
    else:
        print(json.dumps(res, ensure_ascii=False)[:int(os.environ.get("MCP_MAX","20000"))])
