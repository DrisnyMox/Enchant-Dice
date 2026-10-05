using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using TMPro;
using static Settings;
using YG.Utils.LB;

public class Menu : MonoBehaviour
{
    [SerializeField] GameObject panelEbash;
    [SerializeField] PanelDeck panelDeck;
    [SerializeField] PanelShop panelShop;
    [SerializeField] GameObject panelChestResult;
    [SerializeField] PanelUpdateGame panelUpdateGame;
    [SerializeField] YGLeaderBoard leaderBoard;

    [Space]

    [SerializeField] Button btnShop;
    [SerializeField] Button btnDeck;
    [SerializeField] Button btnEbash;

    [SerializeField] public Button btnPVP;
    [SerializeField] public Button btnCoop;
    [SerializeField] public GameObject connectingInfo;
    
    [SerializeField] Button btnOpenChest;
    [SerializeField] Button btnChestResult;
    [SerializeField] Button btnTutor;

    [Space]

    [SerializeField] CoinRewardAnimator coinAnim;
    [SerializeField] CoinRewardAnimator stonesAnim;

    [Space]

    [SerializeField] TMP_Text labelExp;
    [SerializeField] TMP_Text labelGold;
    [SerializeField] TMP_Text labelStones;
    [SerializeField] TMP_Text labelCountCards;
    [SerializeField] TMP_Text labelMaxWave;

    [Space]

    [SerializeField] Transform chestResultParent;

    [Space]

    [SerializeField] CardChestResult chestResultPrefab;
    

    [SerializeField] Button tutorReset;
    [SerializeField] TMP_InputField inputNickname;

    List<Dice> allDices;

    static bool loaded;

    public void Init()
    {
        allDices ??= DiceStorage.Single.allDices;

        gameObject.SetActive(true);

#if UNITY_ANDROID
        Saver.Load();
        loaded = true;
#endif

#if UNITY_WEBGL
        YG.YandexGame.GetDataEvent += Data_Geted;
        YG.YandexGame.onGetLeaderboard += LB_geted;
#endif
        inputNickname.onValueChanged.AddListener(Nickname_Changed);
        inputNickname.onSubmit.AddListener(Nickname_Submited);

        

        panelChestResult.SetActive(false);
        panelDeck.gameObject.SetActive(false);
        panelShop.gameObject.SetActive(false);
        panelEbash.SetActive(true);
        panelUpdateGame.gameObject.SetActive(false);

        btnTutor.gameObject.SetActive(false);

        btnShop.onClick.AddListener(BtnShop_Clicked);
        btnDeck.onClick.AddListener(BtnDeck_Clicked);
        btnEbash.onClick.AddListener(BtnEbash_Clicked);
        btnPVP.onClick.AddListener(BtnPVP_Clicked);
        btnOpenChest.onClick.AddListener(BtnChest_Clicked);
        btnChestResult.onClick.AddListener(ChestResult_Clicked);

        //checkGameVersion.versionNotMatch += GameVersion_NotMatched;
        if (loaded)
        {
            leaderBoard.Init();
            leaderBoard.InvokeGetLeadearBoard();

            panelShop.Init();
            panelShop.onCoinsUpdate.AddListener(UserData_Updated);
            panelShop.onStonesUpdate += StonesData_Updated;

            // Куда лететь камням, считается в рантайме по фактическим позициям:
            // система частиц живёт в префабе магазина, аттрактор — в префабе счётчика,
            // ссылку между разными префабами не положить, поэтому связываем здесь
            if (stonesAnim && panelShop.FlyStonesEffect)
            {
                var attractor = stonesAnim.GetComponent<Coffee.UIExtensions.UIParticleAttractor>();
                if (attractor)
                    attractor.AddParticleSystem(panelShop.FlyStonesEffect);
            }
#if UNITY_WEBGL

            if (YG.YandexGame.savesData != null && string.IsNullOrEmpty(YG.YandexGame.savesData.newPlayerName))
            {
                var str = Language.Rus ? "Игрок " : "Player ";
                inputNickname.text = $"{str}{Random.Range(100, 999)}";
            }
            else
            {
                inputNickname.text = YG.YandexGame.savesData?.newPlayerName;
            }
#endif

            UpdateMaxWaveUIView();
        }

        tutorReset.onClick.AddListener(() => 
        { 
            User.Data.tutorCompleted = false;
#if UNITY_ANDROID
            Saver.Save();
#endif

#if UNITY_WEBGL
            Saver.ConvertToYG();
            YG.YandexGame.SaveProgress();
#endif
            Advertising.ShowVideoAd(); 
        });

#if UNITY_WEBGL
        //if (YG.YandexGame.savesData.deck != null)
        //{
        //    Saver.ConvertToUserData();
        //}
#endif
        UpdateUI();

    }

    private void ChestResult_Clicked()
    {
        var canvasGroup = panelChestResult.GetComponent<CanvasGroup>();
        LeanTweanTool.SetTransparencyImage(canvasGroup, 0).setOnComplete(OnComplete);

        void OnComplete()
        {
            panelChestResult.SetActive(false);
        } 
    }

    // Пока счётчик досчитывает до новой суммы, Update не должен перебивать его
    bool stonesDataUpdate = false;
    private void StonesData_Updated(float delay)
    {
        if (!stonesAnim)
            return;

        stonesDataUpdate = true;

        LeanTween.delayedCall(delay, Delay);

        void Delay()
        {
            stonesAnim.Play(1.5f, User.Data.countStones, () => { stonesDataUpdate = false; });
        }
    }

    bool userDataUpdate = false;
    private void UserData_Updated(float delay)
    {
        userDataUpdate = true;

        LeanTween.delayedCall(delay, Delay);

        void Delay()
        {
            coinAnim.Play(1.5f, User.Data.golda, () => { userDataUpdate = false; });
        }
    }

    private void Nickname_Submited(string value)
    {
        Nickname_Changed(value);
    }

    private void Nickname_Changed(string value)
    {
#if UNITY_WEBGL
        if (YG.YandexGame.savesData.newPlayerName == value)
            return;

        YG.YandexGame.savesData.newPlayerName = value;
        YG.YandexGame.SaveProgress();
#endif
        Photon.Pun.PhotonNetwork.NickName = value;

        DelaySave();
    }

    private void DelaySave()
    {
        StartCoroutine(Delay());

        IEnumerator Delay()
        {
            yield return new WaitForSeconds(3);

#if UNITY_WEBGL
            YG.YandexGame.savesData.newPlayerName = inputNickname.text;
            YG.YandexGame.SaveProgress();
#endif

            Saver.Save();
        }
    }

    private void Data_Geted()
    {
#if UNITY_WEBGL
        Saver.ConvertToUserData();

        var data = YG.YandexGame.savesData;
        inputNickname.text = data.newPlayerName;

        Photon.Pun.PhotonNetwork.NickName = data.newPlayerName;
#endif
        
        loaded = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Menuha");
    }

    void UpdateUI()
    {
        //print(YG.YandexGame.savesData.countCards);
        panelDeck.Init(allDices);
        var txtCards = Language.Rus ? "Карточки" : "Cards";
        labelCountCards.text = $"{txtCards} {User.Data.countCards}/{CARDS_TO_OPEN_CHEST}";

        if (User.Data.tutorCompleted == false)
        {
            btnTutor.gameObject.SetActive(true);
            btnPVP.gameObject.SetActive(false);
            btnCoop.gameObject.SetActive(false);

            btnTutor.onClick.AddListener(BtnTutor_Clicked);
        }
    }

    private void GameVersion_NotMatched()
    {
        panelUpdateGame.gameObject.SetActive(true);
    }

    private void BtnTutor_Clicked()
    {
        print("Загружаю тутор");
        UnityEngine.SceneManagement.SceneManager.LoadScene("Tutor Epta");
    }

    private void BtnPVP_Clicked()
    {
        EventsHolder.onBtnPvPClicked?.Invoke(btnPVP);
    }

    private void BtnChest_Clicked()
    {
        if (User.Data.countCards >= CARDS_TO_OPEN_CHEST)
        {
            panelChestResult.SetActive(true);
            var canvasGroup =  panelChestResult.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0;
            LeanTweanTool.SetTransparencyImage(canvasGroup, 1f);

            foreach (Transform item in chestResultParent) Destroy(item.gameObject);

            int countTypesDice = Random.Range(2, 4);
            for (int i = 0; i < countTypesDice; i++)
            {
                var randomIdx = DiceStorage.Single.GetNormalizedRandomDicePrefab().idxInInventory;
                var dice = allDices[randomIdx];
                int countDices = Random.Range(1, 8 + User.Data.lvl);

                var diceUI = Instantiate(chestResultPrefab, chestResultParent);
                diceUI.Init(countDices, dice);

                var inInventory = User.Data.inventory.Find(d => d.idx == randomIdx);
                if(inInventory == null)
                {
                    User.Data.inventory.Add(new() { idx = randomIdx, count = countDices });
                }
                else
                {
                    inInventory.count += countDices;
                }
            }

            // Golda 
            var cardReward = Instantiate(chestResultPrefab, chestResultParent);
            var golda = Random.Range(User.Data.lvl * 8, User.Data.lvl * 88);
            cardReward.Init(golda, null);
            User.Data.golda += golda;

            User.Data.countCards -= CARDS_TO_OPEN_CHEST;
            string txt = Language.Rus ? "Карточки" : "Cards";
            labelCountCards.text = $"{txt} {User.Data.countCards}/{CARDS_TO_OPEN_CHEST}";

#if UNITY_ANDROID
            Saver.Save();
#endif

#if UNITY_WEBGL
            Saver.ConvertToYG();
            YG.YandexGame.SaveProgress();
#endif
        }
    }

    void UpdateMaxWaveUIView()
    {
        labelMaxWave.text = Language.Rus ? $"Максимально прожито волн в кооперативе {User.Data.maxWave}" : $"Max surved waves in cooperative {User.Data.maxWave}";
    }

    private void Update()
    {
#if UNITY_EDITOR
   
        if (Input.GetKeyDown(KeyCode.P))
        {
            User.Data.countCards += 3;
            labelCountCards.text = $"Cards {User.Data.countCards}/{CARDS_TO_OPEN_CHEST}";

            Saver.Save();
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            User.Data.exp += Random.Range(5, 30);

            if (User.Data.exp >= User.Data.ExpToNextLvl)
            {
                User.Data.exp -= User.Data.ExpToNextLvl;
                User.Data.lvl++;
            }
        }

        

        if (Input.GetKeyDown(KeyCode.C))
        {
            User.Data.tutorCompleted = true;
            Saver.Save();
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            print("Загружаю тутор");
            UnityEngine.SceneManagement.SceneManager.LoadScene("Tutor Epta");
        }
#endif

        if (Input.GetKeyDown(KeyCode.R))
            PlayerPrefs.DeleteAll();

        if (Input.GetKeyDown(KeyCode.B))
            YG.YandexGame.GetLeaderboard("wave", 20, 3, 6, "medium");

        string txt = Language.Rus ? "Уровень" : "LvL";
        labelExp.text = $"{txt} {User.Data.lvl} ({User.Data.exp}/{User.Data.ExpToNextLvl})";

        if (!userDataUpdate)
        {
            labelGold.SetText($"{User.Data.golda}");
        }
        if (!stonesDataUpdate)
        {
            labelStones.SetText($"{User.Data.countStones}");
        }
    }

    private void LB_geted(LBData obj)
    {
        if (obj == null || obj.thisPlayer == null)
            return;

        print($"Запись в лидерборде {obj.thisPlayer.score}");
        if (User.Data.maxWave < obj.thisPlayer.score)
        {
            User.Data.maxWave = obj.thisPlayer.score;
            Saver.Save();

            UpdateMaxWaveUIView();
        }
    }

    private void OnDestroy()
    {
        YG.YandexGame.GetDataEvent -= Data_Geted;
        YG.YandexGame.onGetLeaderboard -= LB_geted;

        panelShop.Dispose();
    }

    private void BtnShop_Clicked()
    {
        panelDeck.gameObject.SetActive(false);
        panelShop.gameObject.SetActive(true);
        panelEbash.SetActive(false);
    }

    private void BtnEbash_Clicked()
    {
        panelDeck.gameObject.SetActive(false);
        panelShop.gameObject.SetActive(false);
        panelEbash.SetActive(true);
    }

    private void BtnDeck_Clicked()
    {
        panelDeck.gameObject.SetActive(true);
        panelDeck.UpdateInventoryCards();

        panelEbash.SetActive(false);
        panelShop.gameObject.SetActive(false);
    }
}
