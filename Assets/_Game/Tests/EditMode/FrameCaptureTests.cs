using System;
using Game.Client.Common;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class FrameCaptureTests
    {
        [Test] public void Summary_UsesElapsedTimeAndOnlyRecordedSamples()
        {
            var values = new float[] { 10, 5, 20, 100, 999 };
            var report = new WebFrameCapture.Report();
            WebFrameCapture.Summarize(values, 4, .135, report);
            Assert.That(report.averageFps, Is.EqualTo(4 / .135).Within(.0001));
            Assert.That(report.p50ms, Is.EqualTo(10));
            Assert.That(report.p95ms, Is.EqualTo(100));
            Assert.That(report.p99ms, Is.EqualTo(100));
            Assert.That(report.maxMs, Is.EqualTo(100));
            Assert.That(report.overBudget120, Is.EqualTo(3));
            Assert.That(report.overBudget120Percent, Is.EqualTo(75));
            Assert.That(report.slow50ms, Is.EqualTo(1));
        }

        [Test] public void Summary_Separates120BudgetFromLargeStalls()
        {
            var report = new WebFrameCapture.Report();
            WebFrameCapture.Summarize(new float[] { 8, 8.34f, 16.67f, 50, 51 }, 5, .13401, report);
            Assert.That(report.overBudget120, Is.EqualTo(4));
            Assert.That(report.slow50ms, Is.EqualTo(1));
        }

        [Test] public void Summary_RejectsEmptyCapture()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WebFrameCapture.Summarize(Array.Empty<float>(), 0, 0, new WebFrameCapture.Report()));
        }
    }
}
