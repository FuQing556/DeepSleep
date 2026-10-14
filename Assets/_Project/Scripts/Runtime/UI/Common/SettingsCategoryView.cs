using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Common
{
    /// <summary>共用设置分组；分页不新增菜单入口，也不接管局内暂停规则。</summary>
    public sealed class SettingsCategoryView : MonoBehaviour
    {
        public Button AudioTab, DisplayTab, ControlsTab;
        public GameObject AudioPage, DisplayPage, ControlsPage;
        private void OnEnable()
        {
            AudioTab.onClick.AddListener(ShowAudio); DisplayTab.onClick.AddListener(ShowDisplay);
            ControlsTab.onClick.AddListener(ShowControls); ShowAudio();
        }
        private void OnDisable()
        {
            AudioTab.onClick.RemoveListener(ShowAudio); DisplayTab.onClick.RemoveListener(ShowDisplay);
            ControlsTab.onClick.RemoveListener(ShowControls);
        }
        public void ShowAudio() => Show(0);
        public void ShowDisplay() => Show(1);
        public void ShowControls() => Show(2);
        private void Show(int page)
        {
            AudioPage.SetActive(page == 0); DisplayPage.SetActive(page == 1); ControlsPage.SetActive(page == 2);
            AudioTab.interactable = page != 0; DisplayTab.interactable = page != 1; ControlsTab.interactable = page != 2;
        }
    }
}
