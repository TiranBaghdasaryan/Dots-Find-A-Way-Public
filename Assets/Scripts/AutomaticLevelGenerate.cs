using UnityEditor;
using UnityEngine;


public class AutomaticLevelGenerate : MonoBehaviour
{
    [SerializeField] private int x;
    [SerializeField] private int y;

    [SerializeField] private int wallCount;

    [SerializeField] private int levelsCountWeWantToCreate;

    /// <summary>
    /// when you will build app, comment this method 
    /// </summary>
    /// 
#if UNITY_EDITOR
    [ContextMenu("GenerateLevel")]
    public void GenerateLevel()
    {
        for (int i = 0; i < levelsCountWeWantToCreate; i++)
        {
            LevelData asset = new LevelData();
            int levelNumber = asset.GenerateRandomLevel(x, y, wallCount) + 1;
            AssetDatabase.CreateAsset(asset, $"Assets/Resources/Levels/LevelData_{levelNumber}.asset");
        }
    }
#endif
}