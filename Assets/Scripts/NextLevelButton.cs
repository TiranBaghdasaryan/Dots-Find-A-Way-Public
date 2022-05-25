using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class NextLevelButton : MonoBehaviour
{
    private RectTransform _rectTransform;

    public static void OnWinAnimation()
    {
        Instance.gameObject.SetActive(true);
        float y = Instance.transform.position.y + 2.5f;
        Instance.transform.DOMove(new Vector3(Instance.transform.position.x, y), 1).OnComplete(() =>
        {
            Instance.gameObject.GetComponent<Image>().raycastTarget = true;
        });
    }


    public static void Reset()
    {
        Instance.gameObject.SetActive(false);
        Instance._rectTransform.anchoredPosition = new Vector2(Instance._rectTransform.anchoredPosition.x, -300);
    }

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        Instance = this;
    }

    public static NextLevelButton Instance;
}