// DailyRewardModule.cs — исправленная версия
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class DailyRewardModule
{
    public enum ResetMode { FixedInterval, DailyUtcReset }

    public class RewardConfig
    {
        public string Key;
        public ResetMode Mode;
        public TimeSpan Interval;
    }

    private static readonly Dictionary<string, RewardConfig> _configs = new Dictionary<string, RewardConfig>();
    private const string PrefsPrefix = "DailyReward_";
    private const string TimeFormat = "o";

    // Где лежат метки времени. По умолчанию PlayerPrefs, но проект может подменить
    // хранилище на облачный сейв — иначе кулдаун сбрасывается сменой браузера
    // или устройства, а выданная награда остаётся у игрока
    public static Func<string, string> ReadValue =
        key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;

    public static Action<string, string> WriteValue = (key, value) =>
    {
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
    };

    public static Action<string> DeleteValue = key =>
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    };

    public static string StorageKey(string key) => PrefsPrefix + key;

    // Снимает кулдаун со всех зарегистрированных наград. Нужно для отладки:
    // иначе дневную награду не проверить до следующей полуночи UTC
    public static void ResetAll()
    {
        foreach (var key in _configs.Keys)
            DeleteValue(StorageKey(key));
    }

    public static void RegisterReward(string key, ResetMode mode, TimeSpan interval = default)
    {
        // allow re-registration (update) to avoid silent misconfigurations
        var config = new RewardConfig { Key = key, Mode = mode, Interval = interval };
        _configs[key] = config;
    }

    public static bool CanClaim(string key)
    {
        if (!_configs.TryGetValue(key, out var config))
        {
            Debug.LogError($"Reward '{key}' is not registered.");
            return false;
        }

        DateTime last = ReadLastClaimTime(key);

        // If never claimed -> available
        if (last == DateTime.MinValue) return true;

        DateTime next = GetNextResetTime(last, config);
        return DateTime.UtcNow >= next;
    }

    public static bool Claim(string key, Action onClaimed)
    {
        if (!CanClaim(key)) return false;

        onClaimed?.Invoke();

        string now = DateTime.UtcNow.ToString(TimeFormat, CultureInfo.InvariantCulture);
        WriteValue(StorageKey(key), now);
        return true;
    }

    public static TimeSpan GetTimeRemaining(string key)
    {
        if (!_configs.TryGetValue(key, out var config))
        {
            Debug.LogError($"Reward '{key}' is not registered.");
            return TimeSpan.Zero;
        }

        DateTime last = ReadLastClaimTime(key);

        // If never claimed -> zero remaining (available now)
        if (last == DateTime.MinValue) return TimeSpan.Zero;

        DateTime next = GetNextResetTime(last, config);
        var remaining = next - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static DateTime ReadLastClaimTime(string key)
    {
        string stored = ReadValue(StorageKey(key));
        if (string.IsNullOrEmpty(stored)) return DateTime.MinValue;

        if (DateTime.TryParseExact(stored, TimeFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var last))
        {
            return last;
        }

        return DateTime.MinValue;
    }

    private static DateTime GetNextResetTime(DateTime lastClaim, RewardConfig config)
    {
        // If never claimed, treat as available (caller handles special-case), return MinValue
        if (lastClaim == DateTime.MinValue) return DateTime.MinValue;

        switch (config.Mode)
        {
            case ResetMode.FixedInterval:
                return lastClaim.Add(config.Interval);

            case ResetMode.DailyUtcReset:
                // Next reset is midnight (UTC) after the day of lastClaim.
                // Example: lastClaim 2025-10-20T15:00 -> next reset = 2025-10-21T00:00 UTC
                return lastClaim.ToUniversalTime().Date.AddDays(1);

            default:
                return DateTime.MinValue;
        }
    }
}
