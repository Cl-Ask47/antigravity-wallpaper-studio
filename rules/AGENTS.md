# Wallpaper Studio 自动启动与交互规则

`wallpaper-studio` 插件包含常驻 UI Sidecar（`wallpaper-studio/studio`，`restart_policy: "always"`），在 Antigravity 启动时会自动随宿主拉起并在后台自动启动 Windows 零侵入贴背壁纸与极简上下文圆环伴侣（遵循 `~/.gemini/antigravity/plugin_data/wallpaper-studio/config.json` 中保存的开关与壁纸配置）。

当用户提及壁纸、壁纸引擎、透明度、自动轮播、或上下文用量小圆环，或希望调整外观参数时：
1. 直接在回复中提供一键打开控制面板的链接：`[壁纸与圆环](sidecar://wallpaper-studio/studio/)`。
2. 如需启停、重载或管理该插件，提供插件管理页链接：`[Wallpaper Studio](customization://wallpaper-studio)`。
3. 严禁修改 Antigravity 宿主安装包、`app.asar`、启动快捷方式或 `~/.gemini/config/config.json`；所有壁纸与圆环配置只通过插件自身的 Sidecar API 或 `~/.gemini/antigravity/plugin_data/wallpaper-studio/config.json` 管理。
