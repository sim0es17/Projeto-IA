// BackToMenu.cs
using UnityEngine;
using UnityEngine.SceneManagement; // É necessário para SceneManager.LoadScene no fallback

public class BackToMenu : MonoBehaviour
{
    // Variável para pores o nome da tua Scene do menu principal no Inspector
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    /// <summary>
    /// Esta é a função pública que vais ligar ao teu botão.
    /// É a responsável por iniciar o processo de saída da sala/treino e carregar o menu.
    /// </summary>
    public void GoToMainMenu()
    {
        // 1. Tenta encontrar o RoomManager (para jogos multiplayer normais)
        if (RoomManager.instance != null)
        {
            Debug.Log("[BackToMenu] A notificar RoomManager para sair.");
            // Pede ao RoomManager para tratar da saída (LeaveRoom) e carregar o menu.
            RoomManager.instance.LeaveGameAndGoToMenu(mainMenuSceneName);
        }
        // 2. Tenta encontrar o TGRoomManager (para o modo de treino)
        else if (TGRoomManager.instance != null)
        {
            Debug.Log("[BackToMenu] A notificar TGRoomManager para sair do treino.");
            // Pede ao TGRoomManager para tratar da limpeza e desconexão (Disconnect).
            TGRoomManager.instance.LeaveGameAndGoToMenu(mainMenuSceneName);
        }
        else
        {
            // 3. Fallback: Se não estiver conectado (para testes offline)
            Debug.LogWarning("Nenhum RoomManager/TGRoomManager encontrado! A carregar o menu diretamente. (Teste offline)");
            // Garante que o tempo não está pausado
            Time.timeScale = 1f;
            // Carrega a cena do menu diretamente
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
