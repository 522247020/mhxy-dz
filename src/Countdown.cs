using System;
using System.Diagnostics;
using System.Globalization;

namespace MeditationMini
{
    /// <summary>
    /// 打坐倒计时的状态机，和 Python 版 meditation.py 的行为一致：
    /// 毫秒精度、单调时钟、结束时停在 00:00 不切中文。
    /// </summary>
    public sealed class MeditationTimer
    {
        public const string StateIdle = "idle";
        public const string StateRunning = "running";
        public const string StatePaused = "paused";
        public const string StateFinished = "finished";

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private int _duration;
        private int _remainingMs;
        private long _endsAtMs;
        private string _state;

        public MeditationTimer(int durationSeconds)
        {
            _duration = Math.Max(1, durationSeconds);
            _remainingMs = _duration * 1000;
            _state = StateIdle;
        }

        public int Duration
        {
            get { return _duration; }
        }

        public string State
        {
            get { return _state; }
        }

        public bool IsRunning
        {
            get { return _state == StateRunning; }
        }

        public bool IsFinished
        {
            get { return _state == StateFinished; }
        }

        /// <summary>剩余比例 0~1，给边框进度环用。</summary>
        public float Progress
        {
            get
            {
                float value = (float)_remainingMs / (_duration * 1000f);
                if (value < 0f) return 0f;
                if (value > 1f) return 1f;
                return value;
            }
        }

        public void SetDuration(int seconds)
        {
            _duration = Math.Max(1, seconds);
            Reset();
        }

        public void Reset()
        {
            _state = StateIdle;
            _remainingMs = _duration * 1000;
        }

        public void Restart()
        {
            _remainingMs = _duration * 1000;
            _endsAtMs = _clock.ElapsedMilliseconds + _remainingMs;
            _state = StateRunning;
        }

        public void Pause()
        {
            if (_state != StateRunning) return;
            _remainingMs = ComputeRemaining();
            _state = StatePaused;
        }

        public void Resume()
        {
            if (_state != StatePaused) return;
            _endsAtMs = _clock.ElapsedMilliseconds + _remainingMs;
            _state = StateRunning;
        }

        public int RemainingMs()
        {
            return _state == StateRunning ? ComputeRemaining() : _remainingMs;
        }

        /// <summary>刷新剩余时间；归零时切到 finished，重复调用不会重复结束。</summary>
        public void Tick()
        {
            if (_state != StateRunning) return;
            _remainingMs = ComputeRemaining();
            if (_remainingMs <= 0)
            {
                _remainingMs = 0;
                _state = StateFinished;
            }
        }

        public string Text()
        {
            return Format(RemainingMs());
        }

        /// <summary>毫秒转剩余秒数（只显示数字），不足一秒向上取整。</summary>
        public static string Format(int milliseconds)
        {
            return ((int)Math.Ceiling(Math.Max(0, milliseconds) / 1000.0)).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>毫秒转 MM:SS，同样向上取整，避免提前跳到 00:00。</summary>
        public static string FormatClock(int milliseconds)
        {
            int total = (int)Math.Ceiling(Math.Max(0, milliseconds) / 1000.0);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", total / 60, total % 60);
        }

        private int ComputeRemaining()
        {
            long left = _endsAtMs - _clock.ElapsedMilliseconds;
            if (left < 0) return 0;
            if (left > int.MaxValue) return int.MaxValue;
            return (int)left;
        }
    }
}


