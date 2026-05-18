// Claude Switcher Bridge — MV3 service worker.
//
// Maintains a long-lived chrome.runtime.connectNative port to the desktop app
// (via the claude-switcher-host bridge). The desktop app pushes capture/apply
// requests; we round-trip them against chrome.cookies and chrome.tabs.

const HOST_NAME = "com.operative.claudeswitcher";
const COOKIE_DOMAINS = ["claude.ai", "anthropic.com"];
const TAB_RELOAD_PATTERNS = [
  "https://claude.ai/*",
  "https://*.claude.ai/*",
  "https://console.anthropic.com/*"
];

let port = null;
let reconnectDelay = 1000;

function connect() {
  try {
    port = chrome.runtime.connectNative(HOST_NAME);
  } catch (err) {
    console.warn("[claude-switcher] connectNative failed:", err);
    scheduleReconnect();
    return;
  }

  port.onMessage.addListener(handleMessage);
  port.onDisconnect.addListener(() => {
    const err = chrome.runtime.lastError;
    console.warn("[claude-switcher] native port disconnected", err);
    port = null;
    scheduleReconnect();
  });

  send({ type: "hello", extVersion: chrome.runtime.getManifest().version });
  reconnectDelay = 1000;
}

function scheduleReconnect() {
  setTimeout(connect, reconnectDelay);
  reconnectDelay = Math.min(reconnectDelay * 2, 30_000);
}

function send(obj) {
  if (!port) return;
  try { port.postMessage(obj); }
  catch (err) { console.warn("[claude-switcher] postMessage failed", err); }
}

async function handleMessage(msg) {
  if (!msg || typeof msg !== "object") return;
  if (msg.type === "capture") {
    try {
      const cookies = await captureCookies();
      send({ type: "captureResult", id: msg.id, cookies });
    } catch (err) {
      send({ type: "captureResult", id: msg.id, error: String(err) });
    }
  } else if (msg.type === "apply") {
    try {
      await applyCookies(msg.cookies || []);
      await reloadTabs();
      send({ type: "applyResult", id: msg.id, ok: true });
    } catch (err) {
      send({ type: "applyResult", id: msg.id, error: String(err) });
    }
  }
}

async function captureCookies() {
  const all = [];
  for (const domain of COOKIE_DOMAINS) {
    const found = await chrome.cookies.getAll({ domain });
    all.push(...found);
  }
  // Dedupe by (name, domain, path, storeId, partitionKey)
  const seen = new Set();
  return all.filter(c => {
    const key = [c.name, c.domain, c.path, c.storeId, JSON.stringify(c.partitionKey || null)].join("|");
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

async function applyCookies(cookies) {
  // Wipe existing first so stale entries from the old account don't linger.
  for (const domain of COOKIE_DOMAINS) {
    const existing = await chrome.cookies.getAll({ domain });
    for (const c of existing) {
      const url = cookieUrl(c);
      try {
        await chrome.cookies.remove({
          url,
          name: c.name,
          storeId: c.storeId,
          partitionKey: c.partitionKey
        });
      } catch (err) {
        console.warn("[claude-switcher] failed to remove cookie", c.name, err);
      }
    }
  }

  for (const c of cookies) {
    const url = cookieUrl(c);
    const set = {
      url,
      name: c.name,
      value: c.value,
      path: c.path,
      secure: c.secure,
      httpOnly: c.httpOnly,
      sameSite: c.sameSite,
      storeId: c.storeId
    };
    if (!c.hostOnly) set.domain = c.domain;
    if (!c.session && typeof c.expirationDate === "number") set.expirationDate = c.expirationDate;
    if (c.partitionKey) set.partitionKey = c.partitionKey;
    try {
      await chrome.cookies.set(set);
    } catch (err) {
      console.warn("[claude-switcher] failed to set cookie", c.name, err);
    }
  }
}

function cookieUrl(c) {
  const host = c.domain.startsWith(".") ? c.domain.slice(1) : c.domain;
  const scheme = c.secure ? "https" : "http";
  return `${scheme}://${host}${c.path || "/"}`;
}

async function reloadTabs() {
  for (const pattern of TAB_RELOAD_PATTERNS) {
    const tabs = await chrome.tabs.query({ url: pattern });
    for (const t of tabs) {
      try { await chrome.tabs.reload(t.id, { bypassCache: true }); }
      catch (err) { console.warn("[claude-switcher] tab reload failed", err); }
    }
  }
}

// Popup-driven status ping.
chrome.runtime.onMessage.addListener((msg, _sender, sendResponse) => {
  if (msg && msg.type === "__ping") {
    sendResponse({ connected: !!port });
    return true;
  }
  return false;
});

// Boot.
chrome.runtime.onStartup.addListener(connect);
chrome.runtime.onInstalled.addListener(connect);
connect();
