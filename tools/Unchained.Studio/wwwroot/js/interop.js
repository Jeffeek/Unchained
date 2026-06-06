// ── Blob URL helpers ─────────────────────────────────────────────────────────
// Used by the live compare panel in PreviewPanel: the <object type="application/pdf">
// element receives a blob: URL and renders using Chrome's built-in Pdfium viewer.
// This gives a perfect visual reference with zero server cost.
//
// For batch/export comparison renders, the server uses PDFiumCore directly — no JS
// needed, same engine, no network, no timeouts.

window.unchainedStudio = {

    createPdfBlobUrl(base64) {
        const bytes  = atob(base64);
        const buffer = new Uint8Array(bytes.length);
        for (let i = 0; i < bytes.length; i++) buffer[i] = bytes.charCodeAt(i);
        const blob = new Blob([buffer], { type: 'application/pdf' });
        return URL.createObjectURL(blob);
    },

    revokeBlobUrl(url) {
        try { URL.revokeObjectURL(url); } catch { /* ignore */ }
    },

    // ── Folder-save (File System Access API) ─────────────────────────────────

    isFolderSaveSupported() {
        return typeof window.showDirectoryPicker === 'function';
    },

    async saveFilesToFolder(files) {
        const dirHandle = await window.showDirectoryPicker({ mode: 'readwrite' });
        for (const file of files) {
            const fh       = await dirHandle.getFileHandle(file.name, { create: true });
            const writable = await fh.createWritable();
            const bytes    = Uint8Array.from(atob(file.base64), c => c.charCodeAt(0));
            await writable.write(bytes);
            await writable.close();
        }
    },

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
};
