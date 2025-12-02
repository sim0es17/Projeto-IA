using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using System.Collections;

public class PMMM : MonoBehaviour
{
    // --- Singleton Pattern ---
    public static PMMM instance;

    // --- Variável de Estado Estática ---
    public static bool IsPausedLocally = false;

    [Header("UI Reference")]
    public GameObject pausePanel;

    private bool isGameSceneLoaded = false;
    
    // VARIÁVEL FICTÍCIA (para evitar dependência direta sem fornecer LobbyManager)
    public class LobbyManager
    {
        public static LobbyManager instance;
        public static bool GameStartedAndPlayerCanMove = true; 
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(this.gameObject);
        IsPausedLocally = false;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isGameSceneLoaded = !scene.name.Contains("Menu"); 

        if (pausePanel != null) pausePanel.SetActive(false);
        IsPausedLocally = false;
        
        if (isGameSceneLoaded) LockCursor();
        else UnlockCursor();
    }

    void Update()
    {
        if (!isGameSceneLoaded) return;
        
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 1. PRIORIDADE DO CHAT
            GameChat chatInstance = GameChat.instance;
            if (chatInstance != null && chatInstance.IsChatOpen) 
            {
                return; 
            }

            // 2. PRIORIDADE DO LOBBY (Usando a classe fictícia ou a real, se importada)
            bool lobbyBlocking = (LobbyManager.instance != null && !LobbyManager.GameStartedAndPlayerCanMove);
            if (lobbyBlocking)
            {
                return;
            }

            // 3. Pausar/Retomar
            if (IsPausedLocally)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        if (IsPausedLocally || !isGameSceneLoaded) return;

        IsPausedLocally = true;
        if (pausePanel != null) pausePanel.SetActive(true);
        UnlockCursor();
        Debug.Log("[PMMM] Jogo pausado localmente. Inputs bloqueados.");
    }

    public void ResumeGame()
    {
        if (!IsPausedLocally) return;

        IsPausedLocally = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        LockCursor();
        Debug.Log("[PMMM] Jogo retomado. Inputs reativados.");
    }

    public void LeaveGame()
    {
        IsPausedLocally = false;
        UnlockCursor();
        
        // CHAMA O ROOM MANAGER PARA SAIR DO JOGO
        if (RoomManager.instance != null)
        {
            RoomManager.instance.LeaveGameAndGoToMenu("MenuPrincipal"); 
        }
        else
        {
            // Fallback
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
            SceneManager.LoadScene("MenuPrincipal"); 
        }

        Destroy(gameObject);
    }
    
    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Confined; 
        Cursor.visible = true;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true; 
    }
}
