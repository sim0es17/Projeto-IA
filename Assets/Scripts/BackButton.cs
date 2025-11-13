using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButtonToMenu : MonoBehaviour
{
    // O nome da cena que será carregada (o Menu Principal)
    [Tooltip("Insira o nome exato da cena do Main Menu.")]
    public string mainMenuSceneName = "MainMenu";

    /// <summary>
    /// Função que é chamada quando o botão "Back" é clicado.
    /// Carrega a cena do Menu Principal.
    /// </summary>
    public void OnClickBack()
    {
        // Verifica se o nome da cena foi definido para evitar erros
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("O nome da cena 'Main Menu' não está definido no Inspetor!");
            return;
        }

        // Carrega a cena especificada
        SceneManager.LoadScene(mainMenuSceneName);

        Debug.Log("Voltando para a cena: " + mainMenuSceneName);
    }
}
