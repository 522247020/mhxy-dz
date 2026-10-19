using System;
using System.Drawing;
using System.IO;
using System.Threading;
using MeditationMini;

internal static class SelfTest
{
    private static int _failures;

    private static void Check(string name, bool condition)
    {
        Console.WriteLine((condition ? "ok   " : "FAIL ") + name);
        if (!condition) _failures++;
    }

    private static void Main()
    {
        // 倒计时文字
        Check("format 180s", MeditationTimer.Format(180000) == "180");
        Check("format 1.5s rounds up", MeditationTimer.Format(1500) == "2");
        Check("format 0", MeditationTimer.Format(0) == "0");
        Check("format negative", MeditationTimer.Format(-500) == "0");
        Check("clock 180s", MeditationTimer.FormatClock(180000) == "03:00");
        Check("clock 1.5s rounds up", MeditationTimer.FormatClock(1500) == "00:02");
        Check("clock 0", MeditationTimer.FormatClock(0) == "00:00");

        // 状态机
        MeditationTimer timer = new MeditationTimer(180);
        Check("initial idle", timer.State == MeditationTimer.StateIdle);
        Check("initial text", timer.Text() == "180");
        Check("initial progress", Math.Abs(timer.Progress - 1f) < 0.001f);

        timer.Restart();
        Check("running after restart", timer.IsRunning);
        Thread.Sleep(1100);
        timer.Tick();
        int remaining = timer.RemainingMs();
        Check("counts down", remaining < 179500 && remaining > 178000);
        Check("text follows", timer.Text() == MeditationTimer.Format(remaining));

        timer.Pause();
        Check("paused", timer.State == MeditationTimer.StatePaused);
        int frozen = timer.RemainingMs();
        Thread.Sleep(300);
        timer.Tick();
        Check("pause freezes", timer.RemainingMs() == frozen);
        timer.Resume();
        Check("resumed", timer.IsRunning);

        timer.SetDuration(1);
        Check("set duration resets", timer.State == MeditationTimer.StateIdle);
        timer.Restart();
        Thread.Sleep(1200);
        timer.Tick();
        Check("finishes", timer.State == MeditationTimer.StateFinished);
        Check("finished text is 0", timer.Text() == "0");
        Check("finished progress", Math.Abs(timer.Progress) < 0.001f);
        timer.Tick();
        Check("finish is idempotent", timer.State == MeditationTimer.StateFinished);

        // 快捷键换算
        uint modifiers;
        uint key;
        Check("parse F9", Hotkey.TryParse("F9", out modifiers, out key) && key == 0x78 && modifiers == Hotkey.ModNoRepeat);
        Check("parse ctrl+alt+f9", Hotkey.TryParse("Ctrl+Alt+F9", out modifiers, out key)
            && key == 0x78 && modifiers == (Hotkey.ModNoRepeat | Hotkey.ModControl | Hotkey.ModAlt));
        Check("parse bad", !Hotkey.TryParse("Ctrl+", out modifiers, out key));
        Check("text roundtrip", Hotkey.ToText(Hotkey.ModNoRepeat | Hotkey.ModControl | Hotkey.ModAlt, 0x78) == "Ctrl+Alt+F9");

        // 配置读写
        string path = Path.Combine(Path.GetTempPath(), "med-selftest.ini");
        if (!File.Exists(path)) File.WriteAllText(path, "");
        OverlaySettings settings = OverlaySettings.Load(path);
        settings.Opacity = 0.4f;
        settings.FontPx = 28;
        settings.TextColor = Color.FromArgb(255, 251, 191, 36);
        settings.Outline = "#111111";
        settings.DurationSeconds = 300;
        settings.Hotkey = "Ctrl+Alt+F9";
        settings.TimeFormat = OverlaySettings.TimeClock;
        settings.Locked = true;
        settings.OnTop = false;
        settings.Bounds = new Rectangle(120, 240, 176, 54);
        settings.Save();
        OverlaySettings reopened = OverlaySettings.Load(path);
        Check("opacity persists", Math.Abs(reopened.Opacity - 0.4f) < 0.001f);
        Check("font persists", reopened.FontPx == 28);
        Check("color persists", OverlaySettings.ToHex(reopened.TextColor) == "#fbbf24");
        Check("outline persists", reopened.Outline == "#111111");
        Check("duration persists", reopened.DurationSeconds == 300);
        Check("hotkey persists", reopened.Hotkey == "Ctrl+Alt+F9");
        Check("on top persists", !reopened.OnTop);
        Check("time format persists", reopened.TimeFormat == OverlaySettings.TimeClock);
        Check("lock persists", reopened.Locked);
        Check("bounds persists", reopened.Bounds.X == 120 && reopened.Bounds.Y == 240);

        // 行内注释：示例配置里就是这么写的，颜色里的 # 不能被当成注释
        string commented = Path.Combine(Path.GetTempPath(), "med-selftest-comment.ini");
        File.WriteAllText(commented, "opacity=0.5   # 半透明\nfont_px=20  ; 字号\ntext_color=#ff0000\noutline=none\n");
        OverlaySettings parsed = OverlaySettings.Load(commented);
        Check("inline comments", Math.Abs(parsed.Opacity - 0.5f) < 0.001f && parsed.FontPx == 20);
        Check("color not treated as comment", OverlaySettings.ToHex(parsed.TextColor) == "#ff0000");
        File.Delete(commented);

        Check("outline normalize auto", OverlaySettings.NormalizeOutline("bogus") == OverlaySettings.OutlineAuto);
        Check("outline normalize none", OverlaySettings.NormalizeOutline("NONE") == OverlaySettings.OutlineNone);
        Check("outline normalize hex", OverlaySettings.NormalizeOutline("#ABCDEF") == "#abcdef");
        Check("time format normalize", OverlaySettings.NormalizeTimeFormat("CLOCK") == OverlaySettings.TimeClock && OverlaySettings.NormalizeTimeFormat("bogus") == OverlaySettings.TimeSeconds);

        File.Delete(path);

        Console.WriteLine(_failures == 0 ? "ALL PASS" : (_failures + " FAILURES"));
        Environment.ExitCode = _failures == 0 ? 0 : 1;
    }
}




