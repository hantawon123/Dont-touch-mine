"""Compare two local 6-player baseline CSV runs using equal Searching windows."""
import argparse
import csv
import json
from pathlib import Path


def quantile(values, fraction):
    values = sorted(values)
    return values[round((len(values) - 1) * fraction)]


def load_run(path):
    manifest = json.loads((path / "manifest.json").read_text(encoding="utf-8-sig"))
    if manifest["count"] != 6:
        raise ValueError("Expected six participants")
    peers = []
    for peer in range(6 + (manifest["mode"] == "Server")):
        with (path / f"peer{peer}.csv").open(encoding="utf-8-sig", newline="") as stream:
            rows = [row for row in csv.DictReader(stream)
                    if row["phase"] == "2" and row["scene"] == "3" and row["peers"] == "6"]
        if not rows:
            raise ValueError(f"{path}: peer {peer} has no six-player Searching samples")
        start = float(rows[0]["seconds"]) + 10
        peers.append((start, rows))
    return manifest, peers


def summarize(start, rows, duration):
    rows = [row for row in rows if start <= float(row["seconds"]) <= start + duration]
    if len(rows) < 100:
        raise ValueError("Too few samples for a percentile comparison")
    frame = [float(row["frame_ms"]) for row in rows]
    fusion = [float(row["fusion_update_ms"]) for row in rows]
    rtt = [float(row["rtt_ms"]) for row in rows]
    return {
        "samples": len(rows),
        "frame_p95_ms": quantile(frame, .95),
        "frame_p99_ms": quantile(frame, .99),
        "frame_over_33ms_percent": 100 * sum(value > 33.333 for value in frame) / len(frame),
        "fusion_update_p95_ms": quantile(fusion, .95),
        "fusion_update_p99_ms": quantile(fusion, .99),
        "gc0_collections": int(rows[-1]["gc0"]) - int(rows[0]["gc0"]),
        "rtt_p95_ms": quantile(rtt, .95),
    }


def compare(before, after, allow_topology_change=False):
    left, right = load_run(before), load_run(after)
    for key in ("count", "mode", "resolution", "fpsCap", "graphicsPeer", "seconds"):
        if key == "mode" and allow_topology_change:
            continue
        if left[0][key] != right[0][key]:
            raise ValueError(f"Run conditions differ: {key}")
    duration = min(float(rows[-1]["seconds"]) - start
                   for run in (left, right) for start, rows in run[1])
    if duration < 30:
        raise ValueError("Need at least 30 seconds after Searching warm-up")
    output = {"window_seconds": duration, "warmup_seconds": 10,
              "conditions": {
                  path.name: {"mode": run[0]["mode"], "processes": len(run[1]), "players": 6}
                  for path, run in ((before, left), (after, right))}, "runs": {}}
    for path, run in ((before, left), (after, right)):
        output["runs"][path.name] = {
            f"peer{peer}": summarize(start, rows, duration)
            for peer, (start, rows) in enumerate(run[1])}
    return output


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before", type=Path)
    parser.add_argument("after", type=Path)
    parser.add_argument("--allow-topology-change", action="store_true",
                        help="Allow Host/Server comparison; process counts differ on the same PC")
    args = parser.parse_args()
    print(json.dumps(compare(args.before, args.after, args.allow_topology_change), indent=2))
