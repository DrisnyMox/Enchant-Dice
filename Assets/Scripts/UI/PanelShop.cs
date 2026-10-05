using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.Events;

public class PanelShop : MonoBehaviour
{
    [Header("БЕСПЛАТНЫЕ БОНУСЫ")]
    [SerializeField] TMP_Text freeBonusTitle;
    [SerializeField] Button btnFreeCoins;
    [SerializeField] Button btnFreeStones;
    [SerializeField] ParticleSystem flyCoinsEffect;

    [Header("МОНЕТКИ ЗА РЕКЛАМУ")]
    [SerializeField] BtnCoinsRewarded[] btnsCoinsRewarded;
    [SerializeField] ToastNotify coinsRewardNotifyPrefab;
    [SerializeField] Sprite coinSprite;
    [SerializeField] GameObject coinsRewardEffectPrefab;
    [SerializeField] GameObject coinsFlyEffectPrefab;
    [SerializeField] InfoPopup infoPopupPrefab;
    [SerializeField] TMP_Text rewardCoinsTitle;

    [Header("КАМНИ ЗА РЕКЛАМУ")]
    [SerializeField] Button btnStonesReward;
    [SerializeField] Sprite stoneSprite;

    [HideInInspector] public UnityEvent<float> onCoinsUpdate;

    const string rewardedCoinsKey = "rewardedCoins";
    const string coinsRewardIdxKey = "coinsRewardIdxKey";
    const string coinsRewardResetKey = "coinsRewardResetKey";// Нужен для сброса просмотренности рекламы

    const string coinsFreeKey = "coinsFreeKey";
    const string stonesFreeKey = "stonesFreeKey";

    const int REWARD_STONES_ID = 5;

    AvailableView btnFreeCoinsAvailable;
    AvailableView btnFreeStonesAvailable;

    string freeUnavailableMsg;

    bool coinsRewardsUnavailable;
    bool allCoinsRewardUnavailable;
    int rewardID;

    public void Init()
    {
        // Кулдауны должны жить там же, где валюта, иначе их сбрасывает смена браузера
        DailyRewardModule.ReadValue = RewardStorage.Read;
        DailyRewardModule.WriteValue = RewardStorage.Write;

        RewardStorage.MigrateFromPlayerPrefs
        (
            new[]
            {
                DailyRewardModule.StorageKey(rewardedCoinsKey),
                DailyRewardModule.StorageKey(coinsRewardResetKey),
                DailyRewardModule.StorageKey(coinsFreeKey),
                DailyRewardModule.StorageKey(stonesFreeKey),
            },
            new[] { coinsRewardIdxKey }
        );

        DailyRewardModule.RegisterReward(rewardedCoinsKey, DailyRewardModule.ResetMode.FixedInterval, TimeSpan.FromMinutes(30));
        DailyRewardModule.RegisterReward(coinsRewardResetKey, DailyRewardModule.ResetMode.DailyUtcReset);
        //DailyRewardModule.RegisterReward(coinsRewardResetKey, DailyRewardModule.ResetMode.FixedInterval, TimeSpan.FromMinutes(3));
        //DailyRewardModule.RegisterReward(coinsFreeKey, DailyRewardModule.ResetMode.FixedInterval, TimeSpan.FromSeconds(10));
        //DailyRewardModule.RegisterReward(stonesFreeKey, DailyRewardModule.ResetMode.FixedInterval, TimeSpan.FromMinutes(10));
        DailyRewardModule.RegisterReward(coinsFreeKey, DailyRewardModule.ResetMode.DailyUtcReset);
        DailyRewardModule.RegisterReward(stonesFreeKey, DailyRewardModule.ResetMode.DailyUtcReset);

        //DailyRewardModule.RegisterReward(coinsRewardResetKey, DailyRewardModule.ResetMode.DailyUtcReset);

        freeUnavailableMsg = Language.Rus 
            ? "Это ежедневная награда, ты сможешь снова получить её завтра"
            : "This is a daily reward, you can get it again tomorrow.";

        YG.YandexGame.RewardVideoEvent += RewardVideo_Watched;
        YG.YandexGame.CloseVideoEvent += RewarVideo_Closed;

        btnFreeCoins.onClick.AddListener(FreeCoins_Clicked);
        btnFreeStones.onClick.AddListener(FreeStones_Clicked);
        btnStonesReward.onClick.AddListener(RewardStones_Clicked);

        for (int i = 0; i < btnsCoinsRewarded.Length; i++)
        {
            var btnCoins = btnsCoinsRewarded[i];
            btnCoins.Init();
            btnCoins.onClick.AddListener(CoinsRewarded_Clicked);
        }

        btnFreeCoinsAvailable = btnFreeCoins.GetComponent<AvailableView>();
        btnFreeStonesAvailable = btnFreeStones.GetComponent<AvailableView>();

        CoinsRewardCheckAvailable();
    }

    private void RewardStones_Clicked()
    {
        btnStonesReward.GetComponent<AttentionAnim>().Play(ShowAd);

        static void ShowAd()
        {
#if UNITY_WEBGL && YG_PLUGIN_YANDEX_GAME
            YG.YandexGame.RewVideoShow(REWARD_STONES_ID);
#endif
        }
    }

    private void FreeStones_Clicked()
    {
        if (btnFreeStonesAvailable.state is AvailableView.State.Unavailable)
        {
            var attentionStr = Language.Rus ? "Внимание!" : "Attention!";
            InfoPopup.Show(infoPopupPrefab, attentionStr, new InfoPopup.InfoItemData(freeUnavailableMsg));
        }
        else
        {
            // Награду выдаёт сам модуль и только если она действительно доступна.
            // Раньше начисление стояло снаружи, и единственной защитой от повторной
            // выдачи было состояние кнопки
            if (!DailyRewardModule.Claim(stonesFreeKey, () => User.Data.countStones += 100))
                return;

            btnFreeStones.GetComponent<AttentionAnim>().Play();
            btnFreeStonesAvailable.Unavailable();
            Saver.Save();
        }
    }

    private void FreeCoins_Clicked()
    {
        var availableView = btnFreeCoins.GetComponent<AvailableView>();
        if (availableView.state is AvailableView.State.Unavailable)
        {
            var attentionStr = Language.Rus ? "Внимание!" : "Attention!";
            InfoPopup.Show(infoPopupPrefab, attentionStr, new InfoPopup.InfoItemData(freeUnavailableMsg));
        }
        else
        {
            if (!DailyRewardModule.Claim(coinsFreeKey, () => User.Data.golda += 100))
                return;

            btnFreeCoins.GetComponent<AttentionAnim>().Play();
            btnFreeCoinsAvailable.Unavailable();
            Saver.Save();
            flyCoinsEffect.Play();
            onCoinsUpdate?.Invoke(2.1f);
        }
    }

    private void RewardVideo_Watched(int adID)
    {

        if (adID == REWARD_STONES_ID)
        {
            ToastNotify.Show(coinsRewardNotifyPrefab, $"+{100}", stoneSprite);
            User.Data.countStones += 100;
            Saver.Save();
            return;
        }

        rewardID = adID;

        StartCoroutine(Delay());

        IEnumerator Delay()
        {
            yield return new WaitForSeconds(0.3f);

            Action doAvailables = () => { };

            var maxWatchedIdx = RewardStorage.ReadInt(coinsRewardIdxKey, 0);

            int reward = 0;
            if (rewardID == 0)
            {
                doAvailables = () =>
                {
                    btnsCoinsRewarded[0].Watched();
                    btnsCoinsRewarded[1].Available();
                };
                reward = 100;
            }

            if (rewardID == 1)
            {
                doAvailables = () =>
                {
                    btnsCoinsRewarded[1].Watched();
                    btnsCoinsRewarded[2].Available();
                };
                reward = 500;

            }

            if (rewardID == 2)
            {
                doAvailables = () =>
                {
                    btnsCoinsRewarded[2].Watched();
                };
                reward = 1000;
            }

            if (maxWatchedIdx <= rewardID)
            {
                maxWatchedIdx = rewardID;
                RewardStorage.WriteInt(coinsRewardIdxKey, rewardID);
            }

            CheckMaxWatchedIdx();

            User.Data.golda += reward;
            Saver.Save();

            DailyRewardModule.Claim(coinsRewardResetKey, null);

            onCoinsUpdate?.Invoke(3.1f);

            ToastNotify.Show(coinsRewardNotifyPrefab, $"+{reward}", coinSprite);
            Instantiate(coinsRewardEffectPrefab, transform);
            Instantiate(coinsFlyEffectPrefab, transform);

            yield return new WaitForSeconds(1.8f);

            if (!allCoinsRewardUnavailable)
            {
                doAvailables();
            }

            void CheckMaxWatchedIdx()
            {
                if (maxWatchedIdx == 2)
                {
                    DailyRewardModule.Claim(rewardedCoinsKey, AllCoinsRewardUnavailable);
                }
            }
        }
    }

    private void Update()
    {
        TimeSpan rem = DailyRewardModule.GetTimeRemaining(rewardedCoinsKey);
        coinsRewardsUnavailable = rem > TimeSpan.Zero;

        var getExtraCoins = Language.Rus 
            ? "Получи дополнительные монеты!" 
            : "Get extra coins!";
        rewardCoinsTitle.text = coinsRewardsUnavailable
            ? $"{rem.Hours:D2}:{rem.Minutes:D2}:{rem.Seconds:D2}"
            : getExtraCoins;

        if (allCoinsRewardUnavailable && !coinsRewardsUnavailable)
        {
            CoinsRewardCheckAvailable();
        }

        if (!allCoinsRewardUnavailable && coinsRewardsUnavailable)
        {
            AllCoinsRewardUnavailable();
        }

        // Проверка доступности
        if (DailyRewardModule.CanClaim(coinsRewardResetKey) && RewardStorage.Has(coinsRewardIdxKey))
        {
            RewardStorage.Delete(coinsRewardIdxKey);

            //DailyRewardModule.Claim(coinsRewardResetKey, null);

            if (!allCoinsRewardUnavailable)
            {
                CoinsRewardCheckAvailable();
            }
            print("доооо");
        }



        if (Input.GetKeyDown(KeyCode.J))
        {
            PlayerPrefs.DeleteKey(rewardedCoinsKey);
        }

        FreeBonusUpdate();
    }

    void FreeBonusUpdate()
    {
        if (DailyRewardModule.CanClaim(coinsFreeKey))
        {
            if (btnFreeCoinsAvailable.state is AvailableView.State.Unavailable)
            {
                btnFreeCoinsAvailable.Available();
                btnFreeCoins.GetComponent<AttentionAnim>().Play();
            }
        }
        else if (!(btnFreeCoinsAvailable.state is AvailableView.State.Unavailable))
        {
            btnFreeCoinsAvailable.Unavailable();
        }

        if (DailyRewardModule.CanClaim(stonesFreeKey))
        {
            if (btnFreeStonesAvailable.state is AvailableView.State.Unavailable)
            {
                btnFreeStonesAvailable.Available();
                btnFreeStones.GetComponent<AttentionAnim>().Play();
            }
        }
        else if (!(btnFreeStonesAvailable.state is AvailableView.State.Unavailable))
        {
            btnFreeStonesAvailable.Unavailable();
        }


        var unavailableTitle = Language.Rus ? "Сегодня уже всё получено" : "Everything has already been received today";
        if (btnFreeStonesAvailable.state is AvailableView.State.Unavailable && btnFreeCoinsAvailable.state is AvailableView.State.Unavailable)
        {
            freeBonusTitle.SetText(unavailableTitle);
        }
        else if (freeBonusTitle.text == unavailableTitle)
        {
            var freeLabel = Language.Rus ? "Забери просто так!" : "Take it away just like that!";
            freeBonusTitle.SetText(freeLabel);
        }
    }

    void CoinsRewardCheckAvailable()
    {
        allCoinsRewardUnavailable = false;

        for (int i = 0; i < btnsCoinsRewarded.Length; i++)
        {
            var btnCoins = btnsCoinsRewarded[i];


            if (!RewardStorage.Has(coinsRewardIdxKey))
            {
                if (i == 0)
                {
                    btnCoins.Available();
                }
                else
                {
                    btnCoins.Unavailable();
                }
            }
            else
            {
                var watchedIdx = RewardStorage.ReadInt(coinsRewardIdxKey, 0);
                if (i <= watchedIdx)
                {
                    btnCoins.Watched();
                }
                else if (i - 1 <= watchedIdx)
                {
                    btnCoins.Available();
                }
                else
                {
                    btnCoins.Unavailable();
                }
            }
        }
    }

    void AllCoinsRewardUnavailable()
    {
        allCoinsRewardUnavailable = true;
        foreach (var item in btnsCoinsRewarded)
        {
            item.Unavailable();
        }
    }

    private void CoinsRewarded_Clicked(BtnCoinsRewarded btn)
    {
        var attentionStr = Language.Rus ? "Внимание!" : "Attention!";

        if (btn.state is BtnCoinsRewarded.State.Unavailable)
        {
            if (allCoinsRewardUnavailable)
            {
                var waitResetTimer = 
                    Language.Rus 
                    ? "Дождись сброса времени. Не переживай, максимальная награда будет сразу доступна в течении дня" 
                    : "Wait for the time to reset. Don't worry, the maximum reward will be immediately available during the day.";
                
                InfoPopup.Show
                (
                    infoPopupPrefab,
                    attentionStr,
                    new InfoPopup.InfoItemData(waitResetTimer)
                );
            }
            else
            {
                var r = btn.Idx == 1 ? "+100" : "+500";
                var getPrevReward = Language.Rus 
                    ? "Сначала вам нужно получить предыдущую награду"
                    : "First you need to get the previous reward";

                InfoPopup.Show
                (
                    infoPopupPrefab,
                    attentionStr,
                    new InfoPopup.InfoItemData(getPrevReward),
                    new InfoPopup.InfoItemData($"{r}", coinSprite)
                );
            }
            return;
        }

#if UNITY_WEBGL && YG_PLUGIN_YANDEX_GAME
        YG.YandexGame.RewVideoShow(btn.Idx);
        
#endif
    }

    private void RewarVideo_Closed()
    {

    }

    public void Dispose ()
    {
#if UNITY_WEBGL && YG_PLUGIN_YANDEX_GAME
        YG.YandexGame.RewardVideoEvent -= RewardVideo_Watched;
        YG.YandexGame.CloseVideoEvent -= RewarVideo_Closed;
#endif
    }
}
