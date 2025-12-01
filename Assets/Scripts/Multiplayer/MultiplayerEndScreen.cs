using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class MultiplayerEndScreen : MonoBehaviour
{
    [Header("Paineis UI")]
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Configuração")]
    public string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        // Garante que começam desligados
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
    }

    public void ShowVictory()
    {
        Debug.Log("VITÓRIA! És o último sobrevivente.");
        if (winPanel != null) winPanel.SetActive(true);
        if (losePanel != null) losePanel.SetActive(false);
    }

    public void ShowDefeat()
    {
        Debug.Log("DERROTA! Acabaram-se as vidas.");
        if (losePanel != null) losePanel.SetActive(true);
        if (winPanel != null) winPanel.SetActive(false);
    }

    // Liga esta função ao botão "Back to Main Menu" no Inspector
    public void GoToMainMenu()
    {
        Time.timeScale = 1f; // Descongela o tempo

        // Sai da sala do Photon
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.LeaveRoom();
        }

        // Carrega a cena do menu
        SceneManager.LoadScene(mainMenuSceneName);
    }
}