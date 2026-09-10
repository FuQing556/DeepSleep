using DeepSleep.Runtime.Players.Identity;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Progression.Economy
{
    /// <summary>显示某一角色可独立消费的 Token 余额。</summary>
    public sealed class RoleTokenWalletHudView : MonoBehaviour
    {
        [SerializeField] private TokenWallet _wallet;
        [SerializeField] private PlayerRole _role;
        [SerializeField] private Text _label;

        private void Awake()
        {
            if (_wallet == null || _label == null)
            {
                Debug.LogError(
                    $"[{nameof(RoleTokenWalletHudView)}] 未配置钱包或文本。",
                    this);
                enabled = false;
                return;
            }
            Refresh();
        }

        private void OnEnable()
        {
            if (_wallet != null)
            {
                _wallet.Changed += Refresh;
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (_wallet != null)
            {
                _wallet.Changed -= Refresh;
            }
        }

        private void Refresh()
        {
            _label.text = $"TOKEN  {_wallet.GetBalance(_role)}";
        }
    }
}
