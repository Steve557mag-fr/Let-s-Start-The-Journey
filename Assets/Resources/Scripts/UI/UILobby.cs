using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Rendering;

public class UILobby : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI authLabel;
    [SerializeField] TextMeshProUGUI username, readyButton;
    [SerializeField] GameObject authButton;
    [SerializeField] GameObject joinedContainer, lobbyContainer, authContainer, markReadySystem;
    [SerializeField] TMP_InputField usernameInput, codeInput;
    [SerializeField] CanvasGroup authError;
    [SerializeField] UserSlot[] userSlots;
    [SerializeField] int usernameCharLimit;

    private bool playerIsReady;

    SocketManager lobbyManager;
    MinigamesManager minigamesManager;

    public void Start()
    {
        lobbyManager = GameSingleton.GetInstance<SocketManager>();
        minigamesManager = GameSingleton.GetInstance<MinigamesManager>();

        usernameInput.characterLimit = 15;

        lobbyManager.onAuthentificated += AuthFinished;
        lobbyManager.onJoinedLobby += DisplayLobby;
        lobbyManager.onLobbyUpdate += UpdateLobby;
    }

    public void Connect()
    {
        if (usernameInput.text.Length > 1)
        {
            lobbyManager.Connect(usernameInput.text);
        }
    }
    public void QuitLobby()
    {
        lobbyContainer.SetActive(false);
        markReadySystem.SetActive(false);
        joinedContainer.SetActive(true);
        lobbyManager.LeaveLobby();
    }

    void DisplayLobby(JObject o)
    {
        joinedContainer.SetActive(false);
        lobbyContainer.SetActive(true);
        markReadySystem.SetActive(true);
    }

    private void UpdateLobby(JObject lobbyData)
    {
        for (int i = 0; i < 4; i++)
        {
            if (i < lobbyData["users"].Count())
            {
                JProperty user = ((JObject)lobbyData["users"]).Properties().ToList()[i];
                string name = user.Value["name"].ToString(); 
                if (lobbyData["metadata"][$"{user.Name}_check"] == null) continue;
                bool ready = lobbyData["metadata"][$"{user.Name}_check"].ToObject<bool>();
                userSlots[i].readyMark.SetActive(ready);
                userSlots[i].userNameText.text = name; 
            }
            else
            {
                userSlots[i].readyMark.SetActive(false);
                userSlots[i].userNameText.text = "";
            }

        }

        if (lobbyData["metadata"][$"{lobbyManager.userId}_check"] != null)
            playerIsReady = lobbyData["metadata"][$"{lobbyManager.userId}_check"].ToObject<bool>();
    }

    void AuthFinished(JObject data)
    {
        if (data["state"].ToObject<bool>())
        {
            authContainer.SetActive(false);
            joinedContainer.SetActive(true);
            this.username.text = $"Bonjour {data["user_name"].ToString()} !";
        }
        else
        {
            LeanLog(authError, 1, 2);
        }

    }

    public void OnReady()
    {
        lobbyManager.Ready(!playerIsReady);
        if (playerIsReady)
        {
            readyButton.text = "PR�T"; 
        }
        else
        {
            readyButton.text = "PAS PR�T";
        }
    }

    public void OnCodeEnter()
    {
        minigamesManager.SetMGFromCode(codeInput.text);
    }

    void LeanLog(CanvasGroup text, float alpha, float time, float delay = 3)
    {
        text.LeanAlpha(alpha, time).setOnComplete(() =>
        {
            
            text.LeanAlpha(0, time).setDelay(delay);
        });
    }

    

}


[System.Serializable]
public struct UserSlot
{
    public GameObject readyMark;
    public TextMeshProUGUI userNameText;
}
