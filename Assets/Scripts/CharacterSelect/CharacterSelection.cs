using UnityEngine;

public class CharacterSelection : MonoBehaviour
{
    public static CharacterSelection Instance;

    public string selectedPrefabName; // ex: "SoldierPlayer", "ChefPlayer"

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        selectedPrefabName = PlayerPrefs.GetString("selectedCharacter", "SoldierPlayer");
    }

    public void Choose(string prefabName)
    {
        selectedPrefabName = prefabName;
        PlayerPrefs.SetString("selectedCharacter", prefabName);
    }
}
