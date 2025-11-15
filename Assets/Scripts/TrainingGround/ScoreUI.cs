//ScoreUI
using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts; // Para GetScore()
using TMPro;
using Photon.Realtime;
using ExitGames.Client.Photon;

// Passamos a herdar de MonoBehaviourPunCallbacks para usar o callback OnPlayerPropertiesUpdate
public class ScoreUI : MonoBehaviourPunCallbacks
{
    public TextMeshProUGUI scoreText;

    private int lastScore = int.MinValue;

    void Start()
    {
        // Garante que o score é inicializado e mostrado no início
        UpdateScoreText();
    }

    void Update()
    {
        // ATENÇÃO: Embora este Update() funcione, a abordagem mais eficiente
        // é usar o callback OnPlayerPropertiesUpdate.
        // Mantenho-o por segurança, mas o OnPlayerPropertiesUpdate é o preferido.
        int currentScore = PhotonNetwork.LocalPlayer.GetScore();

        if (currentScore != lastScore)
        {
            lastScore = currentScore;
            UpdateScoreText();
        }
    }

    void UpdateScoreText()
    {
        if (scoreText == null) return;

        int currentScore = PhotonNetwork.LocalPlayer.GetScore();
        scoreText.text = $"Score: {currentScore}";
    }

    // --- NOVO MÉTODO (Callback da Photon) ---
    // Este método é chamado sempre que as propriedades personalizadas de um jogador mudam,
    // incluindo o score (que é uma propriedade personalizada gerida pelo Photon.Pun.UtilityScripts).
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        // Só queremos atualizar a nossa UI se for o nosso próprio score a mudar.
        if (targetPlayer == PhotonNetwork.LocalPlayer)
        {
            // Verificamos se a propriedade "Score" (key = PhotonPlayer.Score) foi alterada
            if (changedProps.ContainsKey(PunPlayerScores.PlayerScoreProp))
            {
                // Se o score mudou, forçamos a atualização da UI
                // (Isto elimina a necessidade do Update(), mas mantive-o por robustez)
                int currentScore = targetPlayer.GetScore();

                // Evitamos a chamada desnecessária ao UpdateScoreText se o score for o mesmo
                if (currentScore != lastScore)
                {
                    lastScore = currentScore;
                    UpdateScoreText();
                }

                // Debug.Log($"Score do jogador local atualizado via callback para: {currentScore}");
            }
        }
    }
}