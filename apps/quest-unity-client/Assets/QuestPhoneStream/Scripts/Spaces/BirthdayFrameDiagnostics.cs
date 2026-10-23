using System;
using UnityEngine;

namespace QuestPhoneStream.Spaces
{
    /// <summary>
    /// Birthday-scene-local B2.8 render-loop monitor. Uses one fixed histogram,
    /// does not read camera/room data, create textures, write files or retain scans.
    /// Counts Unity Update cadence, NOT compositor FPS or GPU execution duration.
    /// Only reports samples collected while Unity is actually in the foreground.
    /// </summary>
    public sealed class BirthdayFrameDiagnostics : MonoBehaviour
    {
        public const float WindowSeconds = 10f;
        const int HistogramSize = 101; // whole milliseconds, 100 = >=100ms
        readonly int[] _milliseconds = new int[HistogramSize];
        int _frames, _overBudget, _longFrames;
        float _elapsed, _totalMs, _maxMs;
        public int ReportCount { get; private set; }
        public float LastFrameRate { get; private set; }
        public float LastP95Ms { get; private set; }
        public float LastAverageMs { get; private set; }
        public float LastWorstMs { get; private set; }
        public int LastOverBudget { get; private set; }
        public int LastLongFrames { get; private set; }

        public void AddSample(float deltaSeconds, bool foreground)
        {
            if (!foreground || deltaSeconds <= 0f || deltaSeconds >= .5f ||
                float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            {
                ResetWindow();
                return;
            }
            float ms = deltaSeconds * 1000f;
            _milliseconds[Mathf.Clamp(Mathf.FloorToInt(ms),0,HistogramSize-1)]++;
            _elapsed += deltaSeconds;
            _frames++;
            _totalMs += ms;
            _maxMs = Mathf.Max(_maxMs,ms);
            if (ms > 1000f/72f) _overBudget++;
            if (ms > 33.5f) _longFrames++; // tolerance at exact 30 FPS
            if (_elapsed < WindowSeconds || _frames == 0) return;
            LastFrameRate = _frames/_elapsed;
            LastAverageMs = _totalMs/_frames;
            LastWorstMs = _maxMs;
            LastOverBudget = _overBudget;
            LastLongFrames = _longFrames;
            int p95Index = Mathf.CeilToInt(_frames*.95f), visited=0;
            LastP95Ms = 100f;
            for(int i=0;i<_milliseconds.Length;i++)
            {
                visited += _milliseconds[i];
                if(visited < p95Index) continue;
                LastP95Ms = i + .5f;
                break;
            }
            ReportCount++;
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("[BirthdayPerf] UnityUpdateFPS="+LastFrameRate.ToString("F1")+
                " avgMs="+LastAverageMs.ToString("F2")+
                " p95Ms="+LastP95Ms.ToString("F1")+
                " worstMs="+LastWorstMs.ToString("F1")+
                " over13_9ms="+LastOverBudget+" long33ms="+LastLongFrames+
                " frames="+_frames+
                " (render loop only, not compositor/GPU FPS)");
#endif
            ResetWindow();
        }
        void Update()
        {
            // Do not include Meta lock screen / background / pause in app FPS.
            AddSample(Time.unscaledDeltaTime,Application.isFocused && !Application.isBatchMode);
        }
        void OnApplicationPause(bool paused) { if(paused)ResetWindow(); }
        void OnApplicationFocus(bool focused) { if(!focused)ResetWindow(); }
        void OnDisable(){ResetWindow();}
        public void ResetWindow()
        {
            Array.Clear(_milliseconds,0,_milliseconds.Length);
            _frames=0;_overBudget=0;_longFrames=0;
            _elapsed=0;_totalMs=0;_maxMs=0;
        }
    }
}
