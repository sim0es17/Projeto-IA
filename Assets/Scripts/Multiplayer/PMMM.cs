using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;

public class PMMM : MonoBehaviour
{
    // --- Singleton Pattern ---
    public static PMMM instance;

    // --- Variável de Estado Estática (A chave para a sincronização local) ---
    public static bool IsPausedLocally = false;

    [Header("UI Reference")]
    [Tooltip("O painel da UI que contém todos os botões e texto do Menu de Pausa.")]
    public GameObject pausePanel;

    private bool isGameSceneLoaded = false;

    void Awake()
    {
        // Implementação do Singleton
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(this.gameObject);

        IsPausedLocally = false;
        
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    // Acompanha se estamos numa cena onde a pausa é permitida
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

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        IsPausedLocally = false;
        
        // Garante que o cursor está no estado correto para o novo ambiente
        if (!isGameSceneLoaded)
        {
            // Se for menu/lobby, liberta (mantém visível)
            UnlockCursor();
        }
        else
        {
            // Se for jogo, confina (mantém visível)
            LockCursor(); 
        }
    }

    void Update()
    {
        if (!isGameSceneLoaded || !PhotonNetwork.InRoom) return;
        
        // Verifica o input da tecla ESCAPE
        if (Input.GetKeyDown(KeyCode.Escape))
        {
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

    // --- FUNÇÕES DE PAUSA ---

    public void PauseGame()
    {
        if (IsPausedLocally || !isGameSceneLoaded) return;

        IsPausedLocally = true;

        // 1. Ativa a UI do Menu de Pausa
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }

        // 2. Liberta o cursor para que o jogador possa interagir com a UI
        UnlockCursor();

        Debug.Log("Jogo pausado localmente. Inputs bloqueados.");
    }

    public void ResumeGame()
    {
        if (!IsPausedLocally) return;

        IsPausedLocally = false;

        // 1. Desativa a UI do Menu de Pausa
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        // 2. Confina o cursor para o gameplay
        LockCursor();

        Debug.Log("Jogo retomado. Inputs reativados.");
    }

    // --- FUNÇÃO DE SAÍDA DO JOGO ---
    
    public void LeaveGame()
    {
        if (RoomManager.instance != null)
        {
            RoomManager.instance.LeaveGameAndGoToMenu("MenuPrincipal");
        }
        else
        {
            Debug.LogError("RoomManager não encontrado! Não é possível sair da sala.");
        }

        IsPausedLocally = false;
        UnlockCursor();
    }
    
    // --- FUNÇÕES DE CONTROLO DO CURSOR MODIFICADAS ---

    private void LockCursor()
    {
        // Confina o cursor à janela (ainda visível) para gameplay
        Cursor.lockState = CursorLockMode.Confined; 
        Cursor.visible = true; // Mantém o cursor visível
    }

    private void UnlockCursor()
    {
        // Liberta o cursor totalmente para a UI de pausa (ainda visível)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true; // Mantém o cursor visível
    }
}
