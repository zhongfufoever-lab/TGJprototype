# 场景2汽车旅馆 UI 导入说明

本包是针对现有场景1 UI的素材与实施说明包，不是已经适配你项目的Unity场景/Prefab。纸张、按钮、面板等UI基础资源继续复用当前Unity项目。

1. 将本包中的 `Assets` 和 `Design` 两个文件夹复制到Unity项目根目录。根目录是同时含有 `Assets`、`Packages`、`ProjectSettings` 的目录。让Assets与已有Assets合并；不要把整个包再套进Assets里。
2. Unity打开当前项目，等待素材导入。Codex打开同一个项目根目录。
3. 将下方指令发给Codex：

> 请读取 `Design/Scene2Motel/CODEX_PROMPT.md`，参考其中两张效果图和原始素材，直接在当前项目中完成场景2汽车旅馆UI。复用已完成的场景1界面，保留全部UI几何布局，替换插画、六件道具与配色。完成后保存可运行的场景/Prefab，检查实际Game视图，并告诉我如何打开。

## 包内文件

- `Assets/ThatGameJam2026/Scene2Motel/Art/`：17张原始场景和道具PNG，未重新绘制或裁剪。
- `Design/Scene2Motel/scene2_ui_reference.png`：确认的场景2设计稿。
- `Design/Scene2Motel/scene1_ui_layout_reference.png`：场景1布局对照。
- `Design/Scene2Motel/CODEX_PROMPT.md`：完整实施要求，包括六格道具顺序。
- `Design/Scene2Motel/palette.json`：近似主题颜色。
- `Design/Scene2Motel/sprite_bounds.json`：透明有效范围和Unity Sprite Rect，帮助Codex正确显示道具大小。

Design放在Assets外，只供Codex读取；真正需要Unity导入的PNG放在Assets内。
当前项目中若已有第二章同一批美术素材，可让Codex识别并复用现有素材引用，不必再复制第二份。

## 官方参考

- Prefab Variants：https://docs.unity3d.com/6000.0/Documentation/Manual/PrefabVariants.html
- uGUI Image：https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-Image.html
- Canvas Scaler：https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html
