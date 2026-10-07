using System.Linq;
using Coffee.UIExtensions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Собирает эффект летящих камней по образцу уже существующего эффекта монет.
//
// Руками в редакторе это были бы те же действия: продублировать "Coins Fly Effect"
// под кнопку бесплатных камней, подменить материал частиц на текстуру камня,
// повесить CoinRewardAnimator на счётчик камней и проставить ссылки.
//
// Запускать повторно безопасно: скрипт переиспользует то, что уже собрано.
public static class StonesFlyEffectSetup
{
    const string ShopPrefabPath = "Assets/Prefabs/UI/Menu/Panel Shop.prefab";
    const string StonesViewPrefabPath = "Assets/Prefabs/UI/Stones Curency View.prefab";
    const string MenuPrefabPath = "Assets/Prefabs/UI/Menu/Menu Portrait.prefab";

    const string CoinsMaterialPath = "Assets/Mats/CoinsParticle.mat";
    const string StonesMaterialPath = "Assets/Mats/StonesParticle.mat";
    const string StoneTexturePath = "Assets/Sprites/Increase Stone.psd";

    const string EffectName = "Stones Fly Effect";

    // Доля времени жизни, которую частица летит свободно, прежде чем её потянет
    const float AttractorDelay = 0.1f;

    [MenuItem("Tools/Enchant Dice/Собрать эффект летящих камней")]
    public static void Build()
    {
        var material = EnsureStonesMaterial();
        if (!material)
            return;

        if (!BuildFlyEffect(material, out var attractorMaxSpeed))
            return;

        if (!BuildCounterAnimator(attractorMaxSpeed))
            return;

        // Menu ссылается на аниматор внутри вложенного префаба счётчика,
        // поэтому тот должен быть записан до того, как мы откроем Menu
        AssetDatabase.SaveAssets();

        if (!WireMenu())
            return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Камни] Готово. Проверь эффект в Panel Shop и, если нужно, подправь поворот Particle System.");
    }

    // --- материал ---------------------------------------------------------

    static Material EnsureStonesMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(StonesMaterialPath);
        if (existing)
        {
            Debug.Log($"[Камни] Материал уже есть: {StonesMaterialPath}");
            return existing;
        }

        if (!AssetDatabase.LoadAssetAtPath<Material>(CoinsMaterialPath))
        {
            Debug.LogError($"[Камни] Не найден материал монет: {CoinsMaterialPath}");
            return null;
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture>(StoneTexturePath);
        if (!texture)
        {
            Debug.LogError($"[Камни] Не найдена текстура камня: {StoneTexturePath}");
            return null;
        }

        if (!AssetDatabase.CopyAsset(CoinsMaterialPath, StonesMaterialPath))
        {
            Debug.LogError("[Камни] Не удалось скопировать материал монет");
            return null;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(StonesMaterialPath);
        material.mainTexture = texture;
        EditorUtility.SetDirty(material);

        Debug.Log($"[Камни] Создан материал {StonesMaterialPath} с текстурой {texture.name}");
        return material;
    }

    // --- эффект в Panel Shop ---------------------------------------------

    static bool BuildFlyEffect(Material material, out float attractorMaxSpeed)
    {
        attractorMaxSpeed = 0f;

        var root = PrefabUtility.LoadPrefabContents(ShopPrefabPath);
        try
        {
            var shop = root.GetComponentInChildren<PanelShop>(true);
            if (!shop)
            {
                Debug.LogError($"[Камни] В {ShopPrefabPath} нет компонента PanelShop");
                return false;
            }

            var so = new SerializedObject(shop);

            var coinsEffect = so.FindProperty("flyCoinsEffect").objectReferenceValue as ParticleSystem;
            var btnStones = so.FindProperty("btnFreeStones").objectReferenceValue as Button;

            if (!coinsEffect)
            {
                Debug.LogError("[Камни] У PanelShop не проставлен flyCoinsEffect — не с чего брать образец");
                return false;
            }

            if (!btnStones)
            {
                Debug.LogError("[Камни] У PanelShop не проставлен btnFreeStones");
                return false;
            }

            // Система частиц лежит внутри объекта с UIParticle, копируем именно его
            var source = coinsEffect.transform.parent;
            if (!source)
            {
                Debug.LogError("[Камни] У эффекта монет нет родителя с UIParticle");
                return false;
            }

            Debug.Log($"[Камни] Образец: {Path(source)}");

            var existing = btnStones.transform.Find(EffectName);
            GameObject copy;

            if (existing)
            {
                copy = existing.gameObject;
                Debug.Log("[Камни] Эффект уже собран, обновляю его");
            }
            else
            {
                copy = Object.Instantiate(source.gameObject, btnStones.transform);
                copy.name = EffectName;
                Debug.Log($"[Камни] Создан {Path(copy.transform)}");
            }

            CopyRectTransform(source as RectTransform, copy.transform as RectTransform);

            var renderers = copy.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var renderer in renderers)
                renderer.sharedMaterial = material;

            Debug.Log($"[Камни] Материал проставлен у {renderers.Length} рендерер(ов)");

            var particles = copy.GetComponentInChildren<ParticleSystem>(true);
            if (!particles)
            {
                Debug.LogError("[Камни] В копии не нашлась система частиц");
                return false;
            }

            // Эффект монет летит по прямой в мировом пространстве: направление задано
            // поворотом и подогнано под положение счётчика золота. У камней и кнопка,
            // и счётчик стоят в других местах, поэтому направление считает аттрактор.
            // Время жизни держим таким же, как у монет — от него зависит скорость
            var coinsMain = coinsEffect.main;
            var main = particles.main;
            main.startLifetime = coinsMain.startLifetime;

            // Скорость аттрактора задаётся в единицах за кадр при 60 fps, а не в
            // единицах за секунду. В режиме Linear получается ровно
            // maxSpeed * 60 / duration единиц в секунду, где duration — время жизни
            // за вычетом свободного полёта. Приравниваем это к startSpeed монет,
            // чтобы камни летели с той же скоростью
            var lifetime = coinsMain.startLifetime.constant;
            var duration = lifetime - lifetime * AttractorDelay;
            var coinsSpeed = coinsMain.startSpeed.constant;

            attractorMaxSpeed = coinsSpeed * duration / 60f;

            Debug.Log($"[Камни] Скорость монет {coinsSpeed} ед/сек при времени жизни {lifetime} " +
                      $"=> maxSpeed аттрактора {attractorMaxSpeed:F3}");

            so.FindProperty("flyStonesEffect").objectReferenceValue = particles;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ShopPrefabPath);
            Debug.Log("[Камни] Panel Shop сохранён, flyStonesEffect проставлен");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void CopyRectTransform(RectTransform from, RectTransform to)
    {
        if (!from || !to)
            return;

        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.anchoredPosition3D = from.anchoredPosition3D;
        to.sizeDelta = from.sizeDelta;
        to.localRotation = from.localRotation;
        to.localScale = from.localScale;
    }

    // --- счётчик камней ---------------------------------------------------

    static bool BuildCounterAnimator(float attractorMaxSpeed)
    {
        var root = PrefabUtility.LoadPrefabContents(StonesViewPrefabPath);
        try
        {
            var animator = root.GetComponentInChildren<CoinRewardAnimator>(true);
            if (!animator)
            {
                animator = root.AddComponent<CoinRewardAnimator>();
                Debug.Log($"[Камни] На {root.name} добавлен CoinRewardAnimator");
            }

            var text = root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
            if (!text)
            {
                Debug.LogError($"[Камни] В {StonesViewPrefabPath} не нашёлся TMP_Text со значением");
                return false;
            }

            // Иконку берём с объекта Icon, как у счётчика монет
            var icon = root.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i.name == "Icon")
                ?? root.GetComponentsInChildren<Image>(true).FirstOrDefault();

            if (!icon)
            {
                Debug.LogError($"[Камни] В {StonesViewPrefabPath} не нашлась иконка (Image)");
                return false;
            }

            animator.coinText = text;
            animator.coinIcon = icon;

            // Те же настройки пульсации, что у счётчика монет
            animator.pulseScale = 1.2f;
            animator.pulseDuration = 0.2f;
            animator.minPulses = 3;
            animator.maxPulses = 7;

            Debug.Log($"[Камни] Аниматор счётчика: текст '{text.name}', иконка '{icon.name}'");

            // Аттрактор тянет частицы к себе, то есть к счётчику. Список систем
            // оставляем пустым: система живёт в другом префабе, ссылку туда положить
            // нельзя, её подставляет Menu при инициализации
            var attractor = root.GetComponent<UIParticleAttractor>();
            if (!attractor)
            {
                attractor = root.AddComponent<UIParticleAttractor>();
                Debug.Log($"[Камни] На {root.name} добавлен UIParticleAttractor");
            }

            // Linear, потому что только он даёт постоянную скорость: Smooth разгоняет
            // частицу к концу жизни и она финиширует заметно быстрее монет
            attractor.movement = UIParticleAttractor.Movement.Linear;
            attractor.maxSpeed = attractorMaxSpeed;
            attractor.delay = AttractorDelay;
            attractor.destinationRadius = 1f;

            PrefabUtility.SaveAsPrefabAsset(root, StonesViewPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // --- ссылка в Menu ----------------------------------------------------

    static bool WireMenu()
    {
        var root = PrefabUtility.LoadPrefabContents(MenuPrefabPath);
        try
        {
            var menu = root.GetComponentInChildren<Menu>(true);
            if (!menu)
            {
                Debug.LogError($"[Камни] В {MenuPrefabPath} нет компонента Menu");
                return false;
            }

            var so = new SerializedObject(menu);

            var labelStones = so.FindProperty("labelStones").objectReferenceValue as TMP_Text;
            if (!labelStones)
            {
                Debug.LogError("[Камни] У Menu не проставлен labelStones");
                return false;
            }

            var animator = labelStones.GetComponentInParent<CoinRewardAnimator>(true);
            if (!animator)
            {
                Debug.LogError($"[Камни] Рядом с {Path(labelStones.transform)} нет CoinRewardAnimator");
                return false;
            }

            so.FindProperty("stonesAnim").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, MenuPrefabPath);
            Debug.Log($"[Камни] Menu.stonesAnim -> {Path(animator.transform)}");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string Path(Transform t)
    {
        var path = t.name;
        while (t.parent)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
