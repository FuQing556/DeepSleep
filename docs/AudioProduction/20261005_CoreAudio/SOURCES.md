# 首版音频原料与许可

批次：2026-10-05。作者/发布者：Kenney（https://kenney.nl）。从官方页面取得，未使用游戏拆包、用户音频或来源不明的镜像。

| 原料包 | 官方页面 | 下载 ZIP | SHA-256 |
|---|---|---|---|
| Impact Sounds 1.0，130 条 | https://kenney.nl/assets/impact-sounds | https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip | `029d734af1582474edf3a694d1b0cebc97c1c152f2f39fa34d4c2bafc5de77f8` |
| Interface Sounds 1.0，100 条 | https://kenney.nl/assets/interface-sounds | https://kenney.nl/media/pages/assets/interface-sounds/fa43c1dd4d-1677589452/kenney_interface-sounds.zip | `f2193d072726d6758a5f7871b2dcc54dcce0d5c35c6f0a62f92549b327c81232` |

两个官方页面均标示 Creative Commons CC0。下载包内 `sources/impact/License.txt` 和 `sources/interface/License.txt` 原样保留，许可链接为 https://creativecommons.org/publicdomain/zero/1.0/ 。许可允许个人、教育及商业项目使用；署名非强制，仍保留 Kenney 来源记录。

下载 ZIP 和完整解包原料只在本生产批次的 `sources` 内留档。它们没有整包复制到 Unity Assets。`manifest.json` 对实际用到的每个原料记录原路径、SHA-256、采样率、声道、时长及裁切区间；每个成品另列所用原料、修改说明、输出哈希和测量值。

修改包括：去直流、裁切前后静段、单声道化、带宽限制、轻微改变采样播放速度、短包络、分层和有限颗粒拼接。短音主体来自这些声音采样，没有以正弦扫频或白噪声批量代替。没有额外加入长混响。

环境三条是明确的**合成近似候选**：带限周期风层，黄昏与休息再加入很轻的草地脚步原料颗粒。它们不是河流、城市或天台的实地录音，不能据此声称环境素材已经定稿，也没有添加虚构鸟叫/人声。

源文件为 44.1 kHz Vorbis。本批输出 44.1 kHz / 24-bit PCM WAV，24-bit 用于剪辑余量，不表示还原了源文件有损压缩前的细节。短音单声道，环境立体声。

制作依赖：Python、NumPy、SciPy、SoundFile 0.13.1。SoundFile 只安装到项目临时目录 `Temp/CoreAudioPython`，没有加入 Assets、修改全局 Python 或加入游戏运行依赖。复现时可以使用自己已有的同类环境。
