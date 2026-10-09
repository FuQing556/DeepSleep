# “2066”间歇超频

> 此页是历史记录。用户随后要求全战斗2倍/0.5倍及移动颠倒，已替换旧实现；当前规则、接线和验收见[SceneBattleEffects.md](SceneBattleEffects.md)。不要依据下方150%怪物专属方案继续开发。

本轮只接入已确认的移动超频：预警1秒、持续4秒、怪物移动150%。不改玩家、攻击冷却、HP、刷怪数、掉落、任务、碰撞或其他关卡，不增加新敌人、声音、网络消息。

## 节奏和调参

配置：`Assets/_Project/Configs/Progression/CFG_EnemyOverclock_World02_2066.asset`。

| 波次 | 首次预警（开战后） | 预警到下次预警 | 实际超频区间 |
|---|---:|---:|---|
| 1 | 禁用 | — | 无 |
| 2 | 12s | 22s | 13–17s、35–39s |
| 3 | 10s | 18s | 11–15s、29–33s、47–51s |
| 4 | 8s | 14s | 9–13s、23–27s、37–41s、51–55s |

间隔为本轮初始调参，体现用户批准的后续波次更频繁。Waves数组按波次顺序；PeriodSeconds包含预警和持续时间，必须大于两者总和。MovementMultiplier=1.5。WarningOpacity=.45、ActiveOpacity=.85、BreathAmount=.12、BreathSeconds=1.4、FadeOutSeconds=.25，均可在配置调。

## 接线和边界

`World02_2066/UI_World02_Overclock`：Canvas(ScreenSpaceOverlay、Order20)、CanvasScaler(1920×1080、MatchHeight)、CanvasGroup、ChapterEnemyOverclock2D、ChapterEnemyOverclockEdgeView；子物体Edge为九宫格Image。没有GraphicRaycaster，Image.raycastTarget=false，Group不拦截输入。

Canvas直接处在场景根，不放入SafeArea；Edge的Anchor=(0,0)–(1,1)，四边offset=0，随实际输出比例覆盖全部屏幕。HUD/触控原有安全区不改。素材路径及生图记录见 `ArtProduction/20261007_World02_Overclock/README.md`。

数据流：现有ChapterRunController的波次、阶段、权威倒计时 → 超频源 → 四个显式EnemyActorPool的MovementTempo → Actor/Motor；同一超频源 → EdgeView → CanvasGroup透明度。

只绑定本场景recursive-large/medium/small与quick-app四池，预热、扩容和分裂新生全部沿用绑定，不改共享Prefab。递归将蠕动相位推进加快，分离位移加快，但出生保护、受击回弹和攻击计时不变；快应用加快S曲线的弧长／相位推进，保持穿屏连续，感知速度同步变化。第一世界、测试场及无此注入的池使用倍率1。

只有Combat且普通关卡时参与；选角、节点、失败、结算、到时、禁用组件及图鉴挑战不参与。暂停冻结章节计时，零dt不移动。重试/下一波从章节实际时间重新推导，无残留协程或独立计时器。客机仅使用既有ChapterState消息重建边缘阶段，敌人移动仍由主机裁决；没有新增协议字段。客机表现可能随既有0.2s章节消息出现小幅时间阶梯，未做双端视觉验收。

协议11不变，内容版本`20261007-world02-overclock-1`，禁止新旧内容混房。

## 验证

编辑态入口：`DeepSleep.Editor.Diagnostics.World02OverclockChecks.RunAssets()`；Play入口RunPlay()，只在独立临时profile运行。覆盖四波预警/持续边界、显式池绑定、第一世界未启用、九宫格/触控穿透、真实快应用位移与感知速度、三种递归实际步距、池回收再租、暂停、节点隐藏、Canvas实际四角。截图辅助CaptureGameView(path)仅用于编辑器输出，处理图形后端RenderTexture方向，不修改素材。

资产142项、隔离Play37项通过。第二世界装配2166项0错误、2条预期警告（已关闭豆包／Kimi没有任务）；第一世界1923项0错误0警告。递归61项、快应用19项现有资产回归通过。真实存档哈希不变。未自然四波难度、手机实机或真实双端验收，未打包上传。

已检查含Overlay UI的GameView截图：`docs/Screenshots/20261007_World02_Overclock_PC_UI.png`（1600×900）、`docs/Screenshots/20261007_World02_Overclock_Ultrawide_UI.png`（2400×1080）。超长屏实际Canvas为2400×1080，Edge四角逐项吻合；中心透明且HUD原位。截图冻结在超频阶段，仅证明编辑器该输出比例的视觉与布局，不代表手机触控或联机同步验收。测试后退出Play并恢复原GameView选项、移除临时分辨率。

人工：从第二世界第二波开始留意预警与速度变化；暂停期间边缘应静止，进节点后消失；宽屏最外边仍显示边框、中央不遮战斗。可先调配置透明度／周期，勿修改玩家全局TimeScale或HUD安全区来实现此机制。
