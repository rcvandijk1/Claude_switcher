// Ask the service worker whether its native port is alive.
async function refresh() {
  const sw = await navigator.serviceWorker?.ready;
  // Service workers in MV3 don't expose a public state — we use a ping.
  chrome.runtime.sendMessage({ type: "__ping" }, (resp) => {
    const ok = !!(resp && resp.connected);
    document.getElementById("status").textContent = ok ? "Connected to desktop app" : "Desktop app not reachable";
    document.getElementById("dot").className = "dot " + (ok ? "ok" : "bad");
  });
}

refresh();
setInterval(refresh, 1500);
