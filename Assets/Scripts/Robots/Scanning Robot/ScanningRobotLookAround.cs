using System.Collections;
using UnityEngine;

namespace Coreline.Robots
{
    public class ScanningRobotLookAround : MonoBehaviour
    {
        [SerializeField] private Vector2 yawRange = new(-120f, 120f);
        [SerializeField] private Vector2 pauseDurationRange = new(0.4f, 1.2f);
        [SerializeField] private Vector2 rotationSpeedRange = new(45f, 120f);
        [SerializeField] private bool startFromCurrentYaw = true;

        private Coroutine lookRoutine;
        private float baseYaw;

        private Transform Visual;

        private void OnEnable()
        {
            Visual = transform;
            baseYaw = startFromCurrentYaw ? Visual.localEulerAngles.y : 0f;
            lookRoutine = StartCoroutine(LookAroundLoop());
        }

        private void OnDisable()
        {
            if (lookRoutine != null)
            {
                StopCoroutine(lookRoutine);
                lookRoutine = null;
            }
        }

        private IEnumerator LookAroundLoop()
        {
            while (enabled)
            {
                float targetYaw = baseYaw + Random.Range(yawRange.x, yawRange.y);
                float speed = Random.Range(rotationSpeedRange.x, rotationSpeedRange.y);

                yield return RotateToYaw(targetYaw, Mathf.Max(1f, speed));
                yield return new WaitForSeconds(Random.Range(pauseDurationRange.x, pauseDurationRange.y));
            }
        }

        private IEnumerator RotateToYaw(float targetYaw, float speed)
        {
            Transform visual = Visual;

            while (enabled)
            {
                Vector3 currentEuler = visual.localEulerAngles;
                float nextYaw = Mathf.MoveTowardsAngle(currentEuler.y, targetYaw, speed * Time.deltaTime);
                visual.localEulerAngles = new Vector3(currentEuler.x, nextYaw, currentEuler.z);

                if (Mathf.Abs(Mathf.DeltaAngle(nextYaw, targetYaw)) <= 0.1f)
                {
                    yield break;
                }

                yield return null;
            }
        }
    }
}
