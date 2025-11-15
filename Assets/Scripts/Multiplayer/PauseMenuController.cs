using UnityEngine;

public class PauseMenuController : MonoBehaviour
{
    // A UI do Menu de Pausa (configurada no Inspector)
    [SerializeField] private GameObject pausePanel;

    private bool isPaused = false;

    // Deve ser chamada sempre que o utilizador prime a tecla de Pausa (ex: Escape)
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    /// <summary>
    /// Alterna entre o estado de Pausa e Jogo.
    /// </summary>
    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    /// <summary>
    /// Congela o jogo e mostra o painel.
    /// </summary>
    public void PauseGame()
    {
        if (RoomManager.instance != null && RoomManager.instance.IsNamePanelActive)
        {
            // Impede que o jogo entre em pausa se o painel de nome ainda estiver ativo
            return;
        }

        // 1. CONGELA O JOGO
        Time.timeScale = 0f;

        // 2. MOSTRA A UI DO MENU DE PAUSA
        pausePanel.SetActive(true);

        // 3. ATUALIZA O ESTADO
        isPaused = true;
    }

    /// <summary>
    /// Descongela o jogo e esconde o painel.
    /// </summary>
    public void ResumeGame()
    {
        // 1. DESCONGELA O JOGO
        Time.timeScale = 1f;

        // 2. ESCONDE A UI DO MENU DE PAUSA
        pausePanel.SetActive(false);

        // 3. ATUALIZA O ESTADO
        isPaused = false;
    }
}
