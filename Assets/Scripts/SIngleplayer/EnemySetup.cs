using Photon.Pun;
using TMPro;
using UnityEngine;

public class EnemySetup : MonoBehaviour
{
    public Movement2D movement;

    public CombatSystem2D combat;

    public string nickname;

    public TextMeshPro nicknameText;

    public void IsLocalEnemy()
    {
        movement.enabled = true;

        // Enable combat system for the local enemy only
        if (combat != null)
            combat.enabled = true;
    }

    /*[PunRPC]
    public void SetNickname(string _nickname)
    {
        nickname = _nickname;

        nicknameText.text = nickname;
    }*/
}
