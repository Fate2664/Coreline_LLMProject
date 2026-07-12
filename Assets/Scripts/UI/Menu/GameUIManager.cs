using DG.Tweening;
using Nova;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Platformer
{
    public class GameUIManager : MonoBehaviour
    {
        [SerializeField] private UIBlock2D pauseMenuUI;
        [SerializeField] private UIBlock2D pauseDimmer;
        
        public bool IsPaused { get; private set; }

        public void LoadMainLevel()
        {
            SceneManager.LoadScene("MainLevel");
        }

        public void LoadMainMenu()
        {
            Time.timeScale = 1f;
            IsPaused = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("MainMenu");
        }
        
        public void ShowPauseMenu()
        {
            if (pauseMenuUI == null) return;

            IsPaused = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            pauseDimmer.BodyEnabled = true;

            pauseMenuUI.transform.DOKill();
            pauseMenuUI.transform.DOScale(1f, .5f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public void HidePauseMenu()
        {
            if (pauseMenuUI == null) return;
            
            //You can still see the cursor after unpausing because pressing ESC in editor makes curor visible by default
            IsPaused = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            pauseDimmer.BodyEnabled = false;

            pauseMenuUI.transform.DOKill();
            pauseMenuUI.transform.DOScale(0f, .3f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void TogglePauseMenu()
        {
            if (IsPaused)
                HidePauseMenu();
            else
                ShowPauseMenu();
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}