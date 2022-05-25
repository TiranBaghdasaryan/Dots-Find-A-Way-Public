using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    #region Variables From Inspector

    [SerializeField] private BannerAdExample _bannerAdExample;


    [SerializeField] private GameObject _hand;
    [SerializeField] Color32[] colors;
    [SerializeField] private GameObject _area;
    [SerializeField] Dot _dotObject;
    [SerializeField] GameObject _blockObject;

    [SerializeField] private RewardedAdsButton _rewardedAdsButton;

    #endregion

    public static Dictionary<Vector2Int, Dot> dots;

    private string[] _levelDesign;
    private int restartLevel = 0;

    private void Awake()
    {
        PlayerPrefs.DeleteAll();
        Application.targetFrameRate = 60;
        Instance = this;
    }

    public static GameManager Instance;

    private void Start()
    {
        if (PlayerPrefs.HasKey("sound"))
            Data.IsSoundOn = Convert.ToBoolean(PlayerPrefs.GetInt("sound"));

        if (PlayerPrefs.HasKey("level"))
            Data.CurrentLevel = PlayerPrefs.GetInt("level");

        if (PlayerPrefs.HasKey("helpCount"))
            Data.HelpCount = PlayerPrefs.GetInt("helpCount");

        dots = new Dictionary<Vector2Int, Dot>();
    }

    public void MakeLevel(int levelNumber)
    {
        Color32 color = colors[Random.Range(0, colors.Length)];

        UIManager.ChangeLevelTextInGame(levelNumber.ToString());
        NextLevelButton.Reset();
        restartLevel = levelNumber;
        LevelData _levelDesign;

        _levelDesign = Resources.Load<LevelData>($"Levels/LevelData_{levelNumber}");
        CreateLevel();

        void CreateLevel()
        {
            dots.Clear();
            ClearArea();

            _area.GetComponent<RectTransform>().sizeDelta =
                new Vector2((_levelDesign.size.x - 1) * 150 + 100,
                    (_levelDesign.size.y - 1) * 150 + 100);

            int x = 0;
            int y = 0;


            for (int i = 0; i < _levelDesign.size.y; i++)
            {
                x = 0;
                for (int j = 0; j < _levelDesign.size.x; j++)
                {
                    if (_levelDesign.GetBlock(j, i) == LevelData.BlockType.Empty)
                    {
                        Dot dot = Instantiate(_dotObject, new Vector2(x, y), Quaternion.identity, _area.transform)
                            .Init(i, j, color);
                        dot.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
                    }

                    if (_levelDesign.GetBlock(j, i) == LevelData.BlockType.Wall)
                    {
                        GameObject block = Instantiate(_blockObject, new Vector2(x, y), Quaternion.identity,
                            _area.transform);

                        block.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
                    }

                    if (_levelDesign.GetBlock(j, i) == LevelData.BlockType.Start)
                    {
                        Dot dot = Instantiate(_dotObject, new Vector2(x, y), Quaternion.identity, _area.transform)
                            .Init(i, j, color);
                        dot.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
                        dot.IsCurrent = true;
                        Dot.currentDot = dot;
                    }

                    x += 150;
                }

                y -= 150;
            }

            if (levelNumber.Equals(1))
            {
                UIManager.HelpButton.interactable = false;
                Dot dot =_area.transform.GetChild(1).GetComponent<Dot>();
                Hand hand = Instantiate(_hand, _area.transform).GetComponent<Hand>().Init(
                        _area.transform.GetChild(_area.transform.childCount - 2).transform.position.x,
                        color,
                        dot
                       
                    );
            }
        }
    }

    public static void ClearArea()
    {
        for (int i = 0; i < Instance._area.transform.childCount; i++)
            Destroy(Instance._area.transform.GetChild(i).gameObject);
    }

    public static void CheckWin()
    {
        bool isWin = dots.All(dot => dot.Value.IsActive.Equals(true));
        if (isWin)
            Instance.OnWin();
    }

    private void OnWin()
    {
        AudioManager.CallAudioClipWin();
        UIManager.HelpButton.interactable = false;
        if (restartLevel == Data.CurrentLevel)
            Data.CurrentLevel += 1;

        Dot.currentDot.LastDot.GetComponent<Image>().raycastTarget = false;
        NextLevelButton.OnWinAnimation();
    }

    public void OnPlayButton()
    {
        UIManager.MenuUI.SetActive(false);
        MakeLevel(Data.CurrentLevel);
        _bannerAdExample.LoadBanner();
    }


    public void OnNextLevelButton()
    {
        NextLevelButton.Reset();
        UIManager.HelpButton.interactable = true;
        UIManager.ChangeLevelTextInGame(Data.CurrentLevel.ToString());
        MakeLevel(Data.CurrentLevel);
    }

    public void OnRestartButton()
    {
        MakeLevel(restartLevel);
    }


    public void OnHelpButton()
    {
        if (Data.HelpCount == 0)
        {
            //to do// suggestion to watch ad for reward
            _rewardedAdsButton.ShowAd();
            return;
        }


        Data.HelpCount--;
        UIManager.HelpButton.interactable = false;
        MakeLevel(restartLevel);
        LevelData _levelDesign = Resources.Load<LevelData>($"Levels/LevelData_{restartLevel}");


        foreach (var dot in dots)
            dot.Value.GetComponent<Image>().raycastTarget = false;

        StartCoroutine(localCoroutine());

        IEnumerator localCoroutine()
        {
            foreach (var item in _levelDesign.wayData)
            {
                yield return new WaitForSeconds(.1f);
                dots[item].CheckOnEnter();
            }
        }
    }
}