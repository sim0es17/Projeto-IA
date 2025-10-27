using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    // Função chamada ao clicar no botão "Jogar"
    public void StartGame()
    {
        SceneManager.LoadScene("MainScene");
        // Confirma que "MainScene" é exatamente o nome da tua cena de jogo
    }

    // Função chamada ao clicar no botão "Sair"
    public void ExitGame()
    {
        Application.Quit();  // Fecha o jogo na build final

        // Para também parar o Play Mode dentro do Unity Editor
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
