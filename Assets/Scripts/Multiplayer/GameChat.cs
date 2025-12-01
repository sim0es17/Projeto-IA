using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp; // Nota: Esta referência pode não ser necessária a menos que a use noutros locais.
using Photon.Pun;
using System.Collections;

public class GameChat : MonoBehaviour
{
    // NOVO: IMPLEMENTAÇÃO SINGLETON
    // A instância estática permite que outros scripts (CombatSystem, Movement) acedam a ele facilmente.
    public static GameChat instance;

    // NOVO: PROPRIEDADE PÚBLICA DE ACESSO
    // Informa outros scripts se o jogador está a escrever no chat.
    public bool IsChatOpen => isInputFiieldToggled;

    [Header("Referências")]
    public TextMeshProUGUI chatText;
    public TMP_InputField InputField;

    private bool isInputFiieldToggled;

    // Implementação do Singleton em Awake
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // Se o teu GameChat for um prefab persistente, considera DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject); // Garante que só há uma instância
        }
    }

    void Update()
    {
        // ----------------------------------------------------
        // LÓGICA DE ATIVAÇÃO DO CHAT (Tecla 'Y')
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.Y) && !isInputFiieldToggled)
        {
            isInputFiieldToggled = true;
            InputField.Select();
            InputField.ActivateInputField();

            Debug.Log("InputField ativado");
        }

        // ----------------------------------------------------
        // LÓGICA DE DESATIVAÇÃO DO CHAT (Tecla 'Escape')
        // ----------------------------------------------------
        if(Input.GetKeyDown(KeyCode.Escape) && isInputFiieldToggled)
        {
            isInputFiieldToggled = false;

            // Desseleciona o InputField para remover o foco
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

            Debug.Log("InputField desativado");
        }

        // ----------------------------------------------------
        // LÓGICA DE ENVIO DE MENSAGEM (Tecla 'Enter')
        // ----------------------------------------------------
        // Verifica Enter ou KeypadEnter, se o chat estiver ativo E se houver texto
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && isInputFiieldToggled && !InputField.text.IsNullOrEmpty())
        {
            if (isInputFiieldToggled)
            {
                // Formata a mensagem com o nome do jogador
                string messagetoSend = $"{PhotonNetwork.LocalPlayer.NickName}: {InputField.text}";

                // Envia a mensagem a todos os clientes via RPC
                GetComponent<PhotonView>().RPC("SendChatMessage", RpcTarget.All, messagetoSend);

                // Limpa o input e desativa o chat
                InputField.text = "";
                isInputFiieldToggled = false; 

                // Desseleciona o InputField
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

                Debug.Log("Mensagem enviada");
            }
        }
    }

    // Método RPC chamado para distribuir a mensagem por todos os clientes
    [PunRPC]
    void SendChatMessage(string _message)
    {
        // Adiciona a nova mensagem ao chatText
        chatText.text = chatText.text + "\n" + _message;
    }
}
