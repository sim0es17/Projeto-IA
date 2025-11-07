using UnityEngine;
using Photon.Pun;

public class HealthPowerup : MonoBehaviour
{
    public int healAmount = 20;
    [HideInInspector] public HealthPowerupSpawner spawner;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Health health = other.GetComponentInParent<Health>();
        if (health == null)
            return;

        PhotonView targetView = health.GetComponent<PhotonView>();
        if (targetView != null)
            targetView.RPC("Heal", RpcTarget.All, healAmount);

        if (spawner != null)
            spawner.PowerupApanhado();

        Destroy(gameObject);
    }
}
