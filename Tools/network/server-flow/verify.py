"""Check full-game process logs and emit only bounded, credential-free evidence."""
import argparse
from pathlib import Path
import re

parser = argparse.ArgumentParser()
parser.add_argument('logs', type=Path, help='server.log and client1.log through client6.log')
parser.add_argument('--output', type=Path)
args = parser.parse_args()
evidence = []
failures = []

def require(condition, message):
    if not condition:
        failures.append(message)

server = (args.logs / 'server.log').read_text(encoding='utf-8-sig', errors='replace')
for expected in ('[Server] Ready', '[Match] Started with 6 players.',
                 '[Match] Started with 5 players.', '[Server] Session closed.'):
    require(expected in server, 'Missing server lifecycle: ' + expected)
require(not re.search(r'(?:NullReference|InvalidOperation|VContainer|IndexOutOfRange)Exception', server),
        'Server has a runtime exception; inspect the local log')
evidence.extend(line for line in server.splitlines()
                if re.fullmatch(r'\[Server\] (?:Ready; waiting for first room owner\.|Session closed\.)|\[Match\] Started with [56] players\.', line))

for peer in range(1, 7):
    raw = (args.logs / f'client{peer}.log').read_text(encoding='utf-8-sig', errors='replace')
    # Never copy arbitrary exception messages, authentication or transport logs.
    lines = [line for line in raw.splitlines() if re.fullmatch(
        r'\[Flow988\] peer=[1-6] (?:CONNECTED role=Client|LEFT_DURING_MATCH|GUEST_START_REFUSED|'
        r'RESULT_RECEIVED|RESULT_SCENE_LOADED|REMATCH_REQUEST|OWNER_CLOSED_AFTER_REMATCH|'
        r'PASS rounds=\d+ result=(?:True|False)|'
        r'phase=\w+ players=\d+ rounds=\d+ assignments=\d+ serverTime=[\d.]+)', line)]
    text = '\n'.join(lines)
    require(f'[Flow988] peer={peer} CONNECTED role=Client' in text, f'Peer {peer} did not connect as Client')
    require(bool(re.search(fr'peer={peer} PASS rounds={1 if peer == 6 else 2} result={"False" if peer == 6 else "True"}', text)),
            f'Peer {peer} did not finish the required flow')
    require(f'[Flow988] peer={peer} FAIL' not in raw, f'Peer {peer} reported a failure')
    require(not re.search(r'(?:NullReference|ObjectDisposed|InvalidOperation|VContainer|IndexOutOfRange)Exception:', raw),
            f'Peer {peer} has a runtime exception; inspect the local log')
    if peer == 6:
        require('LEFT_DURING_MATCH' in text, 'Guest departure missing')
    if peer != 1:
        require('GUEST_START_REFUSED' in text, f'Guest {peer} start refusal missing')
    if peer != 6:
        require('RESULT_RECEIVED' in text and 'RESULT_SCENE_LOADED' in text,
                f'Peer {peer} result presentation evidence missing')
    if peer == 1:
        for action in ('REMATCH_REQUEST', 'OWNER_CLOSED_AFTER_REMATCH'):
            require(action in text, 'Owner action missing: ' + action)
    evidence.extend(lines)

if failures:
    raise SystemExit('\n'.join('FAIL: ' + problem for problem in failures))
result = 'PASS: local native server + six clients; external WebGL PCs remain a manual check.\n' + '\n'.join(evidence) + '\n'
if args.output:
    args.output.write_text(result, encoding='utf-8')
print(result)
