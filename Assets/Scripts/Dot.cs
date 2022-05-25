using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class Dot : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private GameObject _connection;
    [SerializeField] private GameObject _dotSkin;
    public GameObject DotSkin => _dotSkin;
    [SerializeField] private GameObject _effectObject;
    public GameObject EffectObject => _effectObject;
    public static Dot currentDot = null;
    private Dot lastDot = null;
    public Dot LastDot => lastDot;

    private Color32 color = default;


    private int i = 0;
    public int I => i;
    private int j = 0;
    public int J => j;

    private bool isActive = false;

    public bool IsActive
    {
        get { return isActive; }

        set
        {
            isActive = value;
            if (isActive)
                _dotSkin.GetComponent<Image>().color = color;
            else
                _dotSkin.GetComponent<Image>().color = new Color32(233, 233, 233, 255);
        }
    }

    private bool isCurrent = false;

    public bool IsCurrent
    {
        get => isCurrent;

        set
        {
            isCurrent = value;
            if (isCurrent)
            {
                lastDot = currentDot;
                currentDot = this;
                IsActive = true;
            }
            else
            {
                currentDot = lastDot;
                currentDot._connection.SetActive(false);
                IsActive = false;
            }
        }
    }

    private bool isLast = false;

    public bool IsLast
    {
        get { return isLast; }
        set { isLast = value; }
    }

    public Dot Init(int i, int j, Color32 color)
    {
        this.color = color;
        _connection.GetComponent<Image>().color = color;

        this.i = i;
        this.j = j;

        GameManager.dots.Add(new Vector2Int(j, i), this);

        return this;
    }

    public void CheckOnEnter()
    {
        if (currentDot.lastDot == this)
        {
            currentDot.lastDot.DoAnimation();
            currentDot.IsCurrent = false;
            return;
        }

        if (!isActive && this != lastDot && this != currentDot)
        {
            if (currentDot.I - 1 == i && currentDot.J == j)
            {
                IsCurrent = true;
                GameObject connection = lastDot._connection;
                connection.SetActive(true);
                //  connection.GetComponent<Image>().color =color;
                RectTransform rectTransform = connection.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = new Vector2(0, 75);
                rectTransform.eulerAngles = new Vector3(0, 0, 90);
                DoAnimation();
            }

            else if (currentDot.I + 1 == i && currentDot.J == j)
            {
                IsCurrent = true;
                GameObject connection = lastDot._connection;
                connection.SetActive(true);
                // connection.GetComponent<Image>().color = color;
                RectTransform rectTransform = connection.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = new Vector2(0, -75);
                rectTransform.eulerAngles = new Vector3(0, 0, 90);

                DoAnimation();
            }

            else if (currentDot.J - 1 == j && currentDot.I == i)
            {
                IsCurrent = true;
                GameObject connection = lastDot._connection;
                connection.SetActive(true);
               // connection.GetComponent<Image>().color = Color.cyan;
                RectTransform rectTransform = connection.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = new Vector2(-75, 0);
                rectTransform.eulerAngles = new Vector3(0, 0, 0);

                DoAnimation();
            }

            else if (currentDot.J + 1 == j && currentDot.I == i)
            {
                IsCurrent = true;
                GameObject connection = lastDot._connection;
                connection.SetActive(true);
              //  connection.GetComponent<Image>().color = Color.cyan;
                RectTransform rectTransform = connection.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = new Vector2(75, 0);
                rectTransform.eulerAngles = new Vector3(0, 0, 0);
                DoAnimation();
            }
            else return;

            //  Debug.Log($"{i}{j}");
            GameManager.CheckWin();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CheckOnEnter();
    }

    public void DoAnimation()
    {
        AudioManager.CallAudioClipPlop();
        _effectObject.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 140);
        _effectObject.GetComponent<Image>().color = _dotSkin.GetComponent<Image>().color;
        _effectObject.GetComponent<Image>().color = new Color(_effectObject.GetComponent<Image>().color.r,
            _effectObject.GetComponent<Image>().color.g, _effectObject.GetComponent<Image>().color.b, .3f);

        IEnumerator localCoroutine()
        {
            while (_effectObject.GetComponent<RectTransform>().sizeDelta.x != 100)
            {
                yield return new WaitForEndOfFrame();
                _effectObject.GetComponent<RectTransform>().sizeDelta = new Vector2(
                    _effectObject.GetComponent<RectTransform>().sizeDelta.x - 1,
                    _effectObject.GetComponent<RectTransform>().sizeDelta.y - 1);
            }
        }

        StartCoroutine(localCoroutine());
    }
}