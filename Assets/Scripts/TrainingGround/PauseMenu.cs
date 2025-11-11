using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public bool IsPaused { get; private set; }

    private void Awake()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        Time.timeScale = 1f;
        IsPaused = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);

        Time.timeScale = 0f;   // pausa o jogo
        IsPaused = true;
    }

    public void ResumeGame()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        Time.timeScale = 1f;   // retoma o jogo
        IsPaused = false;
    }

    public void QuitToMainMenu()
    {
        // garantir que o tempo volta ao normal
        Time.timeScale = 1f;
        IsPaused = false;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
