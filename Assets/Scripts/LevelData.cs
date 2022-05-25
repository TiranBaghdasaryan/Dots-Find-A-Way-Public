using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "LevelData")]
public class LevelData : ScriptableObject
{
    [SerializeField] public Vector2Int size = new Vector2Int(4, 4);
    [SerializeField] public byte[] data = new byte[16];

    public Vector2Int[] wayData;


    /// <summary>
    /// when you will build app, comment this method 
    /// </summary>
    [ContextMenu("FindWay")]
     public void FindWay()
     {
         wayData = GetWayData();
         Debug();
        
     }

    public int GenerateRandomLevel(int parameterX, int parameterY, int wallsCount)
    {
        size = new Vector2Int(parameterX, parameterY);
        data = new byte[parameterX * parameterY];

        int xStartPos = UnityEngine.Random.Range(0, parameterX);
        int yStartPos = UnityEngine.Random.Range(0, parameterY);

        data[xStartPos * yStartPos + xStartPos] = 2;

        for (int i = 0; i < wallsCount; i++)
        {
            int x = UnityEngine.Random.Range(0, parameterX);
            int y = UnityEngine.Random.Range(0, parameterY);
            while (data[y * x + x] == 1 || data[y * x + x] == 2)
            {
                x = UnityEngine.Random.Range(0, parameterX);
                y = UnityEngine.Random.Range(0, parameterY);
            }


            data[y * x + x] = 1;
        }

        LevelData[] allTheData = GetAllTheDataOfLevels();

        foreach (var levelData in allTheData)
        {
            if (data == levelData.data)
                return GenerateRandomLevel(parameterX, parameterY, wallsCount);
        }

        if (GetWayData().Length == 0)
            GenerateRandomLevel(parameterX, parameterY, wallsCount);


        wayData = GetWayData();
#if UNITY_EDITOR

        EditorUtility.SetDirty(this);
        new SerializedObject(this).ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
#endif

        return GetAllTheDataOfLevels().Length;
    }


    public LevelData[] GetAllTheDataOfLevels()
    {
        int fCount = Directory.GetFiles(Application.dataPath + "/Resources/Levels", "*", SearchOption.AllDirectories)
            .Length / 2;

        LevelData[] data = new LevelData[fCount];

        for (int i = 1; i <= fCount; i++)
            data[i - 1] = Resources.Load<LevelData>($"Levels/LevelData_{i}");

        return data;
    }

    private void Debug()
    {
        foreach (var item in wayData)
            UnityEngine.Debug.Log($"1 - ({item.y},{item.x})");

        if (wayData.Length == 0)
        {
            UnityEngine.Debug.Log("can't find a way");
        }
    }


    public Vector2Int[] GetWayData()
    {
        bool[,] data = new bool[size.y, size.x];
        Vector2Int startPos = default;

        // data init
        for (int i = 0; i < size.y; i++)
        {
            for (int j = 0; j < size.x; j++)
            {
                switch (GetBlock(j, i))
                {
                    case BlockType.Empty:
                        data[i, j] = false;
                        break;
                    case BlockType.Wall:
                        data[i, j] = true;
                        break;
                    case BlockType.Start:
                        data[i, j] = true;
                        startPos = new Vector2Int(j, i);
                        break;
                }
            }
        }

        int q = 0;

        for (int k = 0; k < size.y; k++)
        for (int l = 0; l < size.x; l++)
            if (data[k, l].Equals(false))
                q++;

        Vector2Int[] foundWay;
        // this method returns the way  
        Vector2Int stepPos = Vector2Int.zero;

        Vector2Int[] FindWay(bool[,] parameterData, Vector2Int parameterStartPos, Vector2Int[] parameterWay,
            int parameterStep = 0)
        {
            Vector2Int[] newWay;


            int i = parameterStartPos.y;
            int j = parameterStartPos.x;

            newWay = parameterWay.ToArray();

            if (i - 1 > -1 && parameterData[i - 1, j].Equals(false))
            {
                parameterData[i - 1, j] = true;
                
                stepPos.x = j;
                stepPos.y = i - 1;
                
                newWay[parameterStep] = stepPos;
                if (CheckWay()) return newWay;
                foundWay = FindWay(parameterData, stepPos, newWay, parameterStep + 1);
                if (foundWay.Length > 0) return foundWay;
                parameterData[i - 1, j] = false;
            }

            if (i + 1 < size.y && parameterData[i + 1, j].Equals(false))
            {
                parameterData[i + 1, j] = true;


                stepPos.x = j;
                stepPos.y = i + 1;

                newWay[parameterStep] = stepPos;
                if (CheckWay()) return newWay;
                foundWay = FindWay(parameterData, stepPos, newWay, parameterStep + 1);
                if (foundWay.Length > 0) return foundWay;
                parameterData[i + 1, j] = false;
            }

            if (j - 1 > -1 && parameterData[i, j - 1].Equals(false))
            {
                parameterData[i, j - 1] = true;

                stepPos.x = j - 1;
                stepPos.y = i;

                newWay[parameterStep] = stepPos;
                if (CheckWay()) return newWay;
                foundWay = FindWay(parameterData, stepPos, newWay, parameterStep + 1);
                if (foundWay.Length > 0) return foundWay;
                parameterData[i, j - 1] = false;
            }

            if (j + 1 < size.x && parameterData[i, j + 1].Equals(false))
            {
                parameterData[i, j + 1] = true;

                stepPos.x = j + 1;
                stepPos.y = i;

                newWay[parameterStep] = stepPos;
                if (CheckWay()) return newWay;
                foundWay = FindWay(parameterData, stepPos, newWay, parameterStep + 1);
                if (foundWay.Length > 0) return foundWay;
                parameterData[i, j + 1] = false;
            }

            return new Vector2Int[] { };


            bool CheckWay()
            {
                for (int k = 0; k < size.y; k++)
                {
                    for (int l = 0; l < size.x; l++)
                    {
                        if (parameterData[k, l] == true)
                            continue;
                        else
                            return false;
                    }
                }

                return true;
            }
        }

        return FindWay(data, startPos, new Vector2Int[q]);
    }

    public BlockType GetBlock(int x, int y)
    {
        if (x < 0 || y < 0 || x >= size.x || y >= size.y) throw new IndexOutOfRangeException();
        return (BlockType) data[y * size.x + x];
    }


    // To Add new Block Add it here and set color for them in TextDataEditor.colors
    public enum BlockType : byte
    {
        Empty,
        Wall,
        Start
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private float data_scrollbar_x = 0f;
    private bool changed = false;

    private Color[] colors = new Color[]
    {
        Color.white,
        Color.black,
        Color.green
    };

    private void OnDisable()
    {
        if (changed)
        {
            EditorUtility.SetDirty(target);
            new SerializedObject(target).ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }

    public override void OnInspectorGUI()
    {
        var text_data = target as LevelData;
        if (Equals(text_data, null)) return;

        Vector2Int new_size = EditorGUILayout.Vector2IntField("Size", text_data.size);
        if (new_size.x < 0) new_size.x = 0;
        if (new_size.y < 0) new_size.y = 0;
        if (!text_data.size.Equals(new_size))
        {
            byte[] new_data = new byte[new_size.y * new_size.x];
            for (int y = Mathf.Min(new_size.y, text_data.size.y) - 1; y >= 0; y--)
            for (int x = Mathf.Min(new_size.x, text_data.size.x) - 1; x >= 0; x--)
                new_data[y * new_size.x + x] = text_data.data[y * text_data.size.x + x];
            text_data.size = new_size;
            text_data.data = new_data;
            changed = true;
        }

        GUILayout.BeginArea(new Rect(20 - data_scrollbar_x, 24, 20 * text_data.size.x, 20 * text_data.size.y));
        for (int y = 0; y < text_data.size.y; y++)
        {
            GUILayout.BeginHorizontal();
            for (int x = 0; x < text_data.size.x; x++)
            {
                int value = text_data.data[y * text_data.size.x + x];
                GUI.backgroundColor = colors[value];
                bool clicked = EditorGUILayout.Toggle(false, GUI.skin.button);
                if (clicked)
                {
                    value = ++value % colors.Length;
                    text_data.data[y * text_data.size.x + x] = (byte) value;
                    changed = true;
                }
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndArea();
        GUILayout.Space(20 * text_data.size.y + 4);

        if (EditorGUIUtility.currentViewWidth < 20 * text_data.size.x + 20)
        {
            data_scrollbar_x = GUILayout.HorizontalScrollbar(data_scrollbar_x, EditorGUIUtility.currentViewWidth, 0,
                20 * text_data.size.x + 40);
        }
    }
}
#endif
