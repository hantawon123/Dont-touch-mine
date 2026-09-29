using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Network.Session;
using Game.Server.Items;
using Game.Server.Match;
using UnityEngine;

// Measures the real serializer on synthetic frames, not Unity/Fusion gameplay.
const int iterations = 100;
const int warmups = 20;
var output = new List<object>();
foreach (var objectCount in new[] { 6, 32, 64 })
{
    var replay = MakeReplay(objectCount);
    var raw = HighlightReplaySerializer.Serialize(replay);
    var packed = HighlightReplaySerializer.SerializeCompressed(replay);
    if (!HighlightReplaySerializer.TryDeserializeCompressed(packed, out var restored) ||
        !raw.AsSpan().SequenceEqual(HighlightReplaySerializer.Serialize(restored)))
        throw new InvalidOperationException("Replay round-trip changed the payload.");
    if (HighlightReplaySerializer.TryDeserializeCompressed(new byte[] { 1, 2, 3 }, out _))
        throw new InvalidOperationException("Invalid compressed payload was accepted.");

    Action encode = () => GC.KeepAlive(HighlightReplaySerializer.SerializeCompressed(replay));
    Action decode = () =>
    {
        if (!HighlightReplaySerializer.TryDeserializeCompressed(packed, out var frames))
            throw new InvalidOperationException("Decode failed during measurement.");
        GC.KeepAlive(frames);
    };
    for (var i = 0; i < warmups; i++) { encode(); decode(); }
    for (var run = 1; run <= 3; run++)
        output.Add(new
        {
            objectCount, players = 6, highlights = 3, framesPerClip = 101,
            secondsPerClip = 10, run, rawBytes = raw.Length, compressedBytes = packed.Length,
            rawSha256 = Convert.ToHexString(SHA256.HashData(raw)),
            encode = Measure(encode), decode = Measure(decode)
        });
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    measuredAtUtc = DateTime.UtcNow,
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    cpuCount = Environment.ProcessorCount, iterations, warmups,
    scope = "Synthetic serializer microbenchmark; not Unity Mono/IL2CPP or a six-peer game",
    results = output
}, new JsonSerializerOptions { WriteIndented = true }));

static object Measure(Action operation)
{
    var elapsed = new double[iterations];
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    for (var i = 0; i < iterations; i++)
    {
        var start = Stopwatch.GetTimestamp();
        operation();
        elapsed[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
    Array.Sort(elapsed);
    return new { p50Ms = elapsed[49], p95Ms = elapsed[94], allocatedBytesPerOperation = allocated / iterations };
}

static HighlightReplayData[] MakeReplay(int objectCount)
{
    var random = new System.Random(574);
    var result = new HighlightReplayData[3];
    for (var clip = 0; clip < result.Length; clip++)
    {
        var segment = new HighlightSegment(clip * 10, (clip + 1) * 10);
        var candidate = new HighlightCandidate(HighlightType.FirstBlood, new[] { segment },
            "player-0", segment.EndedAt, 80, 0, 1);
        var frames = new HighlightReplayFrame[101];
        for (var f = 0; f < frames.Length; f++)
        {
            var players = new Pose[6];
            var actions = new HighlightPlayerAction[6];
            for (var p = 0; p < players.Length; p++)
            {
                players[p] = Position(random, p, f);
                actions[p] = f % 10 == 0 ? HighlightPlayerAction.Punching : HighlightPlayerAction.None;
            }
            var objects = new WorldObjectState[objectCount];
            for (var o = 0; o < objects.Length; o++)
                objects[o] = new WorldObjectState($"object-{o:D2}", Position(random, o, f));
            frames[f] = new HighlightReplayFrame(segment.StartedAt + f / 10d, players, objects, actions);
        }
        result[clip] = new HighlightReplayData(candidate, new[] { new HighlightReplayClip(segment, frames) });
    }
    return result;
}

static Pose Position(System.Random random, int index, int frame) => new(
    new Vector3(index + (float)random.NextDouble(), frame * 0.01f, (float)random.NextDouble() * 10),
    new Quaternion(0, 0, 0, 1));
