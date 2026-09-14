"""Validate browser probe evidence; fail on missing observations, not just explicit errors."""
import argparse
import re
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('events', type=Path)
args = parser.parse_args()
lines = args.events.read_text(encoding='utf-8').splitlines()
assert not any(' FAIL ' in line or 'STOP_FAILED' in line for line in lines), 'Runtime failure'
assert not any('wrongEcho=' in line and 'wrongEcho=0' not in line for line in lines), 'Cross-client echo'
for peer in ('1', '2'):
    observations = [line for line in lines if f'peer={peer} ' in line]
    assert any('PASS role=Client' in line and 'players=2' in line for line in observations), f'No two-player PASS for {peer}'
    assert any('STOPPED cleanly' in line for line in observations), f'No clean stop for {peer}'
survivor_ticks = [int(re.search(r'tick=(\d+)', line)[1]) for line in lines
                  if 'peer=2 ' in line and 'PASS role=Client' in line
                  and 'survivedPeerExit=True' in line and 'STOPPED' not in line]
assert len(set(survivor_ticks)) >= 2, 'No ongoing replication after other client left'
assert any('peer=4 ' in line and 'CONNECT_FAILED GameNotFound' in line for line in lines), 'No version rejection'
assert any('peer=5 ' in line and 'CONNECT_FAILED GameNotFound' in line for line in lines), 'No missing-room rejection'
print('PASS: both WebGL clients, authority replication/RPC/private echo, peer-exit survival, clean stops, rejection paths')
