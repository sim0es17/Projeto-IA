using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;
using Photon.Pun;
using System.Collections;
using System;

public class GameChat : MonoBehaviour
{
    // 1. SINGLETON
    public static GameChat instance;

    [Header("Referências")]
    public TextMeshProUGUI chatText;
    public TMP_InputField InputField;

    // 2. PROPRIEDADE PÚBLICA
    private bool isInputFiieldToggled;
    public bool IsChatOpen => isInputFiieldToggled; 

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Y) && !isInputFiieldToggled)
        {
            isInputFiieldToggled = true;
            InputField.Select();
            InputField.ActivateInputField();
            Debug.Log("InputField ativado");
        }

        // 3. PRIORIDADE ESCAPE
        if(Input.GetKeyDown(KeyCode.Escape) && isInputFiieldToggled)
        {
            CloseChat(); 
            return; 
        }

        // Enviar mensagem
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && isInputFiieldToggled && !InputField.text.IsNullOrEmpty())
        {
            if (isInputFiieldToggled)
            {
                string messagetoSend = $"{PhotonNetwork.LocalPlayer.NickName}: {InputField.text}";
                GetComponent<PhotonView>().RPC("SendChatMessage", RpcTarget.All, messagetoSend);
                InputField.text = "";
                CloseChat();
                Debug.Log("Mensagem enviada");
            }
        }
    }

    public void CloseChat()
    {
        isInputFiieldToggled = false;
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
             UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
        Debug.Log("InputField desativado");
    }

    [PunRPC]
    void SendChatMessage(string _message)
    {
        chatText.text = chatText.text + "\n" + _message;
    }
}
