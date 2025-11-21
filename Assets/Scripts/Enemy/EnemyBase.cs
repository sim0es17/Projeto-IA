using UnityEngine;
using Photon.Pun;

// Esta classe herda de MonoBehaviourPunCallbacks, tal como os teus inimigos faziam
public abstract class EnemyBase : MonoBehaviourPunCallbacks
{
    // Obriga os "filhos" a terem estas variáveis acessíveis
    public abstract float KnockbackForce { get; }
    public abstract float StunTime { get; }

    // Define o método RPC que ambos os inimigos têm de ter
    [PunRPC]
    public abstract void ApplyKnockbackRPC(Vector2 direction, float force, float time);
}
