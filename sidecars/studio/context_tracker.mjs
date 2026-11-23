import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';

const AGY_ROOT = join(homedir(), '.gemini', 'antigravity');
const BRAIN_ROOT = join(AGY_ROOT, 'brain');
const ANNOTATIONS_ROOT = join(AGY_ROOT, 'annotations');

const transcriptCache = new Map(); // convId -> { mtimeMs, size, summary }
const annotationCache = new Map(); // convId -> { mtimeMs, size, title, lastViewMs }

function cleanPromptTitle(raw) {
  if (typeof raw !== 'string') return '';
  const stripped = raw
    .replace(/<\/?USER_REQUEST>/gi, '')
    .replace(/\s+/g, ' ')
    .trim();
  return stripped.length > 42 ? stripped.slice(0, 42) + '…' : stripped;
}

function formatModelLabel(raw) {
  if (!raw || typeof raw !== 'string') return 'Unknown';
  return raw
    .replace(/^gemini-4-neon-high$/i, 'Gemini 4 Neon (High)')
    .replace(/^gemini-4-neon-low$/i, 'Gemini 4 Neon (Low)')
    .replace(/^gemini-4-hydrogen$/i, 'Gemini 4 Hydrogen')
    .replace(/^gemini-4-carbon$/i, 'Gemini 4 Carbon');
}

function unescapePbtxtString(s) {
  return s.replace(/\\(["\\ntr])/g, (_, ch) => {
    if (ch === 'n') return '\n';
    if (ch === 't') return '\t';
    if (ch === 'r') return '\r';
    return ch;
  });
}

function readAnnotation(convId) {
  const fp = join(ANNOTATIONS_ROOT, `${convId}.pbtxt`);
  if (!existsSync(fp)) return null;
  let st;
  try {
    st = statSync(fp);
  } catch {
    return null;
  }
  const cached = annotationCache.get(convId);
  if (cached && cached.mtimeMs === st.mtimeMs && cached.size === st.size) {
    return cached;
  }

  let raw = '';
  try {
    raw = readFileSync(fp, 'utf8');
  } catch {
    return null;
  }

  let title = '';
  const tm = /title:\s*"((?:[^"\\]|\\.)*)"/.exec(raw);
  if (tm) title = unescapePbtxtString(tm[1]).trim();

  let lastViewMs = 0;
  const vm = /last_user_view_time:\s*\{\s*seconds:\s*(\d+)(?:\s+nanos:\s*(\d+))?/.exec(raw);
  if (vm) {
    lastViewMs = Number(vm[1]) * 1000 + Math.floor((Number(vm[2]) || 0) / 1e6);
  } else {
    lastViewMs = st.mtimeMs;
  }

  const info = { mtimeMs: st.mtimeMs, size: st.size, title, lastViewMs };
  annotationCache.set(convId, info);
  return info;
}

function parseTranscriptFile(convId, filePath) {
  let st;
  try {
    st = statSync(filePath);
  } catch {
    return null;
  }

  const cached = transcriptCache.get(convId);
  if (cached && cached.mtimeMs === st.mtimeMs && cached.size === st.size) {
    return cached.summary;
  }

  let raw = '';
  try {
    raw = readFileSync(filePath, 'utf8');
  } catch {
    return null;
  }

  const lines = raw.split(/\r?\n/);
  let promptTitle = '';
  let model = '';
  let totalSteps = 0;
  let userTurns = 0;
  let plannerTurns = 0;
  let toolCallCount = 0;
  let latestInputTokens = 0;
  let latestCacheReadTokens = 0;
  let latestOutputTokens = 0;
  let totalOutputTokens = 0;
  let totalBilledInputTokens = 0;
  let totalCacheReadTokens = 0;
  let lastUpdatedAt = '';

  for (const line of lines) {
    if (!line || line[0] !== '{') continue;
    let obj;
    try {
      obj = JSON.parse(line);
    } catch {
      continue;
    }
    totalSteps++;
    if (obj.created_at) lastUpdatedAt = obj.created_at;

    if (obj.type === 'USER_INPUT') {
      userTurns++;
      if (!promptTitle && typeof obj.content === 'string') {
        promptTitle = cleanPromptTitle(obj.content);
      }
    } else if (obj.type === 'PLANNER_RESPONSE') {
      plannerTurns++;
      if (obj.model) model = String(obj.model);
      const inTok = Number(obj.input_tokens) || 0;
      const cacheTok = Number(obj.cache_read_tokens) || 0;
      const outTok = Number(obj.output_tokens) || 0;

      if (inTok + cacheTok > 0) {
        latestInputTokens = inTok;
        latestCacheReadTokens = cacheTok;
        latestOutputTokens = outTok;
      }
      totalBilledInputTokens += inTok;
      totalCacheReadTokens += cacheTok;
      totalOutputTokens += outTok;
      if (Array.isArray(obj.tool_calls)) {
        toolCallCount += obj.tool_calls.length;
      }
    }
  }

  const contextTokens = latestInputTokens + latestCacheReadTokens + latestOutputTokens;
  const latestPromptTokens = latestInputTokens + latestCacheReadTokens;
  const cacheHitRate =
    latestPromptTokens > 0 ? Math.round((latestCacheReadTokens / latestPromptTokens) * 1000) / 10 : 0;

  const summary = {
    id: convId,
    promptTitle: promptTitle || convId.slice(0, 8),
    model: formatModelLabel(model),
    rawModel: model,
    contextTokens,
    latestInputTokens,
    latestCacheReadTokens,
    latestOutputTokens,
    cacheHitRate,
    totalSteps,
    userTurns,
    plannerTurns,
    toolCallCount,
    totalOutputTokens,
    totalBilledInputTokens,
    totalCacheReadTokens,
    mtimeMs: st.mtimeMs,
    lastUpdatedAt,
  };

  transcriptCache.set(convId, { mtimeMs: st.mtimeMs, size: st.size, summary });
  return summary;
}

function normTitle(s) {
  return String(s || '')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();
}

function matchSessionByWindowTitle(windowTitle, sessions) {
  if (!windowTitle || typeof windowTitle !== 'string') return null;
  let cleaned = windowTitle.trim();
  if (!cleaned) return null;

  // Strip common Antigravity window suffixes
  cleaned = cleaned.replace(/\s*[-—–]\s*Antigravity$/i, '').trim();
  if (!cleaned || /^antigravity$/i.test(cleaned) || /^new conversation$/i.test(cleaned) || /^新建对话$/i.test(cleaned)) {
    return { isBlankHome: true };
  }

  // Electron window title can be "<Conversation Title> - <Workspace>" or truncated with "..." / "…"
  const candidates = [cleaned];
  const dashIdx = cleaned.indexOf(' - ');
  if (dashIdx > 0) {
    candidates.push(cleaned.slice(0, dashIdx).trim());
  }
  const strippedEllipsis = candidates
    .map((c) => c.replace(/(\.{3}|…)\s*$/, '').trim())
    .filter((c) => c.length >= 3);

  const normFulls = candidates.map(normTitle).filter(Boolean);
  const normPrefixes = strippedEllipsis.map(normTitle).filter((s) => s.length >= 3);

  let best = null;
  let bestScore = -1;

  for (const s of sessions) {
    const t = normTitle(s.officialTitle || s.title);
    if (!t) continue;
    let score = -1;

    for (const nf of normFulls) {
      if (t === nf) {
        score = Math.max(score, 1000 + t.length);
      } else if (nf.startsWith(t) && t.length >= 4) {
        score = Math.max(score, 800 + t.length);
      }
    }
    for (const np of normPrefixes) {
      if (t === np) {
        score = Math.max(score, 950 + t.length);
      } else if (t.startsWith(np) && np.length >= 4) {
        score = Math.max(score, 700 + np.length);
      } else if (np.startsWith(t) && t.length >= 4) {
        score = Math.max(score, 650 + t.length);
      }
    }

    if (score >= 0) {
      // Break ties by latest user view time
      const tieScore = score * 1e6 + Math.min(999999, Math.floor((s.lastViewMs || s.mtimeMs || 0) / 10000));
      if (tieScore > bestScore) {
        bestScore = tieScore;
        best = s;
      }
    }
  }

  return best ? { session: best } : null;
}

export function getContextTelemetry({
  limitTokens = 512000,
  pinnedConvId = '',
  activeWindowTitle = '',
  panelConvId = '',
} = {}) {
  const safeLimit = Math.max(32000, Math.min(2000000, Number(limitTokens) || 512000));
  const idMap = new Map(); // convId -> { id, tf, annMtime }

  if (existsSync(BRAIN_ROOT)) {
    let entries = [];
    try {
      entries = readdirSync(BRAIN_ROOT, { withFileTypes: true });
    } catch {
      entries = [];
    }
    for (const ent of entries) {
      if (!ent.isDirectory()) continue;
      const tf = join(BRAIN_ROOT, ent.name, '.system_generated', 'logs', 'transcript.jsonl');
      if (!existsSync(tf)) continue;
      try {
        const st = statSync(tf);
        idMap.set(ent.name, { id: ent.name, tf, sortMs: st.mtimeMs });
      } catch {
        // ignore
      }
    }
  }

  if (existsSync(ANNOTATIONS_ROOT)) {
    let annFiles = [];
    try {
      annFiles = readdirSync(ANNOTATIONS_ROOT, { withFileTypes: true });
    } catch {
      annFiles = [];
    }
    for (const ent of annFiles) {
      if (!ent.isFile() || !ent.name.endsWith('.pbtxt')) continue;
      const id = ent.name.slice(0, -6);
      const ann = readAnnotation(id);
      if (!ann) continue;
      const existing = idMap.get(id);
      if (existing) {
        existing.sortMs = Math.max(existing.sortMs, ann.lastViewMs || ann.mtimeMs);
      } else {
        idMap.set(id, { id, tf: '', sortMs: ann.lastViewMs || ann.mtimeMs });
      }
    }
  }

  const candidates = [...idMap.values()].sort((a, b) => b.sortMs - a.sortMs).slice(0, 80);
  const sessions = [];

  for (const c of candidates) {
    const ann = readAnnotation(c.id);
    const parsed = c.tf ? parseTranscriptFile(c.id, c.tf) : null;
    if (!parsed && !ann?.title) continue;

    const officialTitle = ann?.title || '';
    const displayTitle = officialTitle || parsed?.promptTitle || c.id.slice(0, 8);
    const lastViewMs = ann?.lastViewMs || parsed?.mtimeMs || 0;

    sessions.push({
      id: c.id,
      title: displayTitle,
      officialTitle,
      model: parsed?.model || 'Antigravity',
      rawModel: parsed?.rawModel || '',
      contextTokens: parsed?.contextTokens || 0,
      latestInputTokens: parsed?.latestInputTokens || 0,
      latestCacheReadTokens: parsed?.latestCacheReadTokens || 0,
      latestOutputTokens: parsed?.latestOutputTokens || 0,
      cacheHitRate: parsed?.cacheHitRate || 0,
      totalSteps: parsed?.totalSteps || 0,
      userTurns: parsed?.userTurns || 0,
      plannerTurns: parsed?.plannerTurns || 0,
      toolCallCount: parsed?.toolCallCount || 0,
      mtimeMs: parsed?.mtimeMs || 0,
      lastViewMs,
      lastUpdatedAt: parsed?.lastUpdatedAt || '',
    });
  }

  let active = null;
  let matchSource = 'latest';

  if (pinnedConvId) {
    active = sessions.find((s) => s.id === pinnedConvId) || null;
    if (active) matchSource = 'pinned';
  }

  if (!active && activeWindowTitle) {
    const matched = matchSessionByWindowTitle(activeWindowTitle, sessions);
    if (matched?.isBlankHome) {
      matchSource = 'blank';
      active = {
        id: '',
        title: '新对话',
        officialTitle: '',
        model: 'Antigravity',
        rawModel: '',
        contextTokens: 0,
        latestInputTokens: 0,
        latestCacheReadTokens: 0,
        latestOutputTokens: 0,
        cacheHitRate: 0,
        totalSteps: 0,
        userTurns: 0,
        plannerTurns: 0,
        toolCallCount: 0,
        mtimeMs: 0,
        lastViewMs: 0,
        lastUpdatedAt: '',
      };
    } else if (matched?.session) {
      active = matched.session;
      matchSource = 'window_title';
    }
  }

  if (!active && panelConvId) {
    active = sessions.find((s) => s.id === panelConvId) || null;
    if (active) matchSource = 'sidecar_panel';
  }

  if (!active && sessions.length > 0) {
    // Prefer the most recently viewed conversation from annotations
    const sortedByView = [...sessions].sort(
      (a, b) => (b.lastViewMs || b.mtimeMs) - (a.lastViewMs || a.mtimeMs),
    );
    active = sortedByView[0];
    matchSource = 'last_view';
  }

  const contextTokens = active ? active.contextTokens : 0;
  const percent = Math.min(100, Math.round((contextTokens / safeLimit) * 1000) / 10);

  return {
    limitTokens: safeLimit,
    matchSource,
    activeWindowTitle: activeWindowTitle || '',
    active: active
      ? { ...active, limitTokens: safeLimit, percent }
      : {
          id: '',
          title: '等待会话活动',
          model: 'Antigravity',
          rawModel: '',
          contextTokens: 0,
          latestInputTokens: 0,
          latestCacheReadTokens: 0,
          latestOutputTokens: 0,
          cacheHitRate: 0,
          totalSteps: 0,
          userTurns: 0,
          plannerTurns: 0,
          toolCallCount: 0,
          limitTokens: safeLimit,
          percent: 0,
          mtimeMs: 0,
          lastViewMs: 0,
          lastUpdatedAt: '',
        },
    sessions: sessions.slice(0, 35).map((s) => ({
      id: s.id,
      title: s.title,
      model: s.model,
      contextTokens: s.contextTokens,
      percent: Math.min(100, Math.round((s.contextTokens / safeLimit) * 1000) / 10),
      cacheHitRate: s.cacheHitRate,
      totalSteps: s.totalSteps,
      mtimeMs: s.mtimeMs,
      lastViewMs: s.lastViewMs,
    })),
    updatedAt: Date.now(),
  };
}
