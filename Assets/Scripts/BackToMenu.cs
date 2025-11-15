//BackToMenu.cs
using UnityEngine;

// Não precisas de SceneManagement ou Photon.Pun aqui,
// porque este script agora só delega a tarefa.

public class BackToMenu : MonoBehaviour
{
    // Variavel para pores o nome da tua Scene do menu principal no Inspector
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    /// <summary>
    /// Esta é a função pública que vais ligar ao teu botão.
    /// </summary>
    public void GoToMainMenu()
    {
        // Tenta encontrar o RoomManager
        if (RoomManager.instance != null)
        {
            // Pede ao RoomManager para tratar da saída e carregar o menu
            RoomManager.instance.LeaveGameAndGoToMenu(mainMenuSceneName);
        }
        else
        {
            // Fallback: Se não encontrar o RoomManager (ex: testar sem rede),
            // faz a versão antiga e "errada" só para funcionar no Editor
            Debug.LogWarning("RoomManager.instance não encontrado! A carregar o menu diretamente. (Isto é normal se estiveres a testar o menu de pausa offline)");
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
