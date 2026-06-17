using System;
using DG.Tweening;
using Nova;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Platformer
{
    public class MenuManager : MonoBehaviour
    {
        [SerializeField] private UIBlock2D settingsUI;
        
        
        public void LoadBeginnerLevel()
        {
            SceneManager.LoadScene("BeginnerLevel");
        }

        public void LoadMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        public void ShowSettings()
        {
            if (settingsUI == null) return;
            
            settingsUI.transform.DOKill();
            settingsUI.transform.DOScale(1f, .5f).SetEase(Ease.OutBack);
        }

        public void HideSettings()
        {
            if (settingsUI == null) return;
            
            settingsUI.transform.DOKill();
            settingsUI.transform.DOScale(0f, .3f).SetEase(Ease.OutQuad);
        }
    }
}
