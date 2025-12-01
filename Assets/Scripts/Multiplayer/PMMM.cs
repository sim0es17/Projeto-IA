using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;

public class PMMM : MonoBehaviour
{
    // --- Singleton Pattern ---
    public static PMMM instance;

    // --- Variável de Estado Estática (A chave para a sincronização local do chat/movimento) ---
    public static bool IsPausedLocally = false;

    [Header("UI Reference")]
    [Tooltip("O painel da UI que contém todos os botões e texto do Menu de Pausa.")]
    public GameObject pausePanel;

    private bool isGameSceneLoaded = false;

    void Awake()
    {
        // 1. Implementação do Singleton
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        
        // 2. Permite que o Manager persista entre cenas (Menu -> Jogo -> Menu)
        DontDestroyOnLoad(this.gameObject);

        // 3. Define o estado inicial como "não pausado" e desativa a UI
        IsPausedLocally = false;
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    void OnEnable()
    {
        // Subscreve ao evento de carregamento de cena para redefinir o estado.
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        // Cancela a subscrição para evitar erros.
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Define que a pausa só pode ser ativada nas cenas de jogo (e não no Menu)
        isGameSceneLoaded = !scene.name.Contains("Menu"); 

        // Se carregarmos uma cena nova, garante que o painel está fechado e o estado redefinido
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        // Redefine a flag de pausa (crucial)
        IsPausedLocally = false;
        
        // Garante que o cursor está no estado correto para o novo ambiente
        if (!isGameSceneLoaded)
        {
            // Se for menu/lobby, liberta o cursor para UI (visível e livre)
            UnlockCursor();
        }
        else
        {
            // Se for jogo, confina o cursor (visível e confinado)
            LockCursor(); 
        }
    }

    void Update()
    {
        // Apenas processa o input de pausa se estivermos numa cena de jogo E numa sala
        if (!isGameSceneLoaded || !PhotonNetwork.InRoom) return;
        
        // Verifica o input da tecla ESCAPE
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // NOTA: O GameChat deve ter a prioridade para fechar o chat
            // Se o chat NÃO estiver aberto, esta lógica é executada para pausar/retomar.
            
            // Assumimos que o GameChat.instance existe
            GameChat chatInstance = GameChat.instance;
            
            // Se o chat estiver aberto, o GameChat lida com o ESCAPE primeiro e fecha-o.
            // Se o chat estiver fechado, o Menu de Pausa é ativado.
            if (chatInstance != null && chatInstance.inputField.gameObject.activeSelf)
            {
                // O chat está aberto. O GameChat deve fechar-se, e o ESCAPE não deve chegar aqui.
                // Mas, por segurança, se o chat falhar ao bloquear, não pausamos.
                return;
            }
            
            // Pausar/Retomar
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

    // ------------------------------------
    // --- FUNÇÕES DE PAUSA E RETOMADA ---
    // ------------------------------------

    /// <summary>
    /// Pausa o jogo localmente, abrindo o menu e libertando o cursor.
    /// </summary>
    public void PauseGame()
    {
        if (IsPausedLocally || !isGameSceneLoaded) return;

        // 1. Define o estado (Bloqueia o movimento e chat via estático)
        IsPausedLocally = true;

        // 2. Ativa a UI do Menu de Pausa
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }

        // 3. Liberta o cursor para que o jogador possa interagir com a UI
        UnlockCursor();

        Debug.Log("[PMMM] Jogo pausado localmente. Inputs bloqueados.");
    }

    /// <summary>
    /// Retoma o jogo localmente, fechando o menu e confinando o cursor.
    /// </summary>
    public void ResumeGame()
    {
        if (!IsPausedLocally) return;

        // 1. Define o estado
        IsPausedLocally = false;

        // 2. Desativa a UI do Menu de Pausa
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        // 3. Confina o cursor novamente para retomar o gameplay
        LockCursor();

        Debug.Log("[PMMM] Jogo retomado. Inputs reativados.");
    }

    // ------------------------------------
    // --- FUNÇÃO DE SAÍDA DO JOGO ---
    // ------------------------------------
    
    /// <summary>
    /// Sai da sala Photon e volta ao menu principal.
    /// </summary>
    public void LeaveGame()
    {
        if (RoomManager.instance != null)
        {
            // Assumimos que a cena do menu principal se chama "MenuPrincipal"
            RoomManager.instance.LeaveGameAndGoToMenu("MenuPrincipal");
        }
        else
        {
            Debug.LogError("[PMMM] RoomManager não encontrado! Não é possível sair da sala.");
        }

        // Garante que o estado de pausa é redefinido e o cursor libertado
        IsPausedLocally = false;
        UnlockCursor();
    }
    
    // ------------------------------------
    // --- FUNÇÕES DE CONTROLO DO CURSOR ---
    // ------------------------------------

    /// <summary>
    /// Confina o cursor à janela do jogo (Visível e Confined).
    /// </summary>
    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Confined; 
        Cursor.visible = true; // Mantém o cursor visível
    }

    /// <summary>
    /// Liberta o cursor para interagir com a UI (Visível e None).
    /// </summary>
    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true; // Mantém o cursor visível
    }
}
