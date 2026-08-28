using UnityEngine;
using UnityEngine.UI;

public class PanelSetting : UICanvas
{
    [Header("Buttons")]
    [SerializeField] private Button musicButton;
    [SerializeField] private Button vibrateButton;
    [SerializeField] private Button soundButton;
    [SerializeField] private Button closeButton;

    [Header("Images")]
    [SerializeField] private Image musicImage;
    [SerializeField] private Image vibrateImage;
    [SerializeField] private Image soundImage;

    [Header("Sprites")]
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;
    [SerializeField] private Sprite vibrateOnSprite;
    [SerializeField] private Sprite vibrateOffSprite;

    public override void SetUp()
    {
        base.SetUp();
        FindReferences();
        BindButtons();
        RefreshState();
    }

    private void OnEnable()
    {
        RefreshState();
    }

    private void OnMusicClicked()
    {
        AudioManager audioManager = AudioManager.GetOrCreate();
        audioManager.PlayButtonClick();
        audioManager.ToggleMusic();
        RefreshState();
    }

    private void OnVibrateClicked()
    {
        AudioManager audioManager = AudioManager.GetOrCreate();
        audioManager.ToggleVibrate();

        if (audioManager.IsVibrateEnabled)
        {
            audioManager.PlayLightHaptic();
        }

        RefreshState();
    }

    private void OnSoundClicked()
    {
        AudioManager audioManager = AudioManager.GetOrCreate();
        audioManager.PlayButtonClick();
        audioManager.ToggleSound();
        RefreshState();
    }

    private void OnCloseClicked()
    {
        AudioManager.GetOrCreate().PlayButtonClick();
        CloseDirectly();
    }

    private void RefreshState()
    {
        AudioManager audioManager = AudioManager.GetOrCreate();

        SetSprite(musicImage, audioManager.IsMusicEnabled ? musicOnSprite : musicOffSprite);
        SetSprite(vibrateImage, audioManager.IsVibrateEnabled ? vibrateOnSprite : vibrateOffSprite);
        SetSprite(soundImage, audioManager.IsSoundEnabled ? soundOnSprite : soundOffSprite);
    }

    private void BindButtons()
    {
        BindButton(musicButton, OnMusicClicked);
        BindButton(vibrateButton, OnVibrateClicked);
        BindButton(soundButton, OnSoundClicked);
        BindButton(closeButton, OnCloseClicked);
    }

    private void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void SetSprite(Image targetImage, Sprite sprite)
    {
        if (targetImage != null && sprite != null)
        {
            targetImage.sprite = sprite;
        }
    }

    private void FindReferences()
    {
        if (musicButton == null)
        {
            musicButton = FindButton("Music");
        }

        if (vibrateButton == null)
        {
            vibrateButton = FindButton("Vibrate");
        }

        if (soundButton == null)
        {
            soundButton = FindButton("Sound");
        }

        if (closeButton == null)
        {
            closeButton = FindButton("CloseBtn");
        }

        if (musicImage == null && musicButton != null)
        {
            musicImage = musicButton.GetComponent<Image>();
        }

        if (vibrateImage == null && vibrateButton != null)
        {
            vibrateImage = vibrateButton.GetComponent<Image>();
        }

        if (soundImage == null && soundButton != null)
        {
            soundImage = soundButton.GetComponent<Image>();
        }

        if (musicOnSprite == null && musicImage != null)
        {
            musicOnSprite = musicImage.sprite;
        }

        if (vibrateOnSprite == null && vibrateImage != null)
        {
            vibrateOnSprite = vibrateImage.sprite;
        }

        if (soundOnSprite == null && soundImage != null)
        {
            soundOnSprite = soundImage.sprite;
        }
    }

    private Button FindButton(string objectName)
    {
        Transform target = FindChildByName(transform, objectName);
        if (target == null)
        {
            return null;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.gameObject.AddComponent<Button>();
        }

        return button;
    }

    private Transform FindChildByName(Transform root, string objectName)
    {
        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
