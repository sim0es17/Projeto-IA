using UnityEngine;
using System.Collections;

public class WelcomeMessage : MonoBehaviour
{
    [Header("Configuração")]
    public GameObject welcomePanel; // O Painel (fundo preto + texto)
    public float duration = 5f;     // Tempo que fica visível

    private IEnumerator Start()
    {
        // 1. SEGURANÇA: Começa com o painel DESLIGADO.
        // Assim não tapa o teu ecrã de "Connecting".
        if (welcomePanel != null)
            welcomePanel.SetActive(false);

        // 2. ESPERA: Fica num loop à espera que o Player nasça.
        // Ele procura um objeto com a tag "Player".
        while (GameObject.FindGameObjectWithTag("Player") == null)
        {
            yield return null; // Espera um frame e tenta de novo
        }

        // 3. AÇÃO: O Player nasceu! Liga o painel agora.
        if (welcomePanel != null)
            welcomePanel.SetActive(true);

        // 4. DURAÇÃO: Espera os 5 segundos com o painel ligado.
        yield return new WaitForSeconds(duration);

        // 5. FIM: Desliga o painel.
        if (welcomePanel != null)
            welcomePanel.SetActive(false);
    }
}