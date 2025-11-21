using UnityEngine;
using UnityEngine.SceneManagement; // <--- MUITO IMPORTANTE: Permite mudar de cenas

public class SceneNavigator : MonoBehaviour
{
    [Header("Nomes Exatos das Cenas")]
    [Tooltip("Escreve aqui o nome exato da cena do Menu Principal")]
    public string mainMenuSceneName = "MainMenu";

    [Tooltip("Escreve aqui o nome exato da cena da Arena 3")]
    public string nextLevelSceneName = "Arena 3";

    // Função para o botão "Menu Principal"
    public void LoadMainMenu()
    {
        Debug.Log("A tentar carregar o menu: " + mainMenuSceneName);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // Função para o botão "Arena 3"
    public void LoadNextLevel()
    {
        Debug.Log("A tentar carregar o próximo nível: " + nextLevelSceneName);
        SceneManager.LoadScene(nextLevelSceneName);
    }
}