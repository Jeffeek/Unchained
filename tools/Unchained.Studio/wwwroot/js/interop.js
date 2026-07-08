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

    // ── PPTX slide shape picker ──────────────────────────────────────────────
    // Delegated pointerdown on the slide host. Because the event fires on the actual
    // painted SVG element, event.target.closest('[data-shape-index]') is a geometry-
    // accurate hit-test (transparent gaps fall through to the background → index -1).
    // Presses on the Blazor selection overlay are ignored so it can handle drag/resize.

    attachShapePicker(host, dotNetRef) {
        if (!host) return;
        this.detachShapePicker(host);

        const handler = (e) => {
            if (e.button !== 0) return;
            if (e.target.closest('.playboard-overlay')) return;
            const g = e.target.closest('[data-shape-index]');
            const index = g ? parseInt(g.getAttribute('data-shape-index'), 10) : -1;
            dotNetRef.invokeMethodAsync('SelectShapeByIndex', index);
        };

        host.__shapePicker = handler;
        host.addEventListener('pointerdown', handler);
    },

    detachShapePicker(host) {
        if (host && host.__shapePicker) {
            host.removeEventListener('pointerdown', host.__shapePicker);
            host.__shapePicker = null;
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

// ── Highcharts rendering ──────────────────────────────────────────────────────
// Renders a Highcharts chart from a JSON options string into the given container.
// Called by ChartVisualizeDialog.razor via IJSRuntime. Retries briefly because the
// dialog's container element may not be in the DOM on the first invoke.
window.__hcRender = function (containerId, optionsJson) {
    const attempt = (retries) => {
        const el = document.getElementById(containerId);
        if (!el) {
            if (retries > 0) setTimeout(() => attempt(retries - 1), 50);
            return;
        }

        if (typeof Highcharts === 'undefined') {
            el.innerHTML = '<pre style="white-space:pre-wrap;font-size:12px;padding:8px;">'
                + 'Highcharts library not loaded.\n\n'
                + optionsJson.replace(/</g, '&lt;') + '</pre>';
            return;
        }

        try {
            const options = JSON.parse(optionsJson);
            Highcharts.chart(containerId, options);
        } catch (err) {
            el.innerHTML = '<pre style="color:#b00;white-space:pre-wrap;font-size:12px;padding:8px;">'
                + 'Failed to render chart: ' + String(err) + '</pre>';
        }
    };

    attempt(10);
};
