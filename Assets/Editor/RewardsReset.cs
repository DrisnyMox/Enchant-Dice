using System.IO;
using UnityEditor;
using UnityEngine;
#if YG_NEWTONSOFT_FOR_SAVES
using Newtonsoft.Json;
#endif

// Сброс кулдаунов дневных наград для отладки.
//
// Кулдауны лежат в облачном сейве рядом с валютой, поэтому путей два:
// в Play mode правим живые данные и сразу перерисовываем панель магазина,
// вне Play mode — файл сейва редактора, который YG читает при старте игры.
public static class RewardsReset
{
    const string EditorSavePath = "/YandexGame/WorkingData/Editor/SavesEditorYG.json";

    [MenuItem("Tools/Enchant Dice/Сбросить дневные награды")]
    public static void Reset()
    {
        if (Application.isPlaying)
        {
            ResetInPlayMode();
            return;
        }

        ResetEditorSave();
    }

    static void ResetInPlayMode()
    {
        var shop = Object.FindObjectOfType<PanelShop>(true);

        if (shop)
        {
            shop.ResetRewards();
            Debug.Log("[Награды] Сброшено в Play mode, панель магазина обновлена");
            return;
        }

        // Ключи регистрирует и хранилище подменяет PanelShop.Init, поэтому без него
        // модулю нечего чистить — он смотрел бы не туда и молча ничего не сделал
        Debug.LogWarning("[Награды] На сцене нет PanelShop — сбрасывать нечего. " +
                         "Зайди в меню и повтори, либо выйди из Play mode: " +
                         "тогда пункт правит файл сейва редактора.");
    }

    static void ResetEditorSave()
    {
        var path = Application.dataPath + EditorSavePath;

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[Награды] Файл сейва не найден: {path}. " +
                             "Запусти игру в редакторе хотя бы раз, чтобы он появился");
            return;
        }

        var json = File.ReadAllText(path);

#if YG_NEWTONSOFT_FOR_SAVES
        var saves = JsonConvert.DeserializeObject<YG.SavesYG>(json);
#else
        var saves = JsonUtility.FromJson<YG.SavesYG>(json);
#endif

        if (saves == null)
        {
            Debug.LogError($"[Награды] Не удалось прочитать {path}");
            return;
        }

        var removed = saves.rewardCooldowns == null ? 0 : saves.rewardCooldowns.Count;

        saves.rewardCooldowns = new System.Collections.Generic.List<YG.SavesYG.RewardEntry>();

#if YG_NEWTONSOFT_FOR_SAVES
        File.WriteAllText(path, JsonConvert.SerializeObject(saves, Formatting.Indented));
#else
        File.WriteAllText(path, JsonUtility.ToJson(saves, true));
#endif

        AssetDatabase.Refresh();

        Debug.Log($"[Награды] Из сейва редактора убрано записей: {removed}. " +
                  "Награды снова доступны при следующем запуске игры");
    }
}
