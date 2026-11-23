import { execFile } from 'node:child_process';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { homedir } from 'node:os';
import { extname, join, normalize, resolve } from 'node:path';

const WE_APPID = '431960';
const STEAM_FALLBACK_DIRS = [
  'C:\\Program Files (x86)\\Steam',
  'C:\\Program Files\\Steam',
  'D:\\Steam',
  'D:\\steam',
  'D:\\SteamLibrary',
  'E:\\SteamLibrary',
  'F:\\SteamLibrary',
];

const VIDEO_EXTS = new Set(['.mp4', '.webm', '.mov', '.m4v', '.wmv', '.avi', '.mkv']);
const IMAGE_EXTS = new Set(['.jpg', '.jpeg', '.png', '.webp', '.gif', '.bmp']);

function querySteamRegistry() {
  if (process.platform !== 'win32') return Promise.resolve(null);
  const reg = join(process.env.SystemRoot || 'C:\\Windows', 'System32', 'reg.exe');
  return new Promise((done) => {
    try {
      execFile(
        reg,
        ['query', 'HKCU\\Software\\Valve\\Steam', '/v', 'SteamPath'],
        { encoding: 'utf8', windowsHide: true, timeout: 4000 },
        (err, stdout) => {
          if (err || !stdout) return done(null);
          const m = /SteamPath\s+REG_SZ\s+(.+)/i.exec(stdout);
          done(m ? normalize(m[1].trim()) : null);
        },
      );
    } catch {
      done(null);
    }
  });
}

function parseLibraryFoldersVdf(vdfPath) {
  const dirs = [];
  if (!existsSync(vdfPath)) return dirs;
  try {
    const raw = readFileSync(vdfPath, 'utf8');
    const re = /"path"\s+"([^"]+)"/gi;
    let m;
    while ((m = re.exec(raw)) !== null) {
      const p = normalize(m[1].replace(/\\\\/g, '\\').trim());
      if (p && existsSync(p)) dirs.push(p);
    }
  } catch {
    // ignore
  }
  return dirs;
}

function rgbFloatToHex(raw) {
  if (typeof raw !== 'string') return '';
  const parts = raw.trim().split(/\s+/).map(Number);
  if (parts.length < 3 || parts.some((n) => !Number.isFinite(n))) return '';
  const toByte = (v) => Math.max(0, Math.min(255, Math.round(v <= 1 ? v * 255 : v)));
  return (
    '#' +
    [toByte(parts[0]), toByte(parts[1]), toByte(parts[2])]
      .map((b) => b.toString(16).padStart(2, '0'))
      .join('')
  );
}

function safeInside(root, target) {
  const r = resolve(root).toLowerCase();
  const t = resolve(target).toLowerCase();
  return t === r || t.startsWith(r + '\\') || t.startsWith(r + '/');
}

function pickPreviewPath(itemDir, previewField) {
  const candidates = [];
  if (typeof previewField === 'string' && previewField.trim()) {
    candidates.push(join(itemDir, previewField.trim()));
  }
  for (const fallback of ['preview.jpg', 'preview.png', 'preview.gif', 'preview.webp']) {
    candidates.push(join(itemDir, fallback));
  }
  for (const c of candidates) {
    if (safeInside(itemDir, c) && existsSync(c)) {
      try {
        if (statSync(c).isFile()) return resolve(c);
      } catch {
        // ignore
      }
    }
  }
  return '';
}

function parseWallpaperDir(itemDir, id, source) {
  const pj = join(itemDir, 'project.json');
  if (!existsSync(pj)) return null;
  let meta;
  try {
    meta = JSON.parse(readFileSync(pj, 'utf8'));
  } catch {
    return null;
  }

  const rawType = String(meta.type || 'unknown').trim().toLowerCase();
  if (rawType === 'application') return null;

  const title = String(meta.title || id).trim() || id;
  const previewPath = pickPreviewPath(itemDir, meta.preview);
  const fileRel = typeof meta.file === 'string' ? meta.file.trim().replace(/\\/g, '/') : '';
  let mediaPath = '';
  if (fileRel) {
    const candidate = join(itemDir, fileRel);
    if (safeInside(itemDir, candidate) && existsSync(candidate)) {
      try {
        if (statSync(candidate).isFile()) mediaPath = resolve(candidate);
      } catch {
        // ignore
      }
    }
  }

  const mediaExt = extname(mediaPath).toLowerCase();
  const isVideo = rawType === 'video' || VIDEO_EXTS.has(mediaExt);
  const isWeb = rawType === 'web' || mediaExt === '.html' || mediaExt === '.htm';
  const isScene =
    rawType === 'scene' ||
    existsSync(join(itemDir, 'scene.pkg')) ||
    existsSync(join(itemDir, 'scene.json')) ||
    mediaExt === '.json';
  const isImage = IMAGE_EXTS.has(mediaExt);

  let renderMode = 'image';
  if (isVideo && mediaPath) renderMode = 'video';
  else if (isWeb && mediaPath) renderMode = 'web';
  else if (isScene) renderMode = 'scene';
  else if (isImage && mediaPath) renderMode = 'image';
  else if (previewPath) renderMode = 'image';
  else renderMode = 'none';

  const effectiveBackdropPath =
    isVideo && mediaPath ? mediaPath : isImage && mediaPath ? mediaPath : previewPath;
  const backdropKind = isVideo && mediaPath ? 'video' : effectiveBackdropPath ? 'image' : 'none';
  const schemeColor = rgbFloatToHex(meta?.general?.properties?.schemecolor?.value || '');

  let mtimeMs = 0;
  try {
    mtimeMs = statSync(pj).mtimeMs;
  } catch {
    // ignore
  }

  return {
    id: String(id),
    title,
    type: rawType || 'unknown',
    renderMode,
    source,
    dir: resolve(itemDir),
    entryFile: fileRel,
    previewPath,
    mediaPath,
    effectiveBackdropPath,
    backdropKind,
    schemeColor,
    rating: String(meta.contentrating || 'Everyone'),
    tags: Array.isArray(meta.tags) ? meta.tags.slice(0, 6).map(String) : [],
    mtimeMs,
  };
}

export function readDshWeHints() {
  const p = join(homedir(), '.dsh-wallpaper-engine', 'config.json');
  if (!existsSync(p)) {
    return { activeId: '', accent: '', favoriteIds: [], rotationGroups: [], rotationEnabled: false };
  }
  try {
    const parsed = JSON.parse(readFileSync(p, 'utf8'));
    const s = parsed?.settings || {};
    const favoriteIds = [];
    const rotationGroups = [];
    if (Array.isArray(s.rotationGroups)) {
      for (const g of s.rotationGroups) {
        if (!g || !Array.isArray(g.wallpaperIds)) continue;
        const ids = g.wallpaperIds.map(String).filter(Boolean);
        rotationGroups.push({
          id: String(g.id || ''),
          name: String(g.name || 'DSH 播放列表'),
          interval: Number(g.interval) || 15,
          order: g.order === 'sequential' ? 'sequential' : 'random',
          wallpaperIds: ids,
        });
        for (const id of ids) favoriteIds.push(id);
      }
    }
    return {
      activeId: s.id ? String(s.id) : '',
      accent: typeof s.accent === 'string' ? s.accent : '',
      favoriteIds: [...new Set(favoriteIds)],
      rotationGroups,
      rotationEnabled: Boolean(s.rotationEnabled),
    };
  } catch {
    return { activeId: '', accent: '', favoriteIds: [], rotationGroups: [], rotationEnabled: false };
  }
}

let cachedScan = null;
let cachedAt = 0;

export async function scanWallpaperEngineLibrary({ force = false } = {}) {
  const now = Date.now();
  if (!force && cachedScan && now - cachedAt < 30000) {
    return cachedScan;
  }

  const rootSet = new Set();
  const regRoot = await querySteamRegistry();
  if (regRoot && existsSync(regRoot)) rootSet.add(resolve(regRoot));

  for (const d of STEAM_FALLBACK_DIRS) {
    if (existsSync(d)) rootSet.add(resolve(d));
  }

  const libMap = new Map();
  for (const r of rootSet) {
    const rr = resolve(r);
    libMap.set(rr.toLowerCase(), rr);
    const vdf = join(r, 'steamapps', 'libraryfolders.vdf');
    for (const lib of parseLibraryFoldersVdf(vdf)) {
      const rl = resolve(lib);
      libMap.set(rl.toLowerCase(), rl);
    }
  }
  const libSet = [...libMap.values()];

  const items = [];
  const seenIds = new Set();
  const scannedRoots = [];
  let weAssetsDir = '';

  for (const lib of libSet) {
    if (!weAssetsDir) {
      const candidateAssets = join(lib, 'steamapps', 'common', 'wallpaper_engine', 'assets');
      if (existsSync(join(candidateAssets, 'materials'))) {
        weAssetsDir = resolve(candidateAssets);
      }
    }

    const workshopDir = join(lib, 'steamapps', 'workshop', 'content', WE_APPID);
    if (existsSync(workshopDir)) {
      scannedRoots.push(workshopDir);
      let entries = [];
      try {
        entries = readdirSync(workshopDir, { withFileTypes: true });
      } catch {
        entries = [];
      }
      for (const ent of entries) {
        if (!ent.isDirectory()) continue;
        if (seenIds.has(ent.name)) continue;
        const item = parseWallpaperDir(join(workshopDir, ent.name), ent.name, 'workshop');
        if (item) {
          seenIds.add(item.id);
          items.push(item);
        }
      }
    }

    const defaultProjects = join(
      lib,
      'steamapps',
      'common',
      'wallpaper_engine',
      'projects',
      'defaultprojects',
    );
    if (existsSync(defaultProjects)) {
      scannedRoots.push(defaultProjects);
      let entries = [];
      try {
        entries = readdirSync(defaultProjects, { withFileTypes: true });
      } catch {
        entries = [];
      }
      for (const ent of entries) {
        if (!ent.isDirectory()) continue;
        const id = `default:${ent.name}`;
        if (seenIds.has(id)) continue;
        const item = parseWallpaperDir(join(defaultProjects, ent.name), id, 'default');
        if (item) {
          seenIds.add(item.id);
          items.push(item);
        }
      }
    }
  }

  const hints = readDshWeHints();
  const favSet = new Set(hints.favoriteIds);
  for (const it of items) {
    it.isDshFavorite = favSet.has(it.id);
    it.isDshActive = hints.activeId === it.id;
  }

  items.sort((a, b) => {
    if (a.isDshActive !== b.isDshActive) return a.isDshActive ? -1 : 1;
    if (a.isDshFavorite !== b.isDshFavorite) return a.isDshFavorite ? -1 : 1;
    if (a.renderMode !== b.renderMode) {
      const rank = { video: 0, scene: 1, web: 2, image: 3, none: 4 };
      return (rank[a.renderMode] ?? 9) - (rank[b.renderMode] ?? 9);
    }
    return b.mtimeMs - a.mtimeMs;
  });

  const counts = { total: items.length, video: 0, scene: 0, web: 0, other: 0 };
  for (const it of items) {
    if (it.renderMode === 'video') counts.video++;
    else if (it.renderMode === 'scene') counts.scene++;
    else if (it.renderMode === 'web') counts.web++;
    else counts.other++;
  }

  cachedScan = {
    scannedRoots,
    weAssetsDir,
    counts,
    dshHints: hints,
    items,
    scannedAt: now,
  };
  cachedAt = now;
  return cachedScan;
}
