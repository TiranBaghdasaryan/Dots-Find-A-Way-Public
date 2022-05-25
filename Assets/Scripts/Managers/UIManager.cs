using System.Runtime.CompilerServices;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject _menuUI;
    public static GameObject MenuUI => Instance._menuUI;

    [SerializeField] private GameObject _settingsUI;
    public static GameObject SettingsUI => Instance._settingsUI;

    [SerializeField] private TMP_Text _levelTextInMenu;
    [SerializeField] private TMP_Text _levelTextInGame;

    [SerializeField]  private TMP_Text _helpCountText;
    
    [SerializeField] private Button _helpButton;
    public static Button HelpButton => Instance._helpButton;


    [SerializeField] private Sprite _soundOn;
    [SerializeField] private Sprite _soundOff;

    [SerializeField] private Image _soundIcon;


    private Vector2 settingsInitialUITransform;

    public static void ChangeSoundSprite(bool value)
    {
        if (value)
        {
            Instance._soundIcon.sprite = Instance._soundOn;
        }
        else
        {
            Instance._soundIcon.sprite = Instance._soundOff;
        }
    }

    public static void ChangeHelpCountText(string text)
    {
        Instance._helpCountText.text = text;
    }
    public static void ChangeLevelTextInMenu(string text)
    {
        Instance._levelTextInMenu.text = text;
    }

    public static void ChangeLevelTextInGame(string text)
    {
        Instance._levelTextInGame.text = text;
    }


    private void Start()
    {
        settingsInitialUITransform = _settingsUI.transform.position;
    }

    private void Awake()
    {
        Instance = this;
    }

    public static UIManager Instance;


    public void OnBackFromSettings()
    {
        SettingsUI.transform.DOMove(settingsInitialUITransform, .5f);
    }
    public void OnSettingsButton()
    {
        SettingsUI.transform.DOMove(Vector3.zero, .5f);
    }

    public void OnSoundButton()
    {
        Data.IsSoundOn = !Data.IsSoundOn;
    }

    public void OnHomeButton()
    {
        NextLevelButton.Reset();
        GameManager.ClearArea();
        MenuUI.SetActive(true);
        HelpButton.interactable = true;
    }

    public void OnRateUsButton()
    {
        Application.OpenURL("https://play.google.com/store/apps");
    }
}