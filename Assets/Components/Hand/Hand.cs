using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;


public class Hand : MonoBehaviour
{
    [SerializeField] GameObject _trailRenderer;
    private float _goToPositionX;
    private Color _color;
    private Dot _dot;

    public Hand Init(float x, Color color, Dot dot)
    {
        _dot = dot;
        _goToPositionX = x;
        _color = color;
        GetComponent<Image>().color = color;
        _trailRenderer.GetComponent<TrailRenderer>().startColor = new Color(color.r, color.g, color.b, .5f);
        return this;
    }

    private void Start()
    {
        Vector2 startPos = transform.position;

        StartCoroutine(localCoroutine());

        IEnumerator localCoroutine()
        {
            while (true)
            {
                GetComponent<Image>().color = _color;
                transform.DOMove(new Vector3(_goToPositionX * 1.25f, transform.position.y), 3)
                    .OnComplete(() =>
                    {
                        GetComponent<Image>().DOColor(new Color((byte) _color.r, _color.g, _color.b, 0), 0.5f);
                        if (_dot.IsActive)
                            gameObject.SetActive(false);
                    });
                yield return new WaitForSeconds(4);
                _trailRenderer.SetActive(false);
                transform.position = startPos;
                _trailRenderer.SetActive(true);
            }
        }
    }
}