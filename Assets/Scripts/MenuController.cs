using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header("Painéis")]
    [SerializeField] private GameObject mainButtonsPanel;   // Painel com Play/Settings/Exit
    [SerializeField] private GameObject playOptionsPanel;   // Painel com Multiplayer/Training

    [Header("Nomes das cenas (exactos nas Build Settings)")]
    [SerializeField] private string multiplayerSceneName = "MultiplayerLobby";
    [SerializeField] private string characterSelectSceneName = "CharacterSelect";
    [SerializeField] private string trainingSceneName = "TrainingGround";

    private void Awake()
    {
        // Apenas para garantir que o objeto que contém o MenuController está ativo.
        // Isso é um fallback de segurança se o pai estiver desativado.
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    private void Start() // <-- O Unity chama o Start() APÓS a cena carregar
    {
        // A lógica de TogglePanels deve estar aqui para forçar a ativação do painel principal
        TogglePanels(true, false); // Garante que o painel principal esteja ativo e o submenu desativo
    }

    // ---------- Botões do menu principal ----------
    public void OnPlayPressed()
    {
        TogglePanels(false, true); // esconde principal, mostra submenu
    }

    public void ExitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ---------- Botões do submenu (Play Options) ----------
    public void OnBackPressed()
    {
        TogglePanels(true, false); // volta ao menu principal
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
    // ---------- Utilitários ----------
    private void TogglePanels(bool showMain, bool showPlayOptions)
    {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(showMain);
        if (playOptionsPanel != null) playOptionsPanel.SetActive(showPlayOptions);
    }

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
