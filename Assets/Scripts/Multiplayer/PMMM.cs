using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using System.Collections; // Adicionado, caso precise de corrotinas no futuro

public class PMMM : MonoBehaviour
{
    // --- Singleton Pattern ---
    public static PMMM instance;

    // --- Variável de Estado Estática ---
    /// <summary>
    /// Flag estática usada por Movement2D e CombatSystem2D para bloquear inputs.
    /// </summary>
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
        // Define que a pausa só pode ser ativada nas cenas de jogo (assumindo que "Menu" é a cena de lobby/menu)
        isGameSceneLoaded = !scene.name.Contains("Menu"); 

        // Se carregarmos uma cena nova, garante que o painel está fechado e o estado redefinido
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        // Redefine a flag de pausa (crucial)
        IsPausedLocally = false;
        
        // Garante que o cursor está no estado correto para o novo ambiente
        if (isGameSceneLoaded)
        {
            LockCursor(); // Confina o cursor para o gameplay
        }
        else
        {
            UnlockCursor(); // Liberta o cursor para a UI do menu/lobby
        }
    }

    void Update()
    {
        // Apenas processa o input de pausa se estivermos numa cena de jogo E (opcionalmente) numa sala Photon
        // Se estiver num jogo Single Player, PhotonNetwork.InRoom é falso, mas a pausa deve funcionar na mesma.
        if (!isGameSceneLoaded) return;
        
        // Verifica o input da tecla ESCAPE
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // --- VERIFICAÇÕES DE PRIORIDADE ---
            
            // 1. PRIORIDADE DO CHAT: Se o chat existir E estiver aberto, bloqueia a pausa.
            GameChat chatInstance = GameChat.instance;
            if (chatInstance != null && chatInstance.IsChatOpen) 
            {
                return; // O GameChat.cs é responsável por fechar o input field quando ESC é pressionado.
            }

            // 2. PRIORIDADE DO LOBBY: Se o LobbyManager existir E ainda não tiver começado, bloqueia a pausa.
            // Esta é uma segurança extra, mas na prática, o PMMM não deve existir no Lobby, ou isGameSceneLoaded deve ser falso.
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
                // Só pausa se o jogo estiver ativo.
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
    /// Se não estiver em sala, apenas carrega o menu.
    /// </summary>
    public void LeaveGame()
    {
        // Garante que o estado de pausa é redefinido
        IsPausedLocally = false;

        if (PhotonNetwork.InRoom)
        {
            // Assume que existe um RoomManager ou que o código de saída está aqui.
            // Se usar o RoomManager, o código será parecido com isto:
            // RoomManager.instance.LeaveGameAndGoToMenu("MenuPrincipal");
            
            // Se o RoomManager não estiver disponível, o método Photon padrão é:
            PhotonNetwork.LeaveRoom();
            
            // O OnLeftRoom (callback do Photon) tratará da transição de cena.
            // Para simplificar, vou adicionar a transição diretamente aqui:
            SceneManager.LoadScene("MenuPrincipal"); // Substitua pelo nome da sua cena de Menu
        }
        else
        {
            // Single Player ou não conectado:
            SceneManager.LoadScene("MenuPrincipal"); // Substitua pelo nome da sua cena de Menu
        }

        UnlockCursor(); // Garante que o cursor está livre no menu
        
        // Destruir o objeto PMMM para garantir que ele é recriado limpo no Menu,
        // já que DontDestroyOnLoad foi usado.
        Destroy(gameObject);
    }
    
    // ------------------------------------
    // --- FUNÇÕES DE CONTROLO DO CURSOR ---
    // ------------------------------------

    /// <summary>
    /// Confina o cursor à janela do jogo.
    /// </summary>
    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Confined; 
        Cursor.visible = true;
    }

    /// <summary>
    /// Liberta o cursor para interagir com a UI.
    /// </summary>
    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true; 
    }
}
