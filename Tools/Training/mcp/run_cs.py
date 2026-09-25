import json,subprocess,os,sys
code=open(sys.argv[1],encoding='utf-8').read()
env=dict(os.environ,PYTHONIOENCODING='utf-8')
r=subprocess.run(['python',os.path.join(os.path.dirname(os.path.abspath(__file__)),'mcp.py'),'tool',json.dumps({"_name":"execute_code","action":"execute","code":code})],capture_output=True,text=True,encoding='utf-8',env=env)
out=r.stdout
try:
    d=json.loads(out); print(d["data"]["result"] if d.get("success") else out)
except Exception: print(out, r.stderr)
