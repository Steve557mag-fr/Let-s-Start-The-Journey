using UnityEngine;
using Newtonsoft.Json.Linq;
using NativeWebSocket;
using System;
using System.Collections.Generic;

public class SocketManager : GameSingleton
{
    const string WEBSOCKET_ADDRESS = "ws://51.75.121.124:3030";
    
    //Delegate
    public delegate void SocketResponse(JObject data = null);
    public SocketResponse onAuthentificated;
    public SocketResponse onJoinedLobby;
    public SocketResponse onLobbyUpdate;
    

    [SerializeField] UILobby uiLobby;
    public WebSocket websocket;

    private Dictionary<string, Action<JObject>> wsResponses;
    private bool userIsConnected;
    private string lobbyId = "";
    private string userName;
    public string userId;
    public JObject currentLobbyData;


    private void Awake()
    {  
        wsResponses = new Dictionary<string, Action<JObject>>()
        {
            {"create_user", OnUserCreated},
            {"join_or_create_lobby", OnJoinedOrCreatedLobby},
            {"leave_lobby", OnLeaveLobby},
            {"get_meta", OnDataFetch},
            {"set_meta", OnDataSet},
            {"send_data", OnSendData},
            {"received_data", OnReceivedData},
            {"lobby_updated", OnLobbyUpdate},
            {"get_game_data", OnGetGameData }
        };
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR 
        if (userIsConnected)
        {
            websocket.DispatchMessageQueue();
        }
#endif
    }

    internal bool LobbyIsReady()
    {
        foreach(var item in currentLobbyData["users"].ToObject<JObject>().Properties())
        {
            var currentUser = item.Name;
            if (!currentLobbyData["metadata"][$"{currentUser}_check"].ToObject<bool>()) return false;
        }
        return true;
    }

    private void OnGetGameData(JObject response)
    {
        bool success = response["success"].ToObject<bool>();
        if (success)
        {
            JObject gameData = response["game_data"].ToObject<JObject>();
            OnLog("GetGameData Called -- Successful", LoggingSeverity.Info);
        }
        else OnLog("GetGameData Called -- Unsuccessful", LoggingSeverity.Warning);
    }

    private void OnLobbyUpdate(JObject response)
    {
        currentLobbyData = response["lobby"].ToObject<JObject>();
        onLobbyUpdate(currentLobbyData);
    }

    private void OnReceivedData(JObject response)
    {
        JObject receivedData = response["data"].ToObject<JObject>();
    }

    private void OnSendData(JObject response)
    {
        bool success = response["success"].ToObject<bool>();
    }

    private void OnDataSet(JObject response)
    {
        bool exist = response["exist"].ToObject<bool>();
        if (exist) OnLog("metadata set successfully!", LoggingSeverity.Info);
        else OnLog("metadata was not set", LoggingSeverity.Warning);
    }

    private void OnDataFetch(JObject response)
    {
        bool exist = response["exist"].ToObject<bool>();
        string val = response["val"].ToString();
    }

    private void OnLeaveLobby(JObject response)
    {
        bool left = response["left"].ToObject<bool>();
    }

    private void OnJoinedOrCreatedLobby(JObject response)
    {
        bool joined = response["joined"].ToObject<bool>();
        if (joined)
        {
            lobbyId = response["lobby_id"].ToString();
            ToWSS(new()
            {
                {"request_method", "set_meta"},
                {"lobby_id", lobbyId},
                {"key", $"{userId}_check"},
                {"val", false}
            });
            onJoinedLobby(); 
            OnLog($"Joined Lobby ! Lobby id : {lobbyId}");
        }
        else
        {
            string message = response["message"].ToString();

            OnLog(message, LoggingSeverity.Warning);
        }
    }

    private void OnUserCreated(JObject response) 
    {
        userId = response["user_id"].ToString();
        onAuthentificated(new(){ {"state",true}, {"user_name", response["user_name"] } });
        OnLog($"new uuid received : {userId}", LoggingSeverity.Message);
    }

    public async void Connect(string username)
    {
        websocket = new WebSocket(WEBSOCKET_ADDRESS);

        websocket.OnOpen += () =>
        {
            ToWSS(new()
            {
                {"request_method", "create_user" },
                {"user_name", username }
            });
            OnLog($"Connected ! Hello {username}", LoggingSeverity.Info);
            this.userName = username;
            userIsConnected = true;
        };

        websocket.OnError += (e) =>
        {
            OnLog($"Error! {e}", LoggingSeverity.Error);
            userIsConnected = false;
        };

        websocket.OnClose += OnClose;
        websocket.OnMessage += OnMessage;

        // waiting for messages
        await websocket.Connect();
    }

    void ToWSS(JObject jsonRequest)
    {
        websocket.SendText(jsonRequest.ToString());
        OnLog($"Sent Message of method {jsonRequest["request_method"]}", LoggingSeverity.Info);
    }

    public void JoinOrCreateLobby()
    {
        ToWSS(new()
        {
            {"request_method", "join_or_create_lobby" },
            {"user_id", userId }
        });

    }

    public void Ready(bool state)
    {
        ToWSS(new()
        {
            {"request_method", "set_meta"},
            {"lobby_id", lobbyId},
            {"key", $"{userId}_check"},
            {"val", state}
        });
    }

    public void LeaveLobby()
    {
        ToWSS(new()
        {
            {"request_method", "leave_lobby" },
            {"user_id", userId}
        });
        lobbyId = "";
    }

    void OnMessage(byte[] bytes)
    {
        string message = System.Text.Encoding.UTF8.GetString(bytes);
        JObject jsonResponse = JObject.Parse(message);

        OnLog($"Message Received -- Content : {jsonResponse}");

        if (wsResponses.ContainsKey(jsonResponse["request_method"].ToString()))
        {
            wsResponses[jsonResponse["request_method"].ToString()](jsonResponse);
        }
        else
        {
            OnLog("No method was found", LoggingSeverity.Warning);
        }

    }

    void OnClose(WebSocketCloseCode e)
    {
        OnLog($"Connection closed! => {e}", LoggingSeverity.Info);
        userIsConnected = false;
    }

    private async void OnApplicationQuit()
    {
        if(lobbyId != "")LeaveLobby();
        if(userIsConnected)await websocket.Close();
        userIsConnected = false;
    }

    void OnLog(string message, LoggingSeverity severity = LoggingSeverity.Verbose)
    {
        Debug.Log($"[DISC/{severity}]: {message}");
    }

}

public enum LoggingSeverity
{
    Verbose = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    None = 5,
    Message = 6,
    State = 7,
}