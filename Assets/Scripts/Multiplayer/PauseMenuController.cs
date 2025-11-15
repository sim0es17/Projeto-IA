using UnityEngine;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;

    private bool isPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

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
        // BLOQUEIO REMOVIDO: 
        // Não verificamos mais se o painel de nome está ativo, 
        // permitindo a pausa a qualquer momento.

        // 1. CONGELA O JOGO
        Time.timeScale = 0f;

        // 2. MOSTRA A UI DO MENU DE PAUSA
        // Linha onde a UnassignedReferenceException ocorria.
        // Certifique-se que o campo 'Pause Panel' está preenchido no Inspector!
        pausePanel.SetActive(true);

        // 3. GERE O CURSOR (LIBERTAR)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 4. ATUALIZA O ESTADO
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

        // 3. GERE O CURSOR (PRENDER)
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;

        // 4. ATUALIZA O ESTADO
        isPaused = false;
    }
}
