using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Progression.Economy
{
    public sealed class TokenWalletHudView : MonoBehaviour
    {
        [SerializeField] private TokenWallet _wallet;
        [SerializeField] private Text _label;

        private void Awake()
        {
            if (_wallet == null || _label == null)
            {
                Debug.LogError(
                    $"[{nameof(TokenWalletHudView)}] 未配置钱包或文本。",
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
            _label.text = $"本次战斗 TOKEN  +{_wallet.BattleEarned}";
        }
    }
}
