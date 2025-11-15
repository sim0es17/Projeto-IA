using UnityEngine;

public class MouseConfiner : MonoBehaviour
{
    void Start()
    {
        // Começa o jogo com o cursor confinado. 
        // Se estiver num menu inicial, pode mudar para LockCursorImmediate() se quiser o cursor logo visível.
        LockCursor();
    }

    void Update()
    {
        // Se o utilizador clicar na janela (botão esquerdo)
        // E o cursor estiver atualmente livre (estado CursorLockMode.None),
        // confina-o novamente.
        // Isto é crucial para voltar ao jogo depois de sair da pausa (ESC).
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
        {
            // Verifica se o jogo está em execução (não pausado), se for o caso
            // if (Time.timeScale > 0f) 
            // {
            LockCursor();
            // }
        }
    }

    /// <summary>
    /// Função para prender o cursor
    /// </summary>
    void LockCursor()
    {
        // Confina o cursor à janela do jogo
        Cursor.lockState = CursorLockMode.Confined;

        // Garante que o cursor permanece visível,
        // mas é controlado pelo MouseConfiner
        Cursor.visible = true;
    }

    // NOTA IMPORTANTE: A lógica de libertar o cursor ao premir ESC
    // FOI REMOVIDA DAQUI. 
    // AGORA DEVE ESTAR EM PauseMenuController.PauseGame()
}
