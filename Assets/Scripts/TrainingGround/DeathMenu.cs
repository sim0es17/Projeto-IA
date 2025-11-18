using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class DeathMenu : MonoBehaviour
{
    [Header("Referências UI")]
    public GameObject panel;        // DeathPanel
    public Camera deathCamera;      // DeathCamera (opcional)

    [Header("Nomes das cenas")]
    public string restartSceneName;    // cena para dar restart (ex: "TrainingGround2")
    public string mainMenuSceneName;   // cena do menu principal (ex: "MainMenu")

    private bool isOpen = false;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);

        // TESTE: Vamos deixar a câmara LIGADA desde o início para ver se ela renderiza
        if (deathCamera != null) deathCamera.enabled = true;
    }

    public void Show()
    {
        if (isOpen) return;
        isOpen = true;

        Debug.Log("[DeathMenu] Show chamado, a activar DeathCamera.");

        if (panel != null) panel.SetActive(true);

        if (deathCamera != null)
        {
            // PASSO CRUCIAL: Garante que o OBJETO está ligado primeiro!
            deathCamera.gameObject.SetActive(true);

            // Depois liga o componente câmara
            deathCamera.enabled = true;

            Debug.Log("[DeathMenu] deathCamera ligada com sucesso.");
        }
        else
        {
            Debug.LogWarning("[DeathMenu] deathCamera NÃO está ligada no Inspector!");
        }
    }
    // BOTÃO: Restart
    public void Restart()
    {
        // Time.timeScale = 1f; // se estiveres a usar pausa

        Debug.Log("[DeathMenu] Restart clicado");

        if (string.IsNullOrEmpty(restartSceneName))
        {
            // se não definiste nada no Inspector, usa a cena actual
            restartSceneName = SceneManager.GetActiveScene().name;
        }

        // Se estiveres numa room do Photon, sair primeiro (opcional)
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        SceneManager.LoadScene(restartSceneName);
    }

    // BOTÃO: Main Menu
    public void GoToMainMenu()
    {
        // Time.timeScale = 1f; // se estiveres a usar pausa

        Debug.Log("[DeathMenu] Main Menu clicado");

        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogWarning("[DeathMenu] mainMenuSceneName não está definido no Inspector!");
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
