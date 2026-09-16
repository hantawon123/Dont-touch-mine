#!/bin/bash
# Only containers owned by this workspace; never touches game/backend containers.
set -euo pipefail
owner=$(printf '%s' "$(pwd -P)" | sha256sum | cut -c1-20)
label="d205.client-ci=$owner"
if [ "${1:-}" = cleanup ]; then
    ids=$(docker ps -aq --filter "label=$label")
    for id in $ids; do
        docker stop -t 10 "$id" >/dev/null || docker rm -f "$id" >/dev/null
    done
    exit 0
fi
name="d205-ci-$owner-$$"
cleanup() { docker stop -t 10 "$name" >/dev/null 2>&1 || true; }
trap cleanup EXIT
trap 'exit 143' TERM
trap 'exit 130' INT
docker run --rm --name "$name" --label "$label" "$@" &
wait $!
