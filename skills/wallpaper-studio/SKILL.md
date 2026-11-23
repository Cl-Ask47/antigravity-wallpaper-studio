---
name: wallpaper-studio
description: >-
  Open and manage the Wallpaper Studio sidecar panel for Steam Wallpaper Engine
  backdrops, auto-rotation, visual filters, and the minimalist context usage ring HUD.
  Use when the user asks to open, toggle, or adjust wallpaper or context ring settings.
---

# Wallpaper Studio

`wallpaper-studio/studio` runs as an auto-starting UI Sidecar whenever this plugin is enabled.

## Instructions
1. Offer the user the direct sidecar panel pill: `[壁纸与圆环](sidecar://wallpaper-studio/studio/)`.
2. If the user wants to enable, disable, or inspect the plugin bundle, provide `[Wallpaper Studio](customization://wallpaper-studio)`.
3. To verify live companion health if the user reports an issue, inspect `%TEMP%\agy_wallpaper_studio\companion_diag.json` and `%USERPROFILE%\.gemini\antigravity\sidecar_data\wallpaper-studio\studio\logs\sidecar.log`.
