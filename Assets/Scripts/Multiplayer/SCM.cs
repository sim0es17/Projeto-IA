using UnityEngine;
using UnityEngine.SceneManagement;

public class SCM : MonoBehaviour
{
    // Mantemos a estática para acesso rápido, mas o importante agora é o PlayerPrefs
    public static string selectedCharacter = "None";

    public enum CharacterType
    {
        None,
        Soldier,
        Skeleton,
        Knight,
        Orc
    }

    // Método chamado pelos botões (String)
    public void SelectCharacter(string characterName)
    {
        selectedCharacter = characterName;

        // --- A CORREÇÃO MÁGICA ---
        // Guarda a escolha na memória permanente do jogo.
        // Assim, quando mudas de cena, o RoomManager consegue ler isto!
        PlayerPrefs.SetString("SelectedCharacter", characterName);

        Debug.Log("Personagem selecionado e salvo: " + selectedCharacter);
    }

    // Método chamado pelos botões (Enum - Opcional se usares o de cima)
    public void SelectCharacter(CharacterType character)
    {
        string charName = character.ToString();

        if (character == CharacterType.None)
            charName = "None";

        // Chama a função principal para salvar
        SelectCharacter(charName);
    }

    public void GoToLobby()
    {
        // Verifica se escolheu algum personagem válido
        if (selectedCharacter == "None" || string.IsNullOrEmpty(selectedCharacter))
        {
            Debug.LogError("Por favor, selecione um personagem antes de clicar em Play.");
            return;
        }

        // Carrega a cena do Lobby
        // Certifica-te que o nome da cena aqui é EXATAMENTE igual ao da tua cena de Lobby
        SceneManager.LoadScene("MultiplayerLobby");
    }
}