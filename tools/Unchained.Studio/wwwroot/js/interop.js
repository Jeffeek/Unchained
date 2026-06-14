// ── Studio browser interop ───────────────────────────────────────────────────
// PDF rendering (both Unchained and the Pdfium reference) happens server-side; no
// browser PDF viewer or PDF.js is involved. This file only holds small DOM/file helpers.

window.unchainedStudio = {

    // ── File download ────────────────────────────────────────────────────────

    downloadFile(base64, fileName, mimeType) {
        const bytes  = atob(base64);
        const buffer = new Uint8Array(bytes.length);
        for (let i = 0; i < bytes.length; i++) buffer[i] = bytes.charCodeAt(i);
        const blob = new Blob([buffer], { type: mimeType });
        const url  = URL.createObjectURL(blob);
        const a    = document.createElement('a');
        a.href     = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },

    // ── Misc interop ─────────────────────────────────────────────────────────

    clickElement(elementId) {
        const el = document.getElementById(elementId);
        if (el) el.click();
    },

    async copyToClipboard(text) {
        try {
            await navigator.clipboard.writeText(text);
        } catch {
            const ta          = document.createElement('textarea');
            ta.value          = text;
            ta.style.position = 'fixed';
            ta.style.opacity  = '0';
            document.body.appendChild(ta);
            ta.focus();
            ta.select();
            document.execCommand('copy');
            document.body.removeChild(ta);
        }
    },

    // ── Local storage (settings persistence) ─────────────────────────────────

    getLocalStorage(key) {
        try { return window.localStorage.getItem(key); }
        catch { return null; }
    },

    setLocalStorage(key, value) {
        try { window.localStorage.setItem(key, value); }
        catch { /* storage unavailable (private mode, quota) — ignore */ }
    },
};
