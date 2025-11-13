using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header("Painéis")]
    [Tooltip("Painel principal com os botões Play, Settings, Exit.")]
    [SerializeField] private GameObject mainButtonsPanel;    // Painel com Play/Settings/Exit

    [Tooltip("Painel que aparece após carregar em Play (ex: Multiplayer/Training).")]
    [SerializeField] private GameObject playOptionsPanel;    // Painel com Multiplayer/Training

    [Header("Nomes das Cenas (exatamente como nas Build Settings)")]
    [SerializeField] private string multiplayerSceneName = "MultiplayerLobby";
    [SerializeField] private string characterSelectSceneName = "CharacterSelect";
    [SerializeField] private string trainingSceneName = "TrainingGround";

    // O OnEnable é a chave: é chamado sempre que o objeto é ativado, incluindo quando
    // a cena é carregada. Garante que a UI aparece corretamente.
    private void OnEnable()
    {
        // 1. Garante que o painel principal está ativo e o sub-menu está escondido.
        TogglePanels(true, false);

        // 2. Garante que o tempo do jogo está normal (se estava em pausa na cena anterior).
        Time.timeScale = 1f;
    }

    // --- Botões do menu principal ---

    public void OnPlayPressed()
    {
        // Esconde principal, mostra submenu
        TogglePanels(false, true);
    }

    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        // Esta linha permite que o botão 'Exit' funcione no editor do Unity
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // --- Botões do submenu (Play Options) ---

    public void OnBackPressed()
    {
        // Volta ao menu principal
        TogglePanels(true, false);
    }

    public void OnMultiplayerPressed()
    {
        LoadSceneSafe(multiplayerSceneName);
    }

    public void OnTrainingPressed()
    {
        // Primeiro vai para a cena de seleção de personagem
        LoadSceneSafe(characterSelectSceneName);
    }

    // --- Utilitários ---

    /// <summary>
    /// Ativa e desativa os painéis de UI.
    /// </summary>
    private void TogglePanels(bool showMain, bool showPlayOptions)
    {
        if (mainButtonsPanel != null)
            mainButtonsPanel.SetActive(showMain);

        if (playOptionsPanel != null)
            playOptionsPanel.SetActive(showPlayOptions);
    }

    /// <summary>
    /// Carrega uma cena com verificação de segurança.
    /// </summary>
    private void LoadSceneSafe(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Nome da cena não está definido no Inspector!");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }
}
