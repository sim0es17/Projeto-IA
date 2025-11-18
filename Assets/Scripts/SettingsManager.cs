using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SettingsManager : MonoBehaviour
{
    // Variáveis de Volume
    public Slider volumeSlider;
    private const string VolumeKey = "MasterVolume";

    // Variáveis de Fullscreen
    public Toggle fullscreenToggle;
    private const string FullscreenKey = "IsFullscreen";

    void Start()
    {
        // === Configuração de Volume ===
        float savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1.0f); // Padrão: 100%

        if (volumeSlider != null)
        {
            volumeSlider.value = savedVolume;
            // Liga a função SetVolume ao evento de mudança do slider
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        AudioListener.volume = savedVolume; // Aplica o volume inicial

        // === Configuração de Fullscreen ===
        if (fullscreenToggle != null)
        {
            // Carrega o estado guardado (1 para true, 0 para false), padrão: 1 (Fullscreen)
            bool isFullscreen = PlayerPrefs.GetInt(FullscreenKey, 1) == 1;

            fullscreenToggle.isOn = isFullscreen;
            // Liga a função SetFullscreen ao evento de mudança do toggle
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);

            // Aplica o estado ao jogo no início
            Screen.fullScreen = isFullscreen;
        }
    }

    // Função chamada pelo Volume Slider
    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(VolumeKey, volume);
        PlayerPrefs.Save();
    }

    // Função chamada pelo Fullscreen Toggle
    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;

        // Guarda o estado para a próxima sessão
        PlayerPrefs.SetInt(FullscreenKey, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Função chamada pelo botão Back
    public void BackToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}