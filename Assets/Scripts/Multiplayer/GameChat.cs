using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

public class GameChat : MonoBehaviour
{
    [Header("Referências")]
    public TextMeshProUGUI chatText;
    public TMP_InputField InputField;


    private bool isInputFiieldToggled;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Y) && !isInputFiieldToggled)
        {
            isInputFiieldToggled = true;
            InputField.Select();
            InputField.ActivateInputField();

            Debug.Log("InputField ativado");
        }

        if(Input.GetKeyDown(KeyCode.Escape) && isInputFiieldToggled)
        {
            isInputFiieldToggled = false;

            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

            Debug.Log("InputField desativado");
        }

        //mandar smg
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) && isInputFiieldToggled && !InputField.text.IsNullOrEmpty())
        {
            if (isInputFiieldToggled)
            {
                InputField.text = "";
                isInputFiieldToggled = false;

                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

                Debug.Log("Mensagem enviada");
            }
        }
    }

    /*void SendMessageToChat(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            chatText.text += "\n" + message;
        }
    }*/
}