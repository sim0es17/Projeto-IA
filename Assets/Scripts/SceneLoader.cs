using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // Esta função será chamada quando o botão for clicado
    public void LoadMainMenu()
    {
        // Usa o nome da sua scene (o mesmo que você me forneceu)
        SceneManager.LoadScene("MainMenu");
    }

    // Você pode adicionar outras funções como QuitGame() ou LoadLevel() aqui
}
