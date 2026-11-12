using UnityEngine;

namespace Coreline
{
    public class ScreenshotManager : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                ScreenCapture.CaptureScreenshot(
                    "Screenshot.png",
                    2
                );
            }
        }
    }
}