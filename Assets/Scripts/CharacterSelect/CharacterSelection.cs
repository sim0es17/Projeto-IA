using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterSelection : MonoBehaviour
{
    // Singleton para ser fácil aceder de outras cenas
    public static CharacterSelection Instance { get; private set; }

    // Nome do prefab escolhido (é isto que o TGRoomManager vai ler)
    // Por defeito deixo "Soldier" caso o jogador não escolha nada.
    public string selectedPrefabName = "Soldier";

    private void Awake()
    {
        // Garantir que só existe um CharacterSelection e que sobrevive às cenas
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Chamado pelos botões da CharacterSelect
    public void SetSelectedCharacter(string prefabName)
    {
        selectedPrefabName = prefabName;
        Debug.Log($"[CharacterSelection] Personagem escolhida: {selectedPrefabName}");
    }

    // Opcional: função para o botão Play chamar
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
