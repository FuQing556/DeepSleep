using DeepSleep.Runtime.Progression.Meta;
using UnityEngine;

namespace DeepSleep.Runtime.AppFlow
{
    /// <summary>跨场景服务的唯一根；场景内玩家、敌人与 UI 不允许挂在这里。</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameAppRoot : MonoBehaviour
    {
        public static GameAppRoot Instance { get; private set; }

        [SerializeField] private LocalPlayerProfileStore _profile;
        [SerializeField] private GameLaunchContext _launchContext;
        [SerializeField] private GameSceneRouter _sceneRouter;
        [SerializeField] private AchievementService _achievements;
        [SerializeField] private DeepSleep.Runtime.Presentation.Audio.GameAudioService _audio;

        public LocalPlayerProfileStore Profile => _profile;
        public GameLaunchContext LaunchContext => _launchContext;
        public GameSceneRouter SceneRouter => _sceneRouter;
        public AchievementService Achievements => _achievements;
        public DeepSleep.Runtime.Presentation.Audio.GameAudioService Audio => _audio;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
