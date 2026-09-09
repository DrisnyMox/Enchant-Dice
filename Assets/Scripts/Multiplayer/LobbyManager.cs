using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Realtime;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [SerializeField] string sceneNameToload;

    [Space(3)]
    [SerializeField] byte maxPlayers = 10;

    [Header("UI References")]
    [SerializeField] TextMeshProUGUI logText;
    
    [SerializeField] TMP_InputField inputField;
    
    TMP_Text btnLabel;
    ExitGames.Client.Photon.Hashtable roomProps;
    Button btnConnect;
    Button btnPvP;
    GameObject connectingInfo;


    public System.Action<string> onPhotonConnection;
    string roomType;
    float createRoomTimer;
    bool isOffline;
    bool startCreateRoom;

    void Awake()
    {
        if (PlayerPrefs.HasKey("nick"))
        {
            PhotonNetwork.NickName = PlayerPrefs.GetString("nick");
        }

        // Первоначальные настройки клиента, тупо спизжено из тутора
        if (PhotonNetwork.NickName == string.Empty)
        {
            if (Language.Rus)
                PhotonNetwork.NickName = "Игрок "  + Random.Range(100, 999);
            else
                PhotonNetwork.NickName = "Player " + Random.Range(100, 999);
        }

        

        //PhotonNetwork.AuthValues = new AuthenticationValues(PhotonNetwork.NickName);
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.GameVersion = "1";

            PhotonNetwork.SerializationRate = 30;
            PhotonNetwork.SendRate = 60;

            PhotonNetwork.ConnectUsingSettings();
        }
        

        var ebat = FindObjectOfType<Advertising>();
        ebat.onVideoClosed += () => PhotonNetwork.ConnectUsingSettings();

        PhotonNetwork.KeepAliveInBackground = 180;
    }

    private void Start()
    {
        btnConnect = MenuStarter.Single.ActiveMenu.btnCoop;
        btnPvP = MenuStarter.Single.ActiveMenu.btnPVP;
        connectingInfo = MenuStarter.Single.ActiveMenu.connectingInfo;

        connectingInfo.SetActive(true);

        //print("ща буду подписывать =-=-=-=-=-=-=-=-=-");
        btnConnect.onClick.AddListener(JoinRoom);
        EventsHolder.onBtnPvPClicked.AddListener(BtnPvP_Clicked);

        
        //if (PhotonNetwork.IsConnected)
        if (PhotonNetwork.IsConnectedAndReady)
        {
            ShowBattleButtons();
        }
        else
        {
            HideBattleButtons();
        }
    }

    private void BtnPvP_Clicked(Button btn)
    {
        btnPvP = btn;

        roomType = "pvp";

        roomProps = new();
        roomProps["t"] = roomType;
        PhotonNetwork.JoinRandomRoom(roomProps, maxPlayers);
    }

    public void JoinRoom()
    {
        //print("нажал");
        btnLabel = btnConnect.GetComponentInChildren<TMP_Text>();
        btnLabel.text = Language.Rus ? "Поиск игры.." : "Game searching..";

        roomType = "coop";

        roomProps = new();
        roomProps["t"] = roomType;
        PhotonNetwork.JoinRandomRoom(roomProps, maxPlayers);
    }

    public static string GetNickname()
    {
        return PhotonNetwork.NickName;
    }

    public static void SetNick(string value)
    {
        PhotonNetwork.NickName = value;
    }

    public override void OnConnectedToMaster()
    {
        Log("Некий хуежуй: " + PhotonNetwork.NickName + " присоеденился к пиздатой игруле");
        onPhotonConnection?.Invoke(PhotonNetwork.NickName);

        ShowBattleButtons();
    }

    public override void OnConnected()
    {
        Log("шо блять?");
    }

    private void ShowBattleButtons()
    {
        connectingInfo.SetActive(false);

        if (!User.Data.tutorCompleted)
        {
            return;
        }

        btnConnect.gameObject.SetActive(true);
        btnPvP.gameObject.SetActive(true);
        
    }

    private void HideBattleButtons()
    {
        btnConnect?.gameObject.SetActive(false);
        btnPvP?.gameObject.SetActive(false);

        connectingInfo?.SetActive(true);
    }
    
    void CreateRoom()
    {  
        RoomOptions roomOptions = new()
        {
            MaxPlayers = maxPlayers,
            CleanupCacheOnLeave = false,
            IsOpen = true,
            IsVisible = true,
        };
        roomOptions.CustomRoomProperties = new();
        roomOptions.CustomRoomProperties["t"] = roomType;
        roomOptions.CustomRoomPropertiesForLobby = new string[] { "t" };

        PhotonNetwork.CreateRoom(null, roomOptions);
        startCreateRoom = true;
        createRoomTimer = 0;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        base.OnCreateRoomFailed(returnCode, message);

        print($"!!! Ошибка создания команты {returnCode}# {message}");

        GoOffline();
    }

    // Комнату создать не вышло — играем в одиночку
    void GoOffline()
    {
        startCreateRoom = false;
        isOffline = true;
        OnLeftRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        CreateRoom();
        print($"Не получилось заджойнитья: {message}");
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (var item in roomList)
        {
            print(item.CustomProperties.Count);
        }
    }

    public override void OnJoinedRoom()
    {
        print("Припиздяшил, Уебок " + PhotonNetwork.NickName);

        // В комнате — сторожевой таймер создания больше не нужен
        startCreateRoom = false;

        float waitTime = 0;
        int timeThresold = 5;

        TMP_Text label = btnLabel;
        if (roomType == "pvp")
            label = btnPvP.GetComponentInChildren<TMP_Text>();

        StartCoroutine(PlayersWaiting());

        IEnumerator PlayersWaiting()
        {
            while (waitTime < timeThresold)
            {
                yield return null;

                waitTime += Time.deltaTime;

                if (waitTime % 1.1f > 0.55f)
                    label.text = Language.Rus ? "Поиск игры.." : "Game searching..";
                else
                    label.text = Language.Rus ? "Поиск игры." : "Game searching.";


                if (PhotonNetwork.CurrentRoom.PlayerCount > 1)
                {
                    break;
                }
            }

            if (waitTime >= timeThresold)
            {
                isOffline = true;
                PhotonNetwork.LeaveRoom();
            }
            else
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    if (roomType == "coop")
                        PhotonNetwork.LoadLevel(sceneNameToload);
                    if (roomType == "pvp")
                        PhotonNetwork.LoadLevel("Piska na Pisky");
                }
            }
        }
    }

    public override void OnLeftRoom()
    {
        if (isOffline)
        {
            if (roomType == "coop")
                SceneManager.LoadScene("Igra blat");
            if (roomType == "pvp")
                SceneManager.LoadScene("Piska na Pisky");
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (!pause)
        {
            if (PhotonNetwork.NetworkClientState != ClientState.ConnectedToMasterServer)
            {
                HideBattleButtons();
                DelayConnect();
            }
        }
    }

    private void OnApplicationFocus(bool focus)
    {
        if (PhotonNetwork.NetworkClientState != ClientState.ConnectedToMasterServer)
        {
            HideBattleButtons();
            DelayConnect();
        }
    }

    private void DelayConnect()
    {
        StartCoroutine(Delay());

        static IEnumerator Delay()
        {
            yield return new WaitForSeconds(0.3f);

            PhotonNetwork.ConnectUsingSettings();
        }
    }

   

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.R))
        {
            PlayerPrefs.DeleteAll();
        }
#endif

        if (Input.GetKeyDown(KeyCode.N))
        {
            print(PhotonNetwork.NetworkClientState);
            print(PhotonNetwork.CountOfPlayersOnMaster);
            print(PhotonNetwork.CountOfRooms);
        }

        if (startCreateRoom)
        {
            createRoomTimer += Time.deltaTime;
            if (createRoomTimer > 3f)
            {
                print("!!! Photon молчит при создании комнаты, уходим в оффлайн");
                GoOffline();
            }
        }

        CheckConnection();
    }

    float timerCheckConnection;
    private void CheckConnection()
    {
        timerCheckConnection += Time.deltaTime;

        if (timerCheckConnection > 0.5f)
        {
            timerCheckConnection = 0;

            if (!PhotonNetwork.IsConnected)
            {
                HideBattleButtons();
                DelayConnect();
            }
        }
    }

    void Log(string msg)
    {
        logText.text += "\n";
        logText.text += msg;
    }

    
}
