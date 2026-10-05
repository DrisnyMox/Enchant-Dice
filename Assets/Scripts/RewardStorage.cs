using System.Collections.Generic;
using UnityEngine;

// Хранилище кулдаунов дневных наград и прогресса рекламных кнопок.
//
// На WebGL лежит в облачном сейве Яндекса — там же, где монеты и камни.
// Раньше всё это жило в PlayerPrefs, то есть в IndexedDB браузера: инкогнито,
// другой браузер, другое устройство или очистка данных сайта сбрасывали кулдаун,
// а начисленная валюта оставалась на аккаунте — дневные награды фармились по кругу.
//
// На остальных платформах облака нет и сам прогресс тоже лежит в PlayerPrefs,
// поэтому там просто проксируем их.
public static class RewardStorage
{
    public static bool Has(string key) => Read(key) != null;

    public static string Read(string key)
    {
#if UNITY_WEBGL
        return Entries().Find(e => e.key == key)?.value;
#else
        return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
#endif
    }

    public static void Write(string key, string value)
    {
#if UNITY_WEBGL
        var entries = Entries();

        var entry = entries.Find(e => e.key == key);
        if (entry == null)
        {
            entry = new YG.SavesYG.RewardEntry { key = key };
            entries.Add(entry);
        }

        entry.value = value;

        YG.YandexGame.SaveProgress();
#else
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
#endif
    }

    public static void Delete(string key)
    {
#if UNITY_WEBGL
        Entries().RemoveAll(e => e.key == key);

        YG.YandexGame.SaveProgress();
#else
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
#endif
    }

    public static int ReadInt(string key, int fallback)
    {
        return int.TryParse(Read(key), out var value) ? value : fallback;
    }

    public static void WriteInt(string key, int value)
    {
        Write(key, value.ToString());
    }

    // Разовый перенос старых ключей из PlayerPrefs, чтобы на обновлении игроки
    // не получили лишний круг наград. intKeys писались через SetInt, поэтому
    // читаются отдельно — GetString на них вернул бы пустую строку.
    public static void MigrateFromPlayerPrefs(string[] stringKeys, string[] intKeys)
    {
#if UNITY_WEBGL
        var data = YG.YandexGame.savesData;

        if (data.rewardCooldownsMigrated)
            return;

        data.rewardCooldownsMigrated = true;

        foreach (var key in stringKeys)
        {
            if (PlayerPrefs.HasKey(key))
            {
                Write(key, PlayerPrefs.GetString(key));
                PlayerPrefs.DeleteKey(key);
            }
        }

        foreach (var key in intKeys)
        {
            if (PlayerPrefs.HasKey(key))
            {
                WriteInt(key, PlayerPrefs.GetInt(key));
                PlayerPrefs.DeleteKey(key);
            }
        }

        PlayerPrefs.Save();
        YG.YandexGame.SaveProgress();
#endif
    }

#if UNITY_WEBGL
    static List<YG.SavesYG.RewardEntry> Entries()
    {
        var data = YG.YandexGame.savesData;

        if (data.rewardCooldowns == null)
            data.rewardCooldowns = new List<YG.SavesYG.RewardEntry>();

        return data.rewardCooldowns;
    }
#endif
}
