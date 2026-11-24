import { execFile, spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createSocket } from 'node:dgram';
import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);
const __dirname = dirname(fileURLToPath(import.meta.url));
const CS_SRC = join(__dirname, 'companion', 'StudioCompanion.cs');
const WV2_VENDOR_DIR = join(__dirname, 'vendor', 'webview2');
const RUNTIME_DIR = join(tmpdir(), 'agy_wallpaper_studio');
const STATE_PATH = join(RUNTIME_DIR, 'companion_state.json');
const HUD_POS_PATH = join(RUNTIME_DIR, 'hud_offset.json');
const ACTIVE_WIN_PATH = join(RUNTIME_DIR, 'active_window.json');
const UDP_PORT_PATH = join(RUNTIME_DIR, 'companion_udp.port');
const LOG_PATH = join(RUNTIME_DIR, 'companion.log');

mkdirSync(RUNTIME_DIR, { recursive: true });

let childProc = null;
let spawnInflight = null;
let compiledExePath = '';
let lastCompileError = '';
let udpClient = null;
let cachedUdpPort = 0;
let cachedUdpPortMtime = 0;

export function sendCompanionLivePatch(patchObj) {
  try {
    if (!existsSync(UDP_PORT_PATH)) return false;
    const raw = readFileSync(UDP_PORT_PATH, 'utf8').trim();
    const port = parseInt(raw, 10);
    if (!Number.isFinite(port) || port < 1024 || port > 65535) return false;
    if (!udpClient) {
      udpClient = createSocket('udp4');
      udpClient.unref();
    }
    const buf = Buffer.from(JSON.stringify(patchObj), 'utf8');
    udpClient.send(buf, port, '127.0.0.1');
    return true;
  } catch {
    return false;
  }
}

function findCscExe() {
  const candidates = [
    'C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe',
    'C:\\Windows\\Microsoft.NET\\Framework\\v4.0.30319\\csc.exe',
  ];
  for (const c of candidates) {
    if (existsSync(c)) return c;
  }
  return '';
}

function findWpfDir() {
  const candidates = [
    'C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\WPF',
    'C:\\Windows\\Microsoft.NET\\Framework\\v4.0.30319\\WPF',
  ];
  for (const c of candidates) {
    if (existsSync(join(c, 'PresentationFramework.dll'))) return c;
  }
  return '';
}

function stageWebView2Dlls() {
  const dlls = [
    'Microsoft.Web.WebView2.Core.dll',
    'Microsoft.Web.WebView2.Wpf.dll',
    'WebView2Loader.dll',
  ];
  for (const f of dlls) {
    const src = join(WV2_VENDOR_DIR, f);
    const dst = join(RUNTIME_DIR, f);
    if (!existsSync(src)) return false;
    if (!existsSync(dst)) {
      try {
        copyFileSync(src, dst);
      } catch {
        // ignore if locked by running exe
      }
    }
  }
  return true;
}

export async function ensureCompanionCompiled() {
  if (process.platform !== 'win32') {
    return { ok: false, error: 'Desktop companion is only supported on Windows.' };
  }
  if (!existsSync(CS_SRC)) {
    return { ok: false, error: `Missing source: ${CS_SRC}` };
  }

  const hasWv2 = stageWebView2Dlls();
  const srcBytes = readFileSync(CS_SRC);
  const hash = createHash('sha256')
    .update(srcBytes)
    .update(hasWv2 ? 'wv2' : 'nowv2')
    .digest('hex')
    .slice(0, 12);
  const targetExe = join(RUNTIME_DIR, `studio_companion_${hash}.exe`);

  if (existsSync(targetExe)) {
    compiledExePath = targetExe;
    lastCompileError = '';
    return { ok: true, exe: targetExe, cached: true, webview2: hasWv2 };
  }

  const csc = findCscExe();
  const wpfDir = findWpfDir();
  if (!csc || !wpfDir) {
    lastCompileError = '未找到 Windows .NET Framework 4.x 编译器 (csc.exe / WPF)。';
    return { ok: false, error: lastCompileError };
  }

  const args = [
    '/nologo',
    '/optimize+',
    '/platform:x64',
    '/target:winexe',
    `/out:${targetExe}`,
    `/lib:${wpfDir}`,
    `/lib:${RUNTIME_DIR}`,
    '/r:PresentationCore.dll',
    '/r:PresentationFramework.dll',
    '/r:WindowsBase.dll',
    '/r:System.Xaml.dll',
    '/r:System.Web.Extensions.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:UIAutomationClient.dll',
    '/r:UIAutomationTypes.dll',
  ];

  if (hasWv2) {
    args.push('/r:Microsoft.Web.WebView2.Core.dll');
    args.push('/r:Microsoft.Web.WebView2.Wpf.dll');
  }
  args.push(CS_SRC);

  try {
    await execFileAsync(csc, args, { windowsHide: true, timeout: 30000 });
    compiledExePath = targetExe;
    lastCompileError = '';
    return { ok: true, exe: targetExe, cached: false, webview2: hasWv2 };
  } catch (err) {
    const detail = String(err?.stdout || err?.stderr || err?.message || err).trim();
    lastCompileError = detail.slice(0, 800);
    return { ok: false, error: lastCompileError };
  }
}

export function readActiveWindowInfo() {
  try {
    if (existsSync(ACTIVE_WIN_PATH)) {
      const o = JSON.parse(readFileSync(ACTIVE_WIN_PATH, 'utf8'));
      return {
        windowTitle: typeof o?.windowTitle === 'string' ? o.windowTitle : '',
        ts: Number(o?.ts) || 0,
      };
    }
  } catch {
    // ignore
  }
  return { windowTitle: '', ts: 0 };
}

export function getActiveWinPath() {
  return ACTIVE_WIN_PATH;
}

export function writeCompanionState(stateObj) {
  mkdirSync(RUNTIME_DIR, { recursive: true });
  writeFileSync(STATE_PATH, JSON.stringify(stateObj, null, 2), 'utf8');
}

export async function syncCompanion(stateObj) {
  writeCompanionState(stateObj);
  const wantRunning = Boolean(stateObj.backdropEnabled || stateObj.hudEnabled);

  if (!wantRunning) {
    await stopCompanion();
    return getCompanionStatus();
  }

  if (childProc && !childProc.killed && childProc.exitCode === null) {
    return getCompanionStatus();
  }
  if (spawnInflight) {
    await spawnInflight;
    return getCompanionStatus();
  }

  spawnInflight = (async () => {
    const compiled = await ensureCompanionCompiled();
    if (!compiled.ok) return;
    if (childProc && !childProc.killed && childProc.exitCode === null) return;
    try {
      childProc = spawn(
        compiled.exe,
        [
          '--state',
          STATE_PATH,
          '--hud-pos',
          HUD_POS_PATH,
          '--active-win',
          ACTIVE_WIN_PATH,
          '--udp-port-file',
          UDP_PORT_PATH,
          '--log',
          LOG_PATH,
          '--parent-pid',
          String(process.pid),
        ],
        {
          windowsHide: true,
          stdio: 'ignore',
          detached: false,
        },
      );
      childProc.on('exit', () => {
        childProc = null;
      });
      childProc.on('error', () => {
        childProc = null;
      });
    } catch (err) {
      lastCompileError = String(err?.message || err);
      childProc = null;
    }
  })().finally(() => {
    spawnInflight = null;
  });

  await spawnInflight;
  return getCompanionStatus();
}

export async function stopCompanion() {
  if (childProc && !childProc.killed && childProc.exitCode === null) {
    try {
      writeCompanionState({ shutdown: true, backdropEnabled: false, hudEnabled: false });
      await new Promise((r) => setTimeout(r, 260));
    } catch {
      // ignore
    }
    try {
      if (childProc && childProc.exitCode === null) {
        childProc.kill();
      }
    } catch {
      // ignore
    }
    childProc = null;
  }
  if (compiledExePath && existsSync(compiledExePath)) {
    try {
      await execFileAsync(compiledExePath, ['--restore'], { windowsHide: true, timeout: 4000 });
    } catch {
      // ignore
    }
  }
}

export function getCompanionStatus() {
  return {
    supported: process.platform === 'win32',
    running: Boolean(childProc && !childProc.killed && childProc.exitCode === null),
    pid: childProc?.pid || 0,
    exePath: compiledExePath,
    error: lastCompileError,
    logPath: LOG_PATH,
  };
}
