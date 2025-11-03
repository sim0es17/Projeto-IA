using UnityEngine;
using UnityEngine.UI;

public class CharacterButton : MonoBehaviour
{
    public string prefabName;          // Nome exato do prefab ("Soldier", "Chef", "Thief")
    public Image highlight;            // referência opcional ao highlight do botão

    public void OnClickChoose()
    {
        // Define o personagem selecionado
        if (CharacterSelection.Instance != null)
        {
            CharacterSelection.Instance.SetSelectedCharacter(prefabName);
            Debug.Log($"[CharacterButton] Selecionaste: {prefabName}");
        }

        // Ativa o highlight deste botão e desativa os outros
        if (highlight != null)
        {
            highlight.enabled = true;
        }

        // Desativar o highlight dos outros botões
        foreach (CharacterButton btn in FindObjectsOfType<CharacterButton>())
        {
            if (btn != this && btn.highlight != null)
                btn.highlight.enabled = false;
        }
    }
}
