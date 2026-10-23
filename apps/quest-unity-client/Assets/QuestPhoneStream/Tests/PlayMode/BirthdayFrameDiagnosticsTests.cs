using NUnit.Framework;
using UnityEngine;
using QuestPhoneStream.Spaces;

namespace QuestPhoneStream.Tests
{
    public sealed class BirthdayFrameDiagnosticsTests
    {
        [Test]
        public void ForegroundOnlyWindowHasExpectedFrameRateAndP95()
        {
            var go = new GameObject("BirthdayStatsTest");
            go.SetActive(false); // avoid automatic Update() during deterministic samples
            try
            {
                var stats=go.AddComponent<BirthdayFrameDiagnostics>();
                for(int i=0;i<720;i++)stats.AddSample(1f/72f,true);
                Assert.AreEqual(1,stats.ReportCount);
                Assert.That(stats.LastFrameRate,Is.InRange(71.9f,72.1f));
                Assert.That(stats.LastAverageMs,Is.InRange(13.7f,14.0f));
                Assert.That(stats.LastP95Ms,Is.InRange(13f,15f));
                Assert.That(stats.LastWorstMs,Is.LessThan(14f));
                stats.AddSample(1f/72f,false);
                for(int i=0;i<360;i++)stats.AddSample(1f/72f,true);
                Assert.AreEqual(1,stats.ReportCount,"Background reset must discard partial sample window");
                stats.AddSample(2f,true); // abnormal pause/resume delay also resets
                for(int i=0;i<720;i++)stats.AddSample(1f/72f,true);
                Assert.AreEqual(2,stats.ReportCount);
            }
            finally{Object.DestroyImmediate(go);}
        }
        [Test]
        public void SlowFramesDetectedWithoutMisrepresentingSystemFrameRate()
        {
            var go = new GameObject("BirthdaySlowFramesTest");go.SetActive(false);
            try
            {
                var stats=go.AddComponent<BirthdayFrameDiagnostics>();
                for(int i=0;i<301;i++)stats.AddSample(1f/30f,true);
                Assert.AreEqual(1,stats.ReportCount);
                Assert.That(stats.LastFrameRate,Is.InRange(29.9f,30.1f));
                Assert.AreEqual(300,stats.LastOverBudget);
                Assert.AreEqual(0,stats.LastLongFrames);
                Assert.That(stats.LastP95Ms,Is.InRange(33f,35f));
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
