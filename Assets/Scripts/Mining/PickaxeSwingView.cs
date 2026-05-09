using DG.Tweening;
using UnityEngine;

public class PickaxeSwingView : MonoBehaviour
{
    [Header("Swing")]
    [SerializeField] private Vector3 swingRotation = new(55f, -18f, 14f);
    [SerializeField] private Vector3 swingPositionOffset = new(0.04f, -0.03f, 0.06f);
    [SerializeField] private Ease swingEase = Ease.OutQuad;
    [SerializeField] private Ease returnEase = Ease.InOutSine;

    private Vector3 restLocalPosition;
    private Quaternion restLocalRotation;
    private Tween activeTween;

    private void Awake()
    {
        restLocalPosition = transform.localPosition;
        restLocalRotation = transform.localRotation;
    }

    private void OnDestroy()
    {
        activeTween?.Kill();
    }

    public void PlaySwing(float duration)
    {
        activeTween?.Kill();

        float downDuration = duration * 0.4f;
        float returnDuration = duration - downDuration;

        Vector3 targetPosition = restLocalPosition + swingPositionOffset;
        Quaternion targetRotation = restLocalRotation * Quaternion.Euler(swingRotation);

        Sequence sequence = DOTween.Sequence();
        sequence.Append(transform.DOLocalMove(targetPosition, downDuration).SetEase(swingEase));
        sequence.Join(transform.DOLocalRotateQuaternion(targetRotation, downDuration).SetEase(swingEase));
        sequence.Append(transform.DOLocalMove(restLocalPosition, returnDuration).SetEase(returnEase));
        sequence.Join(transform.DOLocalRotateQuaternion(restLocalRotation, returnDuration).SetEase(returnEase));
        sequence.SetLink(gameObject);

        activeTween = sequence;
    }

    public void ReturnToRest(float duration = 0.08f)
    {
        activeTween?.Kill();

        Sequence sequence = DOTween.Sequence();
        sequence.Append(transform.DOLocalMove(restLocalPosition, duration).SetEase(returnEase));
        sequence.Join(transform.DOLocalRotateQuaternion(restLocalRotation, duration).SetEase(returnEase));
        sequence.SetLink(gameObject);

        activeTween = sequence;
    }
}
