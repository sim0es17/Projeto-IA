using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterButton : MonoBehaviour
{
    public string prefabName;      // "SoldierPlayer" ou "ChefPlayer"
    public string sceneToLoad;     // "TrainingGround" ou "Multiplayer"

    public void OnClickChoose()
    {
        if (CharacterSelection.Instance == null)
        {
            var go = new GameObject("__CharacterSelection");
            go.AddComponent<CharacterSelection>();
        }

        CharacterSelection.Instance.Choose(prefabName);
        SceneManager.LoadScene(sceneToLoad);
    }
}
