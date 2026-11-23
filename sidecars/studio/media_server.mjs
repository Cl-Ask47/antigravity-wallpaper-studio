import { createReadStream, existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { dirname, extname, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ENGINE_PATH = join(__dirname, 'vendor', 'webwallgl', 'webwallgl.global.min.js');
const STAGE_HTML_PATH = join(__dirname, 'stage.html');

const MIME_MAP = {
  '.html': 'text/html; charset=utf-8',
  '.htm': 'text/html; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.mjs': 'application/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.gif': 'image/gif',
  '.webp': 'image/webp',
  '.bmp': 'image/bmp',
  '.ico': 'image/x-icon',
  '.mp4': 'video/mp4',
  '.webm': 'video/webm',
  '.mov': 'video/quicktime',
  '.m4v': 'video/mp4',
  '.mkv': 'video/x-matroska',
  '.ogg': 'audio/ogg',
  '.mp3': 'audio/mpeg',
  '.wav': 'audio/wav',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.ttf': 'font/ttf',
  '.otf': 'font/otf',
  '.wasm': 'application/wasm',
  '.pkg': 'application/octet-stream',
  '.tex': 'application/octet-stream',
};

const WE_WEB_SHIM = `<script>
(function(){
  if (!window.wallpaperRegisterAudioListener) {
    window.wallpaperRegisterAudioListener = function(){};
  }
  if (!window.wallpaperRegisterMediaStatusListener) {
    window.wallpaperRegisterMediaStatusListener = function(){};
  }
  if (!window.wallpaperRegisterMediaPropertiesListener) {
    window.wallpaperRegisterMediaPropertiesListener = function(){};
  }
  if (!window.wallpaperRegisterMediaThumbnailListener) {
    window.wallpaperRegisterMediaThumbnailListener = function(){};
  }
  if (!window.wallpaperRegisterMediaPlaybackListener) {
    window.wallpaperRegisterMediaPlaybackListener = function(){};
  }
  if (!window.wallpaperRegisterMediaTimelineListener) {
    window.wallpaperRegisterMediaTimelineListener = function(){};
  }
})();
</script>`;

function mimeOf(filePath) {
  return MIME_MAP[extname(filePath).toLowerCase()] || 'application/octet-stream';
}

function sendJson(res, status, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Cache-Control': 'no-store',
    'Access-Control-Allow-Origin': '*',
  });
  res.end(body);
}

function serveFileRange(filePath, req, res, { injectWebShim = false } = {}) {
  let st;
  try {
    st = statSync(filePath);
    if (!st.isFile()) {
      res.writeHead(404);
      res.end('Not found');
      return;
    }
  } catch {
    res.writeHead(404);
    res.end('Not found');
    return;
  }

  const mime = mimeOf(filePath);
  const method = (req.method || 'GET').toUpperCase();

  if (injectWebShim && (extname(filePath).toLowerCase() === '.html' || extname(filePath).toLowerCase() === '.htm')) {
    try {
      let html = readFileSync(filePath, 'utf8');
      if (/<head[^>]*>/i.test(html)) {
        html = html.replace(/<head([^>]*)>/i, `<head$1>${WE_WEB_SHIM}`);
      } else {
        html = WE_WEB_SHIM + html;
      }
      const buf = Buffer.from(html, 'utf8');
      res.writeHead(200, {
        'Content-Type': 'text/html; charset=utf-8',
        'Content-Length': buf.length,
        'Cache-Control': 'no-store',
        'Access-Control-Allow-Origin': '*',
      });
      res.end(method === 'HEAD' ? undefined : buf);
      return;
    } catch {
      // fall through
    }
  }

  const etag = `W/"${st.size}-${Math.trunc(st.mtimeMs)}"`;
  if (req.headers['if-none-match'] === etag && !req.headers.range) {
    res.writeHead(304, { ETag: etag, 'Access-Control-Allow-Origin': '*' });
    res.end();
    return;
  }

  const rangeHeader = req.headers.range;
  if (rangeHeader && typeof rangeHeader === 'string' && rangeHeader.startsWith('bytes=')) {
    const m = /^bytes=(\d*)-(\d*)$/i.exec(rangeHeader.trim());
    if (m) {
      let start = m[1] ? parseInt(m[1], 10) : 0;
      let end = m[2] ? parseInt(m[2], 10) : st.size - 1;
      if (!m[1] && m[2]) {
        const suffix = parseInt(m[2], 10);
        start = Math.max(0, st.size - suffix);
        end = st.size - 1;
      }
      if (Number.isFinite(start) && Number.isFinite(end) && start >= 0 && start <= end && start < st.size) {
        end = Math.min(end, st.size - 1);
        const len = end - start + 1;
        res.writeHead(206, {
          'Content-Type': mime,
          'Content-Length': len,
          'Content-Range': `bytes ${start}-${end}/${st.size}`,
          'Accept-Ranges': 'bytes',
          ETag: etag,
          'Cache-Control': 'private, max-age=300',
          'Access-Control-Allow-Origin': '*',
        });
        if (method === 'HEAD') {
          res.end();
          return;
        }
        const stream = createReadStream(filePath, { start, end });
        stream.on('error', () => {
          try { res.end(); } catch {}
        });
        stream.pipe(res);
        return;
      }
    }
  }

  res.writeHead(200, {
    'Content-Type': mime,
    'Content-Length': st.size,
    'Accept-Ranges': 'bytes',
    ETag: etag,
    'Cache-Control': 'private, max-age=300',
    'Access-Control-Allow-Origin': '*',
  });
  if (method === 'HEAD') {
    res.end();
    return;
  }
  const stream = createReadStream(filePath);
  stream.on('error', () => {
    try { res.end(); } catch {}
  });
  stream.pipe(res);
}

let cachedAssetNames = null;
let cachedAssetRoot = '';

function listMaterialTexNames(assetsRoot) {
  if (cachedAssetNames && cachedAssetRoot === assetsRoot) return cachedAssetNames;
  const matRoot = join(assetsRoot, 'materials');
  const names = [];
  if (existsSync(matRoot)) {
    const walk = (dir) => {
      let ents = [];
      try {
        ents = readdirSync(dir, { withFileTypes: true });
      } catch {
        return;
      }
      for (const e of ents) {
        const full = join(dir, e.name);
        if (e.isDirectory()) walk(full);
        else if (e.isFile() && e.name.toLowerCase().endsWith('.tex')) {
          names.push(relative(matRoot, full).replace(/\\/g, '/'));
        }
      }
    };
    walk(matRoot);
  }
  cachedAssetRoot = assetsRoot;
  cachedAssetNames = names;
  return names;
}

export function createMediaServer({ getLibrary, getStageState }) {
  let server = null;
  let port = 0;
  const sseClients = new Set();

  function broadcastStageState() {
    if (sseClients.size === 0) return;
    let payload = '';
    try {
      payload = `data: ${JSON.stringify(getStageState())}\n\n`;
    } catch {
      return;
    }
    for (const res of sseClients) {
      try {
        res.write(payload);
      } catch {
        sseClients.delete(res);
      }
    }
  }

  async function start() {
    if (server && port > 0) return port;

    server = createServer(async (req, res) => {
      const method = (req.method || 'GET').toUpperCase();
      if (method === 'OPTIONS') {
        res.writeHead(204, {
          'Access-Control-Allow-Origin': '*',
          'Access-Control-Allow-Methods': 'GET, HEAD, OPTIONS',
          'Access-Control-Allow-Headers': 'Range, If-None-Match',
        });
        res.end();
        return;
      }
      if (method !== 'GET' && method !== 'HEAD') {
        res.writeHead(405);
        res.end('Method not allowed');
        return;
      }

      let pathname = '/';
      try {
        pathname = new URL(req.url || '/', 'http://127.0.0.1').pathname;
      } catch {
        res.writeHead(400);
        res.end('Bad request');
        return;
      }

      if (pathname === '/stage' || pathname === '/stage/') {
        try {
          const html = readFileSync(STAGE_HTML_PATH, 'utf8');
          const buf = Buffer.from(html, 'utf8');
          res.writeHead(200, {
            'Content-Type': 'text/html; charset=utf-8',
            'Content-Length': buf.length,
            'Cache-Control': 'no-store',
          });
          res.end(method === 'HEAD' ? undefined : buf);
        } catch {
          res.writeHead(500);
          res.end('Missing stage.html');
        }
        return;
      }

      if (pathname === '/stage-state') {
        sendJson(res, 200, getStageState());
        return;
      }

      if (pathname === '/stage-events') {
        res.writeHead(200, {
          'Content-Type': 'text/event-stream; charset=utf-8',
          'Cache-Control': 'no-cache, no-store',
          Connection: 'keep-alive',
          'Access-Control-Allow-Origin': '*',
        });
        sseClients.add(res);
        try {
          res.write(`data: ${JSON.stringify(getStageState())}\n\n`);
        } catch {
          sseClients.delete(res);
        }
        req.on('close', () => {
          sseClients.delete(res);
        });
        return;
      }

      if (pathname === '/engine/webwallgl.global.min.js') {
        if (!existsSync(ENGINE_PATH)) {
          res.writeHead(404);
          res.end('WebWallGL missing');
          return;
        }
        serveFileRange(ENGINE_PATH, req, res);
        return;
      }

      // Official WE local-assets contract for WebWallGL
      if (pathname === '/api/local-assets' || pathname.startsWith('/api/local-assets/')) {
        const lib = await getLibrary();
        const assetsDir = lib?.weAssetsDir || '';
        const ok = Boolean(assetsDir && existsSync(join(assetsDir, 'materials')));
        const rest = pathname.slice('/api/local-assets'.length);
        if (rest === '' || rest === '/') {
          sendJson(res, 200, { ok, roots: ok ? [{ id: 'local', dir: assetsDir }] : [] });
          return;
        }
        if (!ok) {
          sendJson(res, 404, { ok: false });
          return;
        }
        let segs;
        try {
          segs = rest.replace(/^\/+/, '').split('/').filter(Boolean).map(decodeURIComponent);
        } catch {
          res.writeHead(400);
          res.end('Bad path');
          return;
        }
        const rootId = segs.shift() || '';
        if (rootId !== 'local') {
          res.writeHead(404);
          res.end('Unknown asset root');
          return;
        }
        const rel = segs.join('/');
        if (rel === 'materials/index.json') {
          sendJson(res, 200, { names: listMaterialTexNames(assetsDir) });
          return;
        }
        const target = resolve(assetsDir, rel);
        if (!target.startsWith(resolve(assetsDir) + sep)) {
          res.writeHead(403);
          res.end('Forbidden');
          return;
        }
        serveFileRange(target, req, res);
        return;
      }

      // Preview image by wallpaper ID
      if (pathname.startsWith('/preview/')) {
        let id = '';
        try {
          id = decodeURIComponent(pathname.slice('/preview/'.length));
        } catch {
          res.writeHead(400);
          res.end('Bad id');
          return;
        }
        const lib = await getLibrary();
        const item = lib?.items?.find((x) => x.id === id);
        if (!item || !item.previewPath || !existsSync(item.previewPath)) {
          res.writeHead(404);
          res.end('No preview');
          return;
        }
        serveFileRange(item.previewPath, req, res);
        return;
      }

      // Wallpaper project files: /wp/<id>/<relPath>
      if (pathname.startsWith('/wp/')) {
        const rest = pathname.slice('/wp/'.length);
        const slashIdx = rest.indexOf('/');
        if (slashIdx < 1) {
          res.writeHead(404);
          res.end('Not found');
          return;
        }
        let id = '';
        let rel = '';
        try {
          id = decodeURIComponent(rest.slice(0, slashIdx));
          rel = rest
            .slice(slashIdx + 1)
            .split('/')
            .map((seg) => decodeURIComponent(seg))
            .join('/');
        } catch {
          res.writeHead(400);
          res.end('Bad path');
          return;
        }
        if (!rel) rel = 'index.html';

        const lib = await getLibrary();
        const item = lib?.items?.find((x) => x.id === id);
        if (!item || !item.dir) {
          res.writeHead(404);
          res.end('Wallpaper not found');
          return;
        }
        const rootDir = resolve(item.dir);
        const target = resolve(rootDir, rel);
        if (target !== rootDir && !target.startsWith(rootDir + sep)) {
          res.writeHead(403);
          res.end('Forbidden');
          return;
        }
        const isWebEntry =
          item.renderMode === 'web' &&
          item.entryFile &&
          rel.replace(/^\/+/, '').toLowerCase() === item.entryFile.replace(/^\/+/, '').toLowerCase();
        serveFileRange(target, req, res, { injectWebShim: isWebEntry });
        return;
      }

      res.writeHead(404);
      res.end('Not found');
    });

    await new Promise((resolveReady, rejectReady) => {
      server.once('error', rejectReady);
      server.listen(0, '127.0.0.1', () => {
        const addr = server.address();
        port = typeof addr === 'object' && addr ? addr.port : 0;
        resolveReady();
      });
    });

    return port;
  }

  function getPort() {
    return port;
  }

  function close() {
    for (const res of sseClients) {
      try { res.end(); } catch {}
    }
    sseClients.clear();
    try {
      server?.close();
    } catch {
      // ignore
    }
    server = null;
    port = 0;
  }

  return { start, getPort, broadcastStageState, close };
}
