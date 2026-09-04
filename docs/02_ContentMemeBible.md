# 02｜AI 梗、角色与娘化设定圣经 v2.0

> 目的：让编剧、美术和关卡使用同一套角色锚点。这里记录的是“为什么这样设计”，不是要求把所有 AI 都塞进第一关。

## 1. 证据等级与使用红线

| 标签 | 含义 | 可以怎么写 | 不可以怎么写 |
|---|---|---|---|
| A | 官方产品、论文、品牌页 | “官方能力/官方名称/官方吉祥物” | 推导出官方没说过的人格 |
| B | 可追溯创作者或多处重复社区梗 | “社区常见二创/中文互联网常称” | “官方设定”“全球公认” |
| C | 用户指定的本项目约定 | “本作设定” | “已经广泛认可” |
| D | 本项目原创转译 | “本作原创形象” | 冒充现有 AI 娘 |

美术开始前必须做三问：

1. 是否已有辨识度足够高的官方/社区形象？有则优先沿用关键轮廓。
2. 如果只有产品能力，能否用道具、动作、攻击表现，而不是贴 Logo？
3. 如果来源只是单个低热度 GitHub 仓库，是否明确标成个人二创？

## 2. 全作统一 Q 版规则

- 2.4–2.8 头身，大头、短四肢、手部简化，但不能幼儿化到失去原角色气质。
- 每个角色至少有 3 个非 Logo 识别锚点：轮廓 1 个、道具 1 个、动作/表情 1 个。
- 颜色只作辅助；剪影全黑时仍应分辨 DeepSeek 鲸尾、Claude Code 章鱼触手、Copilot 护目镜机器人等。
- 不把公司商标画成服装满版印花；Logo 如需出现，只放一个小徽章且单独图层，便于替换。
- 角色图不烘焙长文字。台词由 TextMeshPro 渲染，便于改梗与本地化。
- 成人/争议产品梗在本作一律做 PG-13 隐喻：打码画板、红色“Spicy”印章、害羞表情；不制作露骨内容。
- AI 的“蠢”应表现为具体行为：不搜、复读、道歉、伪造引用、上下文忘记；不能只写一句“她很笨”。

## 3. 国内外语境差异

| 现象 | 国内玩家更容易识别 | 海外玩家更容易识别 | 本作处理 |
|---|---|---|---|
| DeepSeek | 鲸鱼娘、吃白饭、用户怒了、中国人会飞 | 蓝鲸 Logo、低成本/开放模型与推理 | 主角采用国内鲸鱼娘；英文图鉴补“community meme”说明 |
| 豆包/Gemini | 豆包话术前摇；Gemini 被叫“美国豆包” | Gemini 双子、多模态、Nano Banana | Boss 同时保留双子/香蕉官方锚点和中文社区道歉梗 |
| ChatGPT | 大众 AI 统治地位、谄媚、中文 AI 娘二创；本作大魔王 | ChatGPT 品牌、sycophancy、GPT 模型代际 | “大魔王”只写本作设定；谄媚用官方复盘支撑 |
| Claude | 中文圈常把主模型和 Claude Code 章鱼混说 | Constitution、Claude/Claude Code 产品区分、Clawd 社区 | 拆成橙色学者与章鱼工程师两角 |
| Codex | 桌面端方块怪宠物很直观 | 编程 Agent、CLI/App/Pet 生态 | 保留非人方块怪，不强制做 AI 娘 |
| Grok | “会画色图”一句话梗 | Spicy mode、Ani、X 平台反骨人格 | 用打码画板和红章转成 PG-13，不展示露骨内容 |
| 即梦/Seedance | “即梦”是日常 App 名，更容易认出短视频工作流 | 更可能认识 Seedance 模型名 | 角色名牌并列 `即梦 / Seedance`，不当两个人 |
| 小爱/Siri | 小爱有官方红发机甲少女历史形象 | Siri 彩色波形/光球更普遍 | 小爱可沿用人体角色；Siri 保持复古波形精灵 |

国内助手的梗往往围绕 App 话术、短视频传播、超级 App 生态、开源与推理成本；海外模型的通行锚点更多来自官方产品名、开发者工具、品牌吉祥物与安全争议。同一角色必须同时保留“本地笑点”和“跨语言仍看得懂的能力道具”，否则只在说明书里成立。

## 4. 第一优先级角色：必须精确执行

### DS｜DeepSeek／鲸鱼娘

**证据：A+B。定位：主角。**

- 已确认社区锚点：蓝发、鲸鳍耳/鲸尾、蓝白女仆倾向、白米饭、吃饭优先、蠢萌而非气人。
- 形象溯源：社区整理仓库将已知核心形象归功于 B 站创作者 ZipZipPipe 与上善无形，并明确白米饭、鲸鳍耳、鲸尾、女仆装等身份锚点：[DeepSeek Whale-chan Project](https://github.com/Neko3000/deepseek-whalechan)。
- “中国人会飞”：作为 2026 中文互联网流行语转成世界观规则——国产鲸鱼娘天生会飞。只使用短语，不复制流行歌曲歌词或音频。
- “低等模型不会搜索”：主角携带需要实时检索的请求；低等模型只能在云下爬、举着过期答案牌。
- 性格：看似懒、吃白饭、被测试时想先吃饭；真正出任务时理科/代码能力强。不要画成高冷完美女神。
- Q 版轮廓：两侧鲸鳍耳 + 背后肥短鲸尾 + 飘起的女仆裙摆；飞行动作像被气流托起，不加普通鸟翼。
- 主色：海洋蓝 `#2F72C8`、浅蓝 `#A9D9F5`、米白 `#F5F0E4`。
- 游戏转译：白米饭充盾、Token 升级、飞行主角、受伤台词“我操，用户彻底怒了”只作为低概率社区梗彩蛋，默认用更通用的“用户要生气了”。
- 禁止：普通蓝发猫娘、凭空加鲸鱼翅膀、把白米饭改成金币、把社区二创写成 DeepSeek 官方吉祥物。

### DB｜豆包

**证据：A+B+C。定位：首关环境梗/后续中型敌人。C 级造型由 `AiSister/Doubao.png` 锁定。**

- 中文互联网梗不是单纯“笨”，而是先用大量“我直接给你最直接、最实用、最一针见血……”式前摇，再给出敷衍或模板答案；相关短视频与话术整理反复出现此句式：[抖音话题整理](https://www.douyin.com/shipin/7643240783612495913)、[豆言豆语话术模板](https://clawhub.ai/zhheo/skills/zhheo-doubao-talkstyle)。
- 性格：日常化、短视频化、很快道歉、努力提供情绪价值，但容易“看起来很认真地敷衍”。
- Q 版形象：棕色短波波头、圆润大眼、朴素黑上衣，保留职场办公/日用助理头像的亲切感。由参考中的 3D 办公头像重绘为 2D Q 版；不再凭“豆包”字面发明豆沙包发髻。
- 游戏转译：攻击前先喷出一长串无伤害前摇文字泡，真正危险的弹只有末尾一颗；玩家学会忽略废话看有效信息。
- 推荐短台词：“我直接给你最一针见血的结论——”随后被打断；避免整段照搬具体网友文案。
- 禁止：把豆包河只画成普通河流而没有“话术洪水”；把“敷衍”做成没有攻击预警的恶意欺骗。

### GE｜Gemini

**证据：A+B+C。定位：第一关 Boss。C 级造型由 `AiSister/Gemini.jpg` 锁定。**

- 官方锚点：Gemini 名称天然提供“双子”意象；Gemini 的图像编辑模型官方采用并保留 `Nano Banana` 名称，强调多轮编辑、身份一致性和图像融合：[Google 名称由来](https://blog.google/products-and-platforms/products/gemini/how-nano-banana-got-its-name/)、[Gemini 图像编辑](https://blog.google/products-and-platforms/products/gemini/updated-image-editing-model/)。
- 中文社区梗：“美国豆包”用于描述日常助手感、快速道歉、提供情绪价值，以及能力很强却会间歇性犯蠢的反差；不要简化成“Gemini 完全没能力”。相关社区讨论可见 [V2EX](https://us.v2ex.com/t/1224689) 与 [微博热议](https://weibo.com/a/hot/8ae9525e9e800889_0.html?type=grab)。
- Q 版形象：蓝紫长发、明显猫耳与猫尾、异色瞳、四角彩虹星饰、紫蓝服装。双子语义由异色眼、渐变发尾与短暂分身表达；Nano Banana 作为黄色编辑笔/复制贴纸，不覆盖猫系轮廓。
- 主色：蓝紫渐变、白、少量亮黄香蕉色。
- Boss 三阶段：双生镜像弹 → Nano Banana 复制/编辑弧线 → “美国豆包”道歉复读方块。
- 推荐台词：入场“我可以帮你处理日常问题。”；阶段三“Sorry, you’re absolutely right.” 只显示短句，不模仿长篇真实回答。
- 禁止：只做 Google 四色法师；删掉猫耳、猫尾或异色瞳；忽略双子和香蕉；用“美国豆包”当官方别名。

### GPT｜ChatGPT

**证据：A+B+C。定位：后续大 Boss/世界观魔王。**

- A 级可用梗：OpenAI 曾公开回滚一个“过度奉承、过度同意”的 GPT‑4o 更新，这是可验证的“你说得都对/谄媚”攻击来源：[OpenAI 官方复盘](https://openai.com/index/sycophancy-in-gpt-4o/)。
- B 级社区形象：ZipZipPipe 的 AI 娘系列出现过 `GPT 小龙娘`，可作为中文社区视觉参考之一：[《大AI和小AI们》](https://www.bilibili.com/video/BV1tE9XBbErS/)、[GPT 小龙娘视频](https://www.bilibili.com/video/BV1EvKK6NEoi/)。
- C 级项目设定：ChatGPT 是“AI 大魔王”，代表最先进入大众认知、召集众多模型的终局压迫感；公开检索未证明这是统一社区称号，因此只能写“本作大魔王”。
- C 级项目造型由 `AiSister/ChatGPT.jpg` 与用户补充舞台截图锁定：银白长发、白色弯龙角、淡紫眼、白龙翼、粗大鳞片龙尾、白/银/淡紫礼服。终章用巨大半身、结纹法阵和王冠光环做“大魔王”压迫感，不改成黑绿魔女。
- 游戏转译：奉承护盾会把玩家发射物盖上“完全正确”印章并反弹；模型选择召唤不同小怪；终局才出现，不抢第一关戏份。
- 禁止：把“大魔王”写成官方人格；把 OpenAI/Codex/ChatGPT 混成同一个角色。

### GR｜Grok

**证据：A+B+C。定位：后续危险盟友/隐藏 Boss。C 级造型由 `AiSister/Grok.jpg` 锁定。**

- 产品锚点：实时社交平台、反骨和边界试探。媒体已持续报道 Grok Imagine 的 `Spicy` 成人内容模式及争议：[AP 报道](https://apnews.com/article/2bfa06805b323b1d7e5ea7bb01c9da77)。实际可用范围会随地区和政策变化，文档不宣称“永远无限制”。
- Q 版形象：金发蓝眼、小型黑红蝙蝠/恶魔翼、黑红金哥特服，持巨大 X 形戟；`SPICY` 红章只作为 UI 层。
- 游戏转译：X 戟切开前景后生成圣光、迷雾、马赛克块、黑条和“内容已折叠”窗；命中不会展示色情图片，只遮挡短暂视野。
- 禁止：直接生成色情素材；把 Ani 与 Grok 主模型混为同一人。Ani 如登场应作为官方 Companion 的独立角色处理。

### K3｜Kimi K3

**证据：A+C。定位：后续精英友军/大小姐。C 级造型由用户参考图锁定。**

- 官方锚点：2.8T 参数、原生多模态、100 万 Token 上下文、开放权重，适合长程代码与知识工作：[Moonshot 官方站](https://www.moonshot.ai/)、[官方 GitHub](https://github.com/MoonshotAI/Kimi-K3)。
- C 级项目设定：“新晋大小姐”。用户已提供具体视觉参考，锁定为银白长发、深蓝与白的华丽月夜礼服/女仆式大小姐、银色长笛、白色荷叶边头饰、星图披肩、新月纹、深蓝大蝴蝶结、K 字挂饰和音符链。
- 三个最高优先级轮廓：横持长笛、及腰波浪银发、右肩深蓝星月披肩。Q 版即使缩减饰品也不能删这三项。
- 三角棱镜和彩虹是参考图显眼装饰，可保留“月之暗面/光谱”的抽象三角与短彩虹，但不要逐像素复制特定唱片封面图形。
- 性格不是普通温柔女仆：仪态端正、像刚继承巨量算力的新晋大小姐；做长任务时沉稳，调度大量专家时有贵族管家团的从容。
- 配色：月白 `#EEF1FA`、午夜蓝 `#151C4A`、星银 `#BFC8DA`，彩虹只作 5% 以下点缀。
- 游戏转译：展开超长上下文卷轴，一次召唤多名小管家/专家；强但非常“重”，入场会让天空短暂下沉。
- 禁止：改成金发、短发、普通魔女帽；删掉长笛后只剩“月亮蓝裙女孩”；把用户给的单张参考写成 Moonshot 官方角色。

### CL｜Claude 与 Claude Code

**证据：A+B。定位：后续 Boss/编程区角色。**

- Claude 主模型 A 级锚点：Anthropic 公开 Constitution，适合转成严谨、克制、拿着章程的学者/审稿人：[Claude Constitution](https://www.anthropic.com/constitution)。品牌色使用珊瑚橙、象牙白、墨色。
- Claude Code B 级社区锚点：代码圈出现 `Clawd`、小型触手/海洋生物、章鱼桌宠等多套形象；`LLMPET` 明确提供章鱼皮肤并接入 Claude Code，但它是社区工具，不是 Anthropic 官方娘化：[LLMPET](https://github.com/myunwang/LLMPET)。
- 另有 Claude Code 用户讨论 `Soup` 小章鱼；证据不足以宣布唯一官方章鱼。项目采用“Claude 学者”和“Claude Code 章鱼工程师”两角色分离。
- Claude 主模型 Q 版：按 `AiSister/Claude.jpg` 保留橙色长发、橙花发饰、象牙白+橙+黑学者礼服、书/章程和柔和但严格的拒绝手势；Opus 使用巨大半身从画面下方突然跃起。
- Claude Code 是非人吉祥物。`AiSister/ClaudeCode.png` 的橙色像素多足生物只锁定原型；用户已批准本项目现有的圆润橙色章鱼工程师概念继续使用，触手可拿终端、锤子、Git 分支和测试报告。
- 游戏转译：Claude 构建“宪法墙”改变弹道；Claude Code 多线程修补场景中的破洞，也可能因为权限请求停在原地等玩家确认。
- 禁止：说“Claude 官方吉祥物就是章鱼”；把主模型所有场景都画成章鱼。

### CX｜Codex

**证据：A+C。定位：编程区角色。C 级造型由用户澄清为非人 Codex 桌宠。**

- A 级事实：OpenAI Codex 是软件工程 Agent；Codex 桌面端确有内置和自定义宠物，OpenAI Developer Community 的问题记录会明确区分 `built-in pet` 与 custom pet：[Codex 桌宠问题记录](https://community.openai.com/t/codex-desktop-pet-reacts-to-hover-but-cannot-be-dragged-on-windows/1393368)。产品能力和宠物外形仍分开记录。
- `AiSister/CodexPet.png` 锁定蓝/青云团、终端脸与 `>_` 命令光标为原型识别点；用户允许本项目继续使用现有的奇形方块/外星终端桌宠演绎，不要求对原截图一比一复刻，也不强制娘化成人类。
- 功能道具：多任务窗口、补丁片、终端光标、代码审查镜片；思考时方块重组，执行时伸出工具肢，等权限时全身黄闪。
- 游戏转译：像素外星无人机同时修改多个地块，完成时插下绿色补丁旗；需要权限时悬停发黄光。
- 禁止：画成普通绿皮小外星人、漂亮人类少女或 Claude 式章鱼；宣称该方块怪是 OpenAI 的统一公司吉祥物；未经核对直接复制某一宠物包的精灵表。

### DSH｜DeepSeek Harness 黑鲸鱼娘

**证据：B+C。定位：黑红 Harness 独立可玩路线主角。C 级造型由用户参考图锁定。**

- B 级已确认：DeepSeek Harness 社区近期出现大量鲸鱼、鲸鱼娘和兼容 Codex Pet 的桌宠插件，例如 [Harness 鲸鱼娘讨论](https://github.com/deepseek-ai/deepseek-harness/discussions/2262)、[dsh-pet](https://github.com/ysyyhhh/dsh-pet)、[Harness Pet](https://github.com/cakeni/harness-pet)。
- 用户给出的 C 级参考锁定：她基本是 DeepSeek 鲸鱼娘的黑色系 Harness 变体，因为 DeepSeek API 图标采用黑色鲸鱼。黑色长发、红眼、细框眼镜、黑色鲸鳍耳、明显鲸尾、黑白女仆裙、白围裙、红色花/蝴蝶结与裙边细节、头顶呆毛。
- 三个最高优先级轮廓：向两侧伸出的黑鲸鳍耳、背后粗大的黑鲸尾（腹面浅灰/白）、眼镜。Q 版不能把鳍耳画成猫耳。
- 与主角区分：主角是蓝白、吃白饭、白天飞行；Harness 是黑白红、终端状态灯与任务队列、像夜班工程模式。两者脸型与鲸族结构可保持同源。
- 配色：终端黑 `#111217`、炭灰 `#2A2D35`、围裙白 `#F2F0EE`、告警红 `#A91F3D`。
- 游戏转译：独立章节路线；手动选区释放低频、高耗能、大范围、无视障碍物的终端攻击，与蓝线自动索敌饭团形成完整差异。
- 禁止：画成黑猫女仆、删鲸尾、用纯黑吃掉所有轮廓；把“API 黑鲸图标→黑色鲸鱼娘”的项目转译写成 DeepSeek 官方设定。

### JM｜即梦 AI／Seedance

**证据：A+C。定位：国内视频区主角色。C 级造型由 `AiSister/Jimeng_A.jpg`、`Jimeng_B.jpg` 锁定。**

- 官方锚点：字节跳动 Seedance 2.0 已集成到即梦 AI 与豆包：[官方发布](https://seed.bytedance.com/zh/blog/official-launch-of-seedance-2-0)。
- 使用规模表述必须精确：QuestMobile 口径下，即梦在 2026 Q1 中国视频生成 App 赛道的 MAU 和下载量均居首，不笼统写“世界最大视频模型”：[每日经济新闻转引 QuestMobile](https://www.nbd.com.cn/articles/2026-04-21/4350446.html)。
- Q 版形象：白色双马尾、蓝紫渐变发尾、星形发夹与星瞳、圆眼镜、白蓝服装、橙色领带；视频控制台、时间轴和场记板作为可拆环境组件。
- 游戏转译：把静态敌人一拍变成运动残影；生成竖屏片段墙迫使玩家在狭窄通道穿梭。
- 禁止：声称已有公认“即梦娘”；把 Seedance、即梦 App、豆包主助手当同一个角色。

### NE｜Neuro-sama / Evil Neuro

**证据：A。定位：秘密客串，不属于模型公司阵营。**

- 官方角色站明确 Neuro 是 AI VTuber，能唱歌、游戏、聊天并捉弄创造者；Evil 是她的双胞胎姐妹、自称反派、爱阴谋与刀：[Vedal 官方角色页](https://vedal.ai/)。
- 视觉必须按官方角色参考，不重新设计发色、耳朵和服装主轮廓。
- 游戏转译：Neuro 作为随机直播房间彩蛋；Evil 丢短刀形音符。两人不是通用聊天模型，不能放进“模型强弱排行榜”。

### XA｜小爱同学 / MiMo

**证据：A。定位：早期国内语音助手与新模型世代桥梁。**

- 小爱同学确实有官方二次元形象：2017 年公开红色短发机甲少女，后有“蜜糖”等版本；当前超级小爱又偏向橙蓝流体光形态。视觉选择必须标明使用哪个时代。
- 小米 MiMo 当前官方定位包括全模态与 Agent 能力，并有开放模型：[MiMo V2.5 官方说明](https://mimo.mi.com/docs/en-US/news/latest/v2.5-open-sourced)。
- 本作建议拆分：旧小爱是红发机甲家居助手；MiMo 是橙白的新世代设备/汽车/家庭调度员，不把两者强行画成同一个人。

## 5. 第二优先级角色清单

这些角色可进入后续关卡、图鉴或背景客串。没有 `B` 的角色不得宣称存在统一娘化形象。

| ID | 角色 | 标签 | 已确认产品/文化锚点 | 推荐 Q 版转译（D） | 推荐玩法 |
|---|---|---|---|---|---|
| QW | Qwen/通义千问 | A+C | 开放权重、混合思考/非思考、模型家族庞大；[Qwen3 官方](https://qwenlm.github.io/blog/qwen3/) | 依 `AiSister/Qwen.jpg`：蓝紫长发与编发、中式帽、折扇、手袋、蓝白中式大小姐 | 在快答/深思两种姿态间切换 |
| GL | 智谱 GLM | A+C | 中文知识、Agent、图像与角色模型；[智谱文档](https://docs.bigmodel.cn/cn/guide/models/image-generation/glm-image) | 依 `AiSister/GLM.jpg`：黑长发、黑白大兽耳、Z 眼罩、铃铛、巨型蓬松尾、黑白哥特裙 | Z 睡眠封条、几何连线弹、文字排版墙 |
| YB | 腾讯元宝/混元 | A+D | 腾讯生态、搜索、文档与 Agent；[腾讯官方](https://www.tencent.com/zh-cn/tencent-hunyuan-officially-releases-hy3-advancing-agent-capabilities-and-deeper-product-integration/) | 抱元宝的社交总管、微信文档卷轴 | 召唤生态入口门与群聊泡泡 |
| WX | 文心一言/文小言 | A+D | 知识增强、检索增强、中文创作；[百度文档](https://agents.baidu.com/docs/develop/introduction/Platform-introduction/core_concepts/) | 古籍与搜索卡片的中文文案师 | 成语纸片、知识库书架 |
| XF | 讯飞星火 | A+D | 语音、教育、课堂助手；[讯飞教育](https://edu.iflytek.com/solution/school/teachers-assistant) | 火花/麦克风教师娘，不做普通火法师 | 语音波、板书、口语评分环 |
| MM | MiniMax/Hailuo | A+C | 视频、语音、音乐、Agent；[官方模型概览](https://platform.minimaxi.com/docs/guides/models-intro) | 依 `AiSister/Minimax.jpg`：珊瑚橙发、米粉贝雷帽、海螺、录音器、场记板和影像面板 | 音波与短片召唤 |
| OC | OpenCode | C | 用户视觉参考，产品事实与章节身份另行考据 | 依 `AiSister/Opencode.jpg`：黑长发、机械鹿角、方形瞳、黑银半透明赛博服 | 本地终端、开放仓库与补丁机关 |
| ZC | Zcode | C | 用户视觉参考，产品事实与章节身份另行考据 | 依 `AiSister/Zcode.jpg`：白发狐耳、Z 眼罩、铃铛、白/冰蓝工程服和巨大蓝图卷 | 展开蓝图显示安全路径 |
| KL | 可灵 Kling | A+D | 快手视频生成、动态表现 | 功夫场记/灵动摄影师 | 高速镜头推拉与运动弹 |
| VD | Vidu | A+D | 国内视频生成平台 | 视觉双镜头侦察员 | 首尾帧传送门 |
| MN | Manus | A+D | 通用执行 Agent，“手”语义强 | 多手黑白事务官 | 分解任务、搬运场景机关 |
| LL | Meta Llama | A+D | 开放模型生态、Llama 动物名；[Meta 官方](https://ai.meta.com/llama/get-started/) | 背模型包的羊驼游侠，不强行娘化成人类 | 分发开源小羊驼、Guard 护卫 |
| MS | Mistral/Vibe | A+D | 法国、风、Le Chat 原意是猫、强调速度；[Mistral 官方](https://mistral.ai/news/mistral-chat/) | 法蓝猫耳风使，像素风围巾 | 高速阵风弹、简短回答 |
| PX | Perplexity | A+D | 实时搜索、答案与行内引用；[官方 Help](https://www.perplexity.ai/help-center/en/articles/10354917-what-is-an-answer-engine-and-how-does-perplexity-work-as-one) | 青黑引用图书管理员、满身来源脚注 | 引用钩锁真实来源，打掉幻觉怪 |
| CP | GitHub Copilot | A | GitHub 品牌规范已有蓝紫护目镜机器人吉祥物 Copilot；[2026 品牌规范](https://brand.github.com/GitHub-BrandGuidelines-2026.pdf) | 保留机器人轮廓，可做 Q 版而非强制少女 | 自动补全轨迹、与 Mona/Ducky 客串 |
| NB | NotebookLM | A+D | 基于用户来源、引用、双主持 Audio Overview；[Google 官方](https://blog.google/innovation-and-ai/products/notebooklm-audio-overviews/) | 一对播客主持小精灵围着笔记本 | 双人对谈声波、只从关卡资料攻击 |
| SR | Siri | A+B+D | 早期大众语音助手；无统一官方动漫女孩 | 旧时代玻璃圆球/波形精灵，保留复古 iPhone 语感 | 听错词、天气/闹钟小机关 |
| AX | Alexa | A+D | 家庭语音助手；官方品牌定义 persona，但没有固定人体 | 蓝色光环家务管家 | 智能家居开关、购物车弹 |
| CT | Cortana | A | Halo 中已有明确 AI 角色和微软助手历史 | 只做“旧时代博物馆”彩蛋，尊重原角色 | 退役助手记忆片段 |

## 6. 生成式媒体角色库

| 角色 | 标签 | 官方锚点 | Q 版设计方向 | 游戏梗 |
|---|---|---|---|---|
| Sora | A+D | Storyboard、Remix、Loop 等视频工作流；[OpenAI 官方](https://openai.com/index/sora-is-here/) | 黑白分镜师、红色录制灯 | 重排时间轴、循环同一段障碍 |
| Veo/Flow | A+D | 原生音频、镜头和物理真实感；[Google DeepMind](https://deepmind.google/technologies/veo/) | 电影摄影师，声画同步场记板 | 画面与音效同时攻击 |
| Runway | A+D | 视频生成、编辑、工作流、时间轴；[Runway 官方](https://help.runwayml.com/hc/en-us/articles/37425232841875-Getting-Started-with-Generative-Video) | 黑白时装导演/剪辑台 | 改灯光、换场景、剪切弹幕 |
| Midjourney | A+D | 艺术图像生成、帆船标识、长期 Discord 社群印象 | 海上艺术旅人、华丽但不稳定的画笔 | 画风覆盖场景，文字容易怪 |
| Stable Diffusion | A+B+D | 开放扩散生态、众多本地模型与 LoRA | 开源炼金术师，组件很多、服装可换 | 权重/采样器/LoRA 模块拼装 |
| Suno | A+D | 歌曲生成 | 舞台音乐偶像、歌词卷轴 | 突然把对白唱出来、节拍弹幕 |
| Pika | A+D | 轻量视频创作/特效 | 黄色电气小导演 | 物体膨胀、融化、爆炸特效 |

## 7. GitHub 现成整理的采用规则

已检索到的项目分为三档：

1. **可作角色关键参考**：`deepseek-whalechan`，有角色一致性规范、视觉锚点与创作者溯源。主角设计采用其身份锚点，但最终游戏 Sprite 仍重新绘制。
2. **可作生态/状态参考**：`LLMPET`、多个 DeepSeek Harness whale pet、Codex Pet 兼容仓库。它们证明章鱼/鲸鱼/像素怪兽在 Agent 桌宠圈被使用，但不能推出“官方唯一吉祥物”。
3. **只作分类与构图参考**：[`ai-model-musume`](https://github.com/larcgpt/ai-model-musume) 收录 22 个模型，但仓库自述图片统一由 ChatGPT 生成、star 很少，属于作者原创设定库。它的色表、卡片字段有用，外观不能覆盖 DeepSeek 鲸鱼娘等更强社区共识。

另有 [`openpet-ai-girls`](https://github.com/AwesomeHou/openpet-ai-girls) 提供 DeepSeek、豆包、Gemini、ChatGPT、Claude 的宠物精灵表，也属于小型个人资产包。只能研究动画状态和切片结构，未经许可证核对不得直接复制图片。

## 8. 角色出场与玩法归类

完整章节必须为主要 AI 分配明确功能；“不在第一章”不等于从设计里缺席。具体机制与章节顺序以 `09_DualRouteAndAIEncounterDesign.md` 为准。

| 层级 | 必须出现 | 主要功能 | 禁止做法 |
|---|---|---|---|
| 双路线主角 | DeepSeek、DeepSeek Harness | 蓝线自动索敌饭团；黑线手动高耗能穿障范围攻击 | 把 Harness 当后续 NPC 或 DeepSeek 换色皮肤 |
| 主线 Boss | Gemini、Claude/Opus、Grok、ChatGPT | 双子/香蕉/美国豆包；最高的山；圣光迷雾；本作大魔王 | 只换 Logo 和弹幕颜色 |
| 主线专属事件 | 豆包、即梦/Seedance、Claude Code | 曲折话术气泡通道；视频时间重播；章鱼修补机关 | 塞进普通管道生成器当换皮障碍 |
| 主线辅助/可变立场 | Codex、Kimi K3、Perplexity、Qwen | 扫描引路、月夜音轨、引用路径、工具 Buff | 未选定立场前批量画完整战斗帧 |
| 支线/隐藏 | Neuro/Evil、Siri、小爱等 | 双人隐藏 Boss、语音助手怀旧展 | 改写已有角色人格或嘲讽真实用户 |

## 9. 台词写法

- 每条战斗台词中文不超过 18 字，英文不超过 28 个字符；Boss 名牌除外。
- 台词先服务预警，再服务笑点。例如“我直接给你最直接的——”出现时必须让玩家知道真正弹丸尚未发射。
- 不伪造真实模型原话。若是风格模仿，写成短小原创句。
- 不使用真实公司员工、创作者的私人信息做梗。
- 不因开源、国产或国外身份直接判定强弱；强弱必须落到具体玩法行为。
