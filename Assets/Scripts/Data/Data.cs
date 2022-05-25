using System;
using UnityEngine;

public class Data
{
    public static int HelpCount
    {
        get => _helpCount;
        set
        {
            _helpCount = value;
            PlayerPrefs.SetInt("helpCount",_helpCount);
            UIManager.ChangeHelpCountText(_helpCount.ToString());
            
        }
           
                
    }

    private static int _helpCount = 3;

    public static int CurrentLevel
    {
        get => _currentLevel;
        set
        {
            _currentLevel = value;
            PlayerPrefs.SetInt("level", _currentLevel);
            UIManager.ChangeLevelTextInMenu(_currentLevel.ToString());
        }
    }

    private static int _currentLevel = 350;


    public static bool IsSoundOn
    {
        get => _isSoundOn;
        set
        {
            _isSoundOn = value;
            PlayerPrefs.SetInt("sound", Convert.ToInt32(value));
            UIManager.ChangeSoundSprite(value);
        }
    }

    private static bool _isSoundOn = true;
}