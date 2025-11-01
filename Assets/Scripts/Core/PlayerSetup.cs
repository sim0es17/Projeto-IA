using Photon.Pun;
using TMPro;
using UnityEngine;

public class PlayerSetup : MonoBehaviour
{
    public Movement2D movement;

    public GameObject camara;

    public CombatSystem2D combat;

    public string nickname;

    public TextMeshPro nicknameText;

    public void IsLocalPlayer()
    {
        movement.enabled = true;
        camara.SetActive(true);

        // Enable combat system for the local player only
        if (combat != null)
            combat.enabled = true;
    }

    [PunRPC]
    public void SetNickname(string _nickname)
    {
        nickname = _nickname;

        nicknameText.text = nickname;
    }
}
