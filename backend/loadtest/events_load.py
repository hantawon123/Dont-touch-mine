"""플레이 로그 수집 부하 테스트 (S15P21D205-863).

"별도 서비스로 안 나눠도 괜찮은가"를 숫자로 답합니다. 이벤트 배치를 초당 N건 쏘면서 같은 앱의
게임 API(GET /api/v1/accounts/me) 응답 시간을 함께 잽니다. N 을 단계별로 올려 어디서 무너지는지 봅니다.

표준 라이브러리만 씁니다. 설치할 것이 없어야 누구든 같은 조건으로 다시 돌릴 수 있습니다.

    python loadtest/events_load.py --base http://localhost:8080 --stages 0,6,30,60,120,240 --stage-seconds 60

- stages: 단계별 초당 요청 수. 요청 하나가 배치 50건이므로 6 req/s = 300 events/s.
  예상 운영 부하는 6 req/s (동시 60명이 10초마다 한 번) 입니다. 0 은 기준선(부하 없음)입니다.
- 앱은 레이트 리밋을 끄고 띄워야 합니다. 한 IP 에서 쏘므로 기본값(분당 600)에 바로 걸립니다.
      ANALYTICS_RATE_LIMIT_PER_MINUTE=100000000 ./gradlew bootRun
- 드롭·실패 수는 앱 로그의 "플레이 로그 지난 1분" 요약에 있습니다. 이 스크립트는 클라이언트가 본
  것(상태 코드, 지연)만 잽니다. 저장된 행 수는 끝나고 DB 에서 셉니다.

운영 서버에 대고 돌리지 마세요. Jenkins 빌드와 경합하고 운영 game_event 에 가짜 행이 쌓입니다.
"""
import argparse
import http.client
import json
import random
import statistics
import sys
import threading
import time
import uuid
from urllib.parse import urlparse

EVENT_NAMES_HOST = ["position_sample"] * 17 + [
    "phase_change", "item_hidden", "item_picked_up", "punch_hit", "player_stunned",
]
EVENT_NAMES_CLIENT = ["punch_swung", "perf_sample", "scene_enter"]


def connect(base):
    u = urlparse(base)
    cls = http.client.HTTPSConnection if u.scheme == "https" else http.client.HTTPConnection
    return cls(u.hostname, u.port or (443 if u.scheme == "https" else 80), timeout=10)


def make_batch(session_id, seq_start, match_id, users, size):
    now = int(time.time() * 1000)
    events = []
    for i in range(size):
        host = random.random() < 0.85
        name = random.choice(EVENT_NAMES_HOST if host else EVENT_NAMES_CLIENT)
        events.append({
            "occurredAt": now - random.randint(0, 9000),
            "clientSessionId": session_id,
            "clientSeq": seq_start + i,
            "roomCode": "LOADT1",
            "matchId": match_id if host else None,
            "matchTimeMs": random.randint(0, 360_000) if host else None,
            "userPublicId": random.choice(users),
            "eventName": name,
            "phase": "Searching",
            "mapId": "basement",
            "posX": round(random.uniform(-30, 30), 2),
            "posY": 0.0,
            "posZ": round(random.uniform(-30, 30), 2),
            "fromHost": host,
            "schemaVer": 1,
            "params": {"k": random.randint(0, 9)} if name != "position_sample" else None,
        })
    return json.dumps(events)


class Counter:
    def __init__(self):
        self.lock = threading.Lock()
        self.codes = {}
        self.latencies = []
        self.errors = 0

    def record(self, code, latency):
        with self.lock:
            self.codes[code] = self.codes.get(code, 0) + 1
            self.latencies.append(latency)

    def error(self):
        with self.lock:
            self.errors += 1

    def snapshot_and_reset(self):
        with self.lock:
            snap = (dict(self.codes), list(self.latencies), self.errors)
            self.codes, self.latencies, self.errors = {}, [], 0
            return snap


def percentile(values, p):
    if not values:
        return float("nan")
    values = sorted(values)
    k = max(0, min(len(values) - 1, round(p / 100 * (len(values) - 1))))
    return values[k]


def sender(base, rps, seconds, stop, counter, users, batch_size):
    """한 워커. rps 만큼 초당 보내려고 다음 발사 시각을 계산해 잠듭니다."""
    conn = connect(base)
    session_id = str(uuid.uuid4())
    match_id = str(uuid.uuid4())
    seq = 0
    interval = 1.0 / rps if rps > 0 else None
    next_at = time.perf_counter()
    end = time.perf_counter() + seconds
    while not stop.is_set() and time.perf_counter() < end and interval is not None:
        body = make_batch(session_id, seq, match_id, users, batch_size)
        seq += batch_size
        if seq % 3000 == 0:
            match_id = str(uuid.uuid4())
        t0 = time.perf_counter()
        try:
            conn.request("POST", "/api/v1/events", body=body, headers={"Content-Type": "application/json"})
            resp = conn.getresponse()
            resp.read()
            counter.record(resp.status, time.perf_counter() - t0)
        except Exception:
            counter.error()
            try:
                conn.close()
            except Exception:
                pass
            conn = connect(base)
        next_at += interval
        delay = next_at - time.perf_counter()
        if delay > 0:
            time.sleep(delay)
        elif delay < -2:
            next_at = time.perf_counter()  # 너무 뒤처지면 따라잡기를 포기하고 현재부터 다시 셉니다.


def prober(base, user_id, stop, counter, hz):
    """게임 API 응답 시간. 부하와 무관하게 일정 주기로 찔러 p99 를 봅니다."""
    conn = connect(base)
    interval = 1.0 / hz
    while not stop.is_set():
        t0 = time.perf_counter()
        try:
            conn.request("GET", "/api/v1/accounts/me", headers={"X-User-Id": user_id})
            resp = conn.getresponse()
            resp.read()
            counter.record(resp.status, time.perf_counter() - t0)
        except Exception:
            counter.error()
            try:
                conn.close()
            except Exception:
                pass
            conn = connect(base)
        time.sleep(max(0.0, interval - (time.perf_counter() - t0)))


def issue_account(base):
    conn = connect(base)
    conn.request("POST", "/api/v1/accounts", body=json.dumps({"deviceId": str(uuid.uuid4())}),
                 headers={"Content-Type": "application/json"})
    resp = conn.getresponse()
    body = json.loads(resp.read())
    conn.close()
    return body["userId"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="http://localhost:8080")
    ap.add_argument("--stages", default="0,6,30,60,120,240")
    ap.add_argument("--stage-seconds", type=int, default=60)
    ap.add_argument("--batch", type=int, default=50)
    ap.add_argument("--probe-hz", type=float, default=5.0)
    ap.add_argument("--workers", type=int, default=24)
    ap.add_argument("--out", default="loadtest/result.json")
    args = ap.parse_args()

    if "ssafy.io" in args.base:
        sys.exit("운영 서버에는 돌리지 않습니다.")

    users = [str(uuid.uuid4()) for _ in range(60)]
    user_id = issue_account(args.base)
    stages = [int(s) for s in args.stages.split(",")]
    results = []

    print(f"기준: 요청 1건 = 이벤트 {args.batch}건. 예상 운영 부하 6 req/s. 단계당 {args.stage_seconds}초.")
    print(f"{'req/s':>6} {'events/s':>9} | {'202':>7} {'429':>5} {'기타':>5} {'오류':>5} {'ingest p99':>11} | {'게임 p50':>9} {'게임 p99':>9} {'게임 max':>9} {'게임 오류':>8}")

    for rps in stages:
        ingest, game = Counter(), Counter()
        stop = threading.Event()
        threads = [threading.Thread(target=prober, args=(args.base, user_id, stop, game, args.probe_hz), daemon=True)]
        if rps > 0:
            workers = min(args.workers, rps)
            per_worker = rps / workers
            for _ in range(workers):
                threads.append(threading.Thread(
                    target=sender, args=(args.base, per_worker, args.stage_seconds, stop, ingest, users, args.batch),
                    daemon=True))
        for t in threads:
            t.start()
        time.sleep(args.stage_seconds)
        stop.set()
        for t in threads:
            t.join(timeout=15)

        codes, lat, errs = ingest.snapshot_and_reset()
        gcodes, glat, gerrs = game.snapshot_and_reset()
        ok = codes.get(202, 0)
        rate_limited = codes.get(429, 0)
        other = sum(v for k, v in codes.items() if k not in (202, 429))
        game_errors = gerrs + sum(v for k, v in gcodes.items() if k != 200)
        row = {
            "rps": rps, "events_per_s": rps * args.batch,
            "accepted": ok, "rate_limited": rate_limited, "other": other, "send_errors": errs,
            "achieved_rps": round(ok / args.stage_seconds, 1),
            "ingest_p99_ms": round(percentile(lat, 99) * 1000, 1) if lat else None,
            "game_p50_ms": round(percentile(glat, 50) * 1000, 1),
            "game_p99_ms": round(percentile(glat, 99) * 1000, 1),
            "game_max_ms": round(max(glat) * 1000, 1) if glat else None,
            "game_errors": game_errors,
        }
        results.append(row)
        print(f"{rps:>6} {rps * args.batch:>9} | {ok:>7} {rate_limited:>5} {other:>5} {errs:>5} "
              f"{(row['ingest_p99_ms'] if row['ingest_p99_ms'] is not None else '-'):>11} | "
              f"{row['game_p50_ms']:>9} {row['game_p99_ms']:>9} {row['game_max_ms']:>9} {game_errors:>8}")
        sys.stdout.flush()
        time.sleep(3)  # 다음 단계 전에 큐가 비도록 잠깐 쉽니다.

    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=2)
    print(f"결과 저장: {args.out}")


if __name__ == "__main__":
    main()
