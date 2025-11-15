using UnityEngine;
using Photon.Pun; // Necessário para ter acesso ao RoomManager/TGRoomManager se forem scripts Pun

public class PauseMenuController : MonoBehaviour
{
    // A UI do Menu de Pausa (configurada no Inspector)
    [SerializeField] private GameObject pausePanel;

    private bool isPaused = false;

    // Deve ser chamada sempre que o utilizador prime a tecla de Pausa (ex: Escape)
    void Update()
    {
        // Se a Unity não tiver foco, não queremos registar o input
        if (!Application.isFocused) return;

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
        // 1. VERIFICAÇÃO DE PRÉ-PAUSA: Se estivermos numa fase de conexão/configuração, bloqueia a pausa.

        // Se estivermos no modo normal/multiplayer, verifica o painel de nome
        if (RoomManager.instance != null)
        {
            // Se o painel de nome ainda estiver ativo, estamos a configurar. Bloqueia a pausa.
            if (RoomManager.instance.IsNamePanelActive)
            {
                Debug.LogWarning("Não é possível pausar: O painel de nome/conexão ainda está ativo.");
                return;
            }
        }
        // Se estivermos no modo de treino e ainda a conectar, também poderíamos bloquear.
        // No TGRoomManager, ele conecta e junta-se à sala imediatamente, portanto,
        // não é estritamente necessário bloquear, mas mantemos a estrutura de verificação
        else if (TGRoomManager.instance != null)
        {
            // Adicionar aqui qualquer verificação de UI específica do modo de treino, se existir.
        }


        // 2. CONGELA O JOGO
        Time.timeScale = 0f;

        // 3. MOSTRA A UI DO MENU DE PAUSA
        pausePanel.SetActive(true);

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

        // 3. ATUALIZA O ESTADO
        isPaused = false;
    }
}
