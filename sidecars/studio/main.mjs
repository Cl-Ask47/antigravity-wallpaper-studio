import { existsSync, mkdirSync, readFileSync, unlinkSync, writeFileSync } from 'node:fs';
import { homedir, tmpdir } from 'node:os';
import { dirname, extname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { SidecarApp, Response } from 'sidecar_sdk';
import {
  getCompanionStatus,
  readActiveWindowInfo,
  sendCompanionLivePatch,
  stopCompanion,
  syncCompanion,
} from './companion_manager.mjs';
import { getContextTelemetry } from './context_tracker.mjs';
import { createMediaServer } from './media_server.mjs';
import { readDshWeHints, scanWallpaperEngineLibrary } from './we_scanner.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const DATA_DIR = join(homedir(), '.gemini', 'antigravity', 'plugin_data', 'wallpaper-studio');
const CONFIG_PATH = join(DATA_DIR, 'config.json');
const HUD_POS_PATH = join(tmpdir(), 'agy_wallpaper_studio', 'hud_offset.json');

mkdirSync(DATA_DIR, { recursive: true });

const DEFAULT_CONFIG = {
  backdropEnabled: true,
  hudEnabled: true,
  wallpaperId: '',
  windowTranslucency: 22,
  backdropBlur: 2,
  backdropDim: 22,
  brightness: 95,
  contrast: 105,
  saturate: 115,
  sceneFps: 30,
  videoMuted: true,
  videoVolume: 0,
  // Rotation settings
  rotationEnabled: false,
  rotationIntervalMin: 15,
  rotationOrder: 'random', // 'random' | 'sequential'
  rotationSource: 'dsh_fav', // 'all' | 'video' | 'scene' | 'web' | 'dsh_fav' | 'custom'
  rotationCustomIds: [],
  // Context & HUD settings
  contextLimit: 512000,
  pinnedConvId: '',
  hudSize: 40,
  hudAccent: '#F1717F',
  hudWarnColor: '#F59E0B',
  hudDangerColor: '#EF4444',
  hudShowPercent: true,
  hudFont: 'Segoe UI',
  // Panel own UI settings
  panelAccent: '#F1717F',
  panelFont: 'system',
  panelInputBg: '#161b26',
  panelSurfaceOpacity: 82,
  // Doc-preview integration
  docMaskEnabled: true,
  docMaskMode: 'auto', // 'auto' | 'dark' | 'light'
  hostBgColor: '#0f1419',
};

function clamp(n, min, max, fallback) {
  const v = Number(n);
  if (!Number.isFinite(v)) return fallback;
  return Math.max(min, Math.min(max, v));
}

function sanitizeHex(val, fallback) {
  if (typeof val !== 'string') return fallback;
  const s = val.trim();
  return /^#[0-9a-fA-F]{6}$/.test(s) ? s : fallback;
}

function sanitizeConfig(raw) {
  const src = raw && typeof raw === 'object' ? raw : {};
  const validSources = new Set(['all', 'video', 'scene', 'web', 'dsh_fav', 'custom']);
  return {
    backdropEnabled:
      typeof src.backdropEnabled === 'boolean'
        ? src.backdropEnabled
        : DEFAULT_CONFIG.backdropEnabled,
    hudEnabled:
      typeof src.hudEnabled === 'boolean' ? src.hudEnabled : DEFAULT_CONFIG.hudEnabled,
    wallpaperId: typeof src.wallpaperId === 'string' ? src.wallpaperId.trim() : '',
    windowTranslucency: clamp(src.windowTranslucency, 0, 55, DEFAULT_CONFIG.windowTranslucency),
    backdropBlur: clamp(src.backdropBlur, 0, 24, DEFAULT_CONFIG.backdropBlur),
    backdropDim: clamp(src.backdropDim, 0, 80, DEFAULT_CONFIG.backdropDim),
    brightness: clamp(src.brightness, 40, 160, DEFAULT_CONFIG.brightness),
    contrast: clamp(src.contrast, 50, 160, DEFAULT_CONFIG.contrast),
    saturate: clamp(src.saturate, 0, 200, DEFAULT_CONFIG.saturate),
    sceneFps: [15, 24, 30, 60].includes(Number(src.sceneFps))
      ? Number(src.sceneFps)
      : DEFAULT_CONFIG.sceneFps,
    videoMuted: typeof src.videoMuted === 'boolean' ? src.videoMuted : DEFAULT_CONFIG.videoMuted,
    videoVolume: clamp(src.videoVolume, 0, 100, DEFAULT_CONFIG.videoVolume),
    rotationEnabled:
      typeof src.rotationEnabled === 'boolean'
        ? src.rotationEnabled
        : DEFAULT_CONFIG.rotationEnabled,
    rotationIntervalMin: clamp(
      src.rotationIntervalMin,
      1,
      240,
      DEFAULT_CONFIG.rotationIntervalMin,
    ),
    rotationOrder: src.rotationOrder === 'sequential' ? 'sequential' : 'random',
    rotationSource: validSources.has(src.rotationSource)
      ? src.rotationSource
      : DEFAULT_CONFIG.rotationSource,
    rotationCustomIds: Array.isArray(src.rotationCustomIds)
      ? [...new Set(src.rotationCustomIds.map(String).filter(Boolean))]
      : DEFAULT_CONFIG.rotationCustomIds,
    contextLimit: [256000, 512000, 1000000].includes(Number(src.contextLimit))
      ? Number(src.contextLimit)
      : DEFAULT_CONFIG.contextLimit,
    pinnedConvId: typeof src.pinnedConvId === 'string' ? src.pinnedConvId.trim() : '',
    hudSize: clamp(src.hudSize, 32, 56, DEFAULT_CONFIG.hudSize),
    hudAccent: sanitizeHex(src.hudAccent, DEFAULT_CONFIG.hudAccent),
    hudWarnColor: sanitizeHex(src.hudWarnColor, DEFAULT_CONFIG.hudWarnColor),
    hudDangerColor: sanitizeHex(src.hudDangerColor, DEFAULT_CONFIG.hudDangerColor),
    hudShowPercent:
      typeof src.hudShowPercent === 'boolean' ? src.hudShowPercent : DEFAULT_CONFIG.hudShowPercent,
    hudFont:
      typeof src.hudFont === 'string' && src.hudFont.trim().length <= 64
        ? src.hudFont.trim()
        : DEFAULT_CONFIG.hudFont,
    panelAccent: sanitizeHex(src.panelAccent, DEFAULT_CONFIG.panelAccent),
    panelFont:
      typeof src.panelFont === 'string' && src.panelFont.trim().length <= 80
        ? src.panelFont.trim()
        : DEFAULT_CONFIG.panelFont,
    panelInputBg: sanitizeHex(src.panelInputBg, DEFAULT_CONFIG.panelInputBg),
    panelSurfaceOpacity: clamp(
      src.panelSurfaceOpacity,
      45,
      100,
      DEFAULT_CONFIG.panelSurfaceOpacity,
    ),
    docMaskEnabled:
      typeof src.docMaskEnabled === 'boolean'
        ? src.docMaskEnabled
        : DEFAULT_CONFIG.docMaskEnabled,
    docMaskMode: ['auto', 'dark', 'light'].includes(src.docMaskMode)
      ? src.docMaskMode
      : DEFAULT_CONFIG.docMaskMode,
    hostBgColor:
      typeof src.hostBgColor === 'string' && /^#[0-9a-fA-F]{6}$/.test(src.hostBgColor.trim())
        ? src.hostBgColor.trim()
        : DEFAULT_CONFIG.hostBgColor,
  };
}

function loadConfig() {
  const hints = readDshWeHints();
  if (existsSync(CONFIG_PATH)) {
    try {
      const loaded = sanitizeConfig(JSON.parse(readFileSync(CONFIG_PATH, 'utf8')));
      if (loaded.rotationCustomIds.length === 0 && hints.favoriteIds.length > 0) {
        loaded.rotationCustomIds = hints.favoriteIds.slice();
      }
      return loaded;
    } catch {
      // fall through
    }
  }
  const firstGroup = hints.rotationGroups?.[0];
  const initial = sanitizeConfig({
    ...DEFAULT_CONFIG,
    wallpaperId: hints.activeId || '',
    hudAccent: hints.accent || DEFAULT_CONFIG.hudAccent,
    panelAccent: hints.accent || DEFAULT_CONFIG.panelAccent,
    rotationEnabled: Boolean(hints.rotationEnabled),
    rotationIntervalMin: firstGroup?.interval || 30,
    rotationOrder: firstGroup?.order || 'random',
    rotationSource: hints.favoriteIds.length >= 2 ? 'dsh_fav' : 'all',
    rotationCustomIds: hints.favoriteIds.slice(),
  });
  saveConfig(initial);
  return initial;
}

function saveConfig(cfg) {
  mkdirSync(DATA_DIR, { recursive: true });
  writeFileSync(CONFIG_PATH, JSON.stringify(cfg, null, 2), 'utf8');
}

let config = loadConfig();
let libraryCache = null;
let lastRotationAt = Date.now();
let panelConvId = '';
let lastSeenWinTs = 0;
let lastPushedCtxSig = '';

async function ensureLibrary(force = false) {
  libraryCache = await scanWallpaperEngineLibrary({ force });
  if (!config.wallpaperId && libraryCache.items.length > 0) {
    config = sanitizeConfig({ ...config, wallpaperId: libraryCache.items[0].id });
    saveConfig(config);
  }
  return libraryCache;
}

function findActiveWallpaper() {
  if (!libraryCache || !libraryCache.items.length) return null;
  return libraryCache.items.find((it) => it.id === config.wallpaperId) || libraryCache.items[0];
}

function getRotationPool() {
  if (!libraryCache || !libraryCache.items.length) return [];
  const items = libraryCache.items.filter((it) => it.renderMode !== 'none');
  if (config.rotationSource === 'video') return items.filter((it) => it.renderMode === 'video');
  if (config.rotationSource === 'scene') return items.filter((it) => it.renderMode === 'scene');
  if (config.rotationSource === 'web') return items.filter((it) => it.renderMode === 'web');
  if (config.rotationSource === 'dsh_fav') {
    const favs = items.filter((it) => it.isDshFavorite || it.isDshActive);
    return favs.length >= 2 ? favs : items;
  }
  if (config.rotationSource === 'custom') {
    const set = new Set(config.rotationCustomIds);
    const picked = items.filter((it) => set.has(it.id));
    return picked.length >= 2 ? picked : items;
  }
  return items;
}

function pickNextWallpaper() {
  const pool = getRotationPool();
  if (pool.length === 0) return null;
  if (pool.length === 1) return pool[0];

  const curIdx = pool.findIndex((it) => it.id === config.wallpaperId);
  if (config.rotationOrder === 'sequential') {
    return pool[(curIdx + 1) % pool.length];
  }
  const candidates = pool.filter((it) => it.id !== config.wallpaperId);
  return candidates[Math.floor(Math.random() * candidates.length)] || pool[0];
}

function getRotationStatus() {
  const pool = getRotationPool();
  const intervalMs = config.rotationIntervalMin * 60 * 1000;
  const nextAt = config.rotationEnabled ? lastRotationAt + intervalMs : 0;
  const remainingSec = config.rotationEnabled
    ? Math.max(0, Math.round((nextAt - Date.now()) / 1000))
    : 0;
  return {
    enabled: config.rotationEnabled,
    poolCount: pool.length,
    intervalMin: config.rotationIntervalMin,
    order: config.rotationOrder,
    source: config.rotationSource,
    remainingSec,
    nextAt,
  };
}

function getStageState() {
  const wp = findActiveWallpaper();
  return {
    backdropEnabled: config.backdropEnabled && Boolean(wp),
    wallpaperId: wp?.id || '',
    title: wp?.title || '',
    renderMode: wp?.renderMode || 'none',
    entryFile: wp?.entryFile || '',
    hasPreview: Boolean(wp?.previewPath),
    backdropBlur: config.backdropBlur,
    backdropDim: config.backdropDim,
    brightness: config.brightness,
    contrast: config.contrast,
    saturate: config.saturate,
    sceneFps: config.sceneFps,
    videoMuted: config.videoMuted,
    videoVolume: config.videoVolume,
    weAssetsAvailable: Boolean(libraryCache?.weAssetsDir),
  };
}

const mediaServer = createMediaServer({
  getLibrary: () => ensureLibrary(false),
  getStageState,
});

function resolveDocMaskColor() {
  if (config.docMaskMode === 'light') return '#f8fafc';
  if (config.docMaskMode === 'dark') return '#0f1419';
  return config.hostBgColor || '#0f1419';
}

function buildCompanionPayload(telemetry) {
  const wp = findActiveWallpaper();
  const port = mediaServer.getPort();
  return {
    shutdown: false,
    backdropEnabled: config.backdropEnabled && Boolean(wp) && port > 0,
    hudEnabled: config.hudEnabled,
    docMaskEnabled: config.docMaskEnabled,
    docMaskColor: resolveDocMaskColor(),
    stageUrl: port > 0 ? `http://127.0.0.1:${port}/stage?localAssets=1` : '',
    previewPath: wp?.previewPath || '',
    mediaPath: wp?.effectiveBackdropPath || '',
    mediaKind: wp?.backdropKind || 'none',
    wallpaperTitle: wp?.title || '',
    windowTranslucency: config.windowTranslucency,
    backdropBlur: config.backdropBlur,
    backdropDim: config.backdropDim,
    videoMuted: config.videoMuted,
    videoVolume: config.videoVolume,
    hudSize: config.hudSize,
    hudAccent: config.hudAccent,
    hudWarnColor: config.hudWarnColor,
    hudDangerColor: config.hudDangerColor,
    hudShowPercent: config.hudShowPercent,
    hudFont: config.hudFont,
    contextPercent: telemetry.active.percent,
    contextTokens: telemetry.active.contextTokens,
    contextLimit: telemetry.limitTokens,
    cacheHitRate: telemetry.active.cacheHitRate,
    sessionTitle: telemetry.active.title,
    model: telemetry.active.model,
    totalSteps: telemetry.active.totalSteps,
    updatedAt: Date.now(),
  };
}

async function refreshAndSyncCompanion() {
  await mediaServer.start();
  await ensureLibrary(false);

  // Automatic rotation tick
  if (config.rotationEnabled && config.backdropEnabled) {
    const intervalMs = config.rotationIntervalMin * 60 * 1000;
    if (Date.now() - lastRotationAt >= intervalMs) {
      const next = pickNextWallpaper();
      if (next && next.id !== config.wallpaperId) {
        config = sanitizeConfig({ ...config, wallpaperId: next.id });
        saveConfig(config);
      }
      lastRotationAt = Date.now();
    }
  }

  const activeWin = readActiveWindowInfo();
  const telemetry = getContextTelemetry({
    limitTokens: config.contextLimit,
    pinnedConvId: config.pinnedConvId,
    activeWindowTitle: activeWin.windowTitle,
    panelConvId,
  });
  const companion = await syncCompanion(buildCompanionPayload(telemetry));
  mediaServer.broadcastStageState();
  return { telemetry, companion };
}

function guessMime(filePath) {
  const ext = extname(filePath).toLowerCase();
  if (ext === '.jpg' || ext === '.jpeg') return 'image/jpeg';
  if (ext === '.png') return 'image/png';
  if (ext === '.gif') return 'image/gif';
  if (ext === '.webp') return 'image/webp';
  if (ext === '.bmp') return 'image/bmp';
  return 'application/octet-stream';
}

const app = new SidecarApp();

app.page('/', () => readFileSync(join(__dirname, 'index.html'), 'utf8'));

app.api(
  '/api/state',
  async () => {
    const { telemetry, companion } = await refreshAndSyncCompanion();
    const activeWp = findActiveWallpaper();
    return {
      config,
      telemetry,
      companion,
      rotation: getRotationStatus(),
      mediaPort: mediaServer.getPort(),
      activeWallpaper: activeWp
        ? {
            id: activeWp.id,
            title: activeWp.title,
            type: activeWp.type,
            renderMode: activeWp.renderMode,
            schemeColor: activeWp.schemeColor,
            hasPreview: Boolean(activeWp.previewPath),
          }
        : null,
      librarySummary: libraryCache
        ? {
            counts: libraryCache.counts,
            scannedRoots: libraryCache.scannedRoots,
            weAssetsAvailable: Boolean(libraryCache.weAssetsDir),
          }
        : {
            counts: { total: 0, video: 0, scene: 0, web: 0, other: 0 },
            scannedRoots: [],
            weAssetsAvailable: false,
          },
    };
  },
  'GET',
);

app.api(
  '/api/library',
  async (data) => {
    const force = String(data?.force || '') === '1';
    const lib = await ensureLibrary(force);
    const customSet = new Set(config.rotationCustomIds);
    return {
      counts: lib.counts,
      scannedRoots: lib.scannedRoots,
      weAssetsAvailable: Boolean(lib.weAssetsDir),
      items: lib.items.map((it) => ({
        id: it.id,
        title: it.title,
        type: it.type,
        renderMode: it.renderMode,
        schemeColor: it.schemeColor,
        rating: it.rating,
        tags: it.tags,
        isDshFavorite: it.isDshFavorite,
        isDshActive: it.isDshActive,
        inCustomPool: customSet.has(it.id),
        hasPreview: Boolean(it.previewPath),
      })),
    };
  },
  'GET',
);

app.api(
  '/api/preview',
  async (data) => {
    const id = typeof data?.id === 'string' ? data.id : '';
    const lib = await ensureLibrary(false);
    const item = lib.items.find((it) => it.id === id);
    if (!item || !item.previewPath || !existsSync(item.previewPath)) {
      return new Response('Not found', { status: 404 });
    }
    return new Response(readFileSync(item.previewPath), {
      contentType: guessMime(item.previewPath),
      headers: { 'Cache-Control': 'private, max-age=3600' },
    });
  },
  'GET',
);

// Ultra-fast live slider preview route (no disk write, instant SSE + UDP push)
app.api('/api/live', (data) => {
  if (!data || typeof data !== 'object') return { ok: true };
  config = sanitizeConfig({ ...config, ...data });
  mediaServer.broadcastStageState();
  sendCompanionLivePatch({
    windowTranslucency: config.windowTranslucency,
    backdropBlur: config.backdropBlur,
    backdropDim: config.backdropDim,
    hudSize: config.hudSize,
    hudAccent: config.hudAccent,
    hudWarnColor: config.hudWarnColor,
    hudDangerColor: config.hudDangerColor,
    hudShowPercent: config.hudShowPercent,
    hudFont: config.hudFont,
    docMaskEnabled: config.docMaskEnabled,
    docMaskColor: resolveDocMaskColor(),
  });
  return { ok: true };
});

app.api('/api/config', async (data) => {
  const prevInterval = config.rotationIntervalMin;
  const prevEnabled = config.rotationEnabled;
  const prevWp = config.wallpaperId;
  config = sanitizeConfig({ ...config, ...(data || {}) });
  saveConfig(config);

  if (
    config.rotationEnabled !== prevEnabled ||
    config.rotationIntervalMin !== prevInterval ||
    config.wallpaperId !== prevWp
  ) {
    lastRotationAt = Date.now();
  }

  const { telemetry, companion } = await refreshAndSyncCompanion();
  const activeWp = findActiveWallpaper();
  return {
    ok: true,
    config,
    telemetry,
    companion,
    rotation: getRotationStatus(),
    activeWallpaper: activeWp
      ? {
          id: activeWp.id,
          title: activeWp.title,
          type: activeWp.type,
          renderMode: activeWp.renderMode,
          schemeColor: activeWp.schemeColor,
          hasPreview: Boolean(activeWp.previewPath),
        }
      : null,
  };
});

app.api('/api/rotation/next', async () => {
  await ensureLibrary(false);
  const next = pickNextWallpaper();
  if (next) {
    config = sanitizeConfig({ ...config, wallpaperId: next.id, backdropEnabled: true });
    saveConfig(config);
    lastRotationAt = Date.now();
  }
  const { telemetry, companion } = await refreshAndSyncCompanion();
  const activeWp = findActiveWallpaper();
  return {
    ok: true,
    config,
    telemetry,
    companion,
    rotation: getRotationStatus(),
    activeWallpaper: activeWp
      ? {
          id: activeWp.id,
          title: activeWp.title,
          type: activeWp.type,
          renderMode: activeWp.renderMode,
          schemeColor: activeWp.schemeColor,
          hasPreview: Boolean(activeWp.previewPath),
        }
      : null,
  };
});

app.api('/api/rotation/toggle-item', async (data) => {
  const id = typeof data?.id === 'string' ? data.id.trim() : '';
  if (!id) throw new Error('Missing wallpaper id');
  const set = new Set(config.rotationCustomIds);
  if (set.has(id)) set.delete(id);
  else set.add(id);
  config = sanitizeConfig({ ...config, rotationCustomIds: [...set] });
  saveConfig(config);
  return {
    ok: true,
    inCustomPool: set.has(id),
    rotationCustomIds: config.rotationCustomIds,
    rotation: getRotationStatus(),
  };
});

app.api('/api/active-conv', async (data) => {
  const cid = typeof data?.conversationId === 'string' ? data.conversationId.trim() : '';
  if (cid && cid !== panelConvId) {
    panelConvId = cid;
    await refreshAndSyncCompanion();
  }
  return { ok: true, panelConvId };
});

app.api('/api/hud/reset-pos', async () => {
  try {
    if (existsSync(HUD_POS_PATH)) unlinkSync(HUD_POS_PATH);
  } catch {
    // ignore
  }
  const { telemetry, companion } = await refreshAndSyncCompanion();
  return { ok: true, telemetry, companion };
});

app.api('/api/companion/restore', async () => {
  config = sanitizeConfig({ ...config, backdropEnabled: false });
  saveConfig(config);
  await stopCompanion();
  const { telemetry, companion } = await refreshAndSyncCompanion();
  return { ok: true, config, telemetry, companion, rotation: getRotationStatus() };
});

// Fast active-window conversation switch detector (200ms)
setInterval(() => {
  if (!config.hudEnabled && !config.backdropEnabled) return;
  const w = readActiveWindowInfo();
  if (w.ts !== lastSeenWinTs) {
    lastSeenWinTs = w.ts;
    refreshAndSyncCompanion().catch(() => {});
  }
}, 200);

// Background telemetry & rotation timer (1200ms)
setInterval(() => {
  if (!config.backdropEnabled && !config.hudEnabled) return;
  refreshAndSyncCompanion().catch(() => {});
}, 1200);

// Initial boot sync + cold-start retry (guarantees auto-start on Antigravity launch)
refreshAndSyncCompanion().catch(() => {});
setTimeout(() => {
  refreshAndSyncCompanion().catch(() => {});
}, 2500);

for (const sig of ['SIGINT', 'SIGTERM', 'SIGHUP']) {
  process.on(sig, () => {
    mediaServer.close();
    stopCompanion()
      .catch(() => {})
      .finally(() => process.exit(0));
  });
}

app.run();
