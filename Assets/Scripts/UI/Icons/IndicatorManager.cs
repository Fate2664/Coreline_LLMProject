using System;
using DG.Tweening;
using Nova;
using UnityEngine;

public class IndicatorManager : MonoBehaviour
{
    [SerializeField] private float scaleDuration = 0.5f;
    [SerializeField] private float height = 1.0f;
    public Sprite icon;

    private SpriteRenderer renderer;
    private Vector3 startScale;

    private void Awake()
    {
        startScale = transform.localScale;
        transform.localScale = Vector3.zero;
        renderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        transform.localScale = Vector3.zero;
        transform.DOLocalMoveY(height, 1f).SetLoops(-1,  LoopType.Yoyo).SetEase(Ease.InOutQuad);
    }

    public void ShowIndictor()
    {
        if (icon != null)
        {
            renderer.sprite = icon;
        }
        transform.DOScale(startScale, scaleDuration).SetEase(Ease.OutCubic);
    }

    public void HideIndictor()
    {
        transform.DOScale(Vector3.zero, scaleDuration).SetEase(Ease.OutCubic);
    }

}
