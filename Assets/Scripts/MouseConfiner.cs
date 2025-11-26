using UnityEngine;

public class MouseConfiner : MonoBehaviour
{
    void Start()
    {
        // Garante que o cursor está confinado à janela do jogo.
        // O cursor permanece visível (Cursor.visible = true; é o padrão, mas explicitamos por clareza).
        Cursor.lockState = CursorLockMode.Confined;
        
        // Mantém o cursor visível.
        Cursor.visible = true; 

        // O Update() foi removido, pois toda a lógica de pausa (libertar/confinar) é gerida pelo PMMM.
    }
}
