using System;
using System.Linq;
using UnityEngine;

// The only door to automatic play (auto-build, auto-upgrade, auto-start, 4x). It opens solely when the
// executable is started with -smoketest. Every automated action must pass through Require(), which
// refuses (and logs an error) in normal play, so a normal session can never build anything by itself.
public static class Automation
{
    public const string Flag = "-smoketest";

    public static bool Enabled { get; private set; }

    public static bool RequestedBy(string[] args) => args != null && args.Contains(Flag);

    // Called once by SmokeTest's bootstrap. Tests may also enable it explicitly to exercise the bot.
    public static void Enable(string[] args)
    {
        Enabled = RequestedBy(args);
    }

    public static void EnableForTests(bool on) => Enabled = on;

    public static bool Require(string action)
    {
        if (Enabled) return true;
        Debug.LogError($"Automation: '{action}' bloqueado — ações automáticas só existem com {Flag}.");
        return false;
    }

    // Value after a command-line flag (e.g. -savepath X), or null.
    public static string Arg(string flag)
    {
        var a = CommandLine;
        int i = Array.IndexOf(a, flag);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }

    public static string[] CommandLine
    {
        get
        {
            try { return Environment.GetCommandLineArgs(); }
            catch { return Array.Empty<string>(); }
        }
    }
}
