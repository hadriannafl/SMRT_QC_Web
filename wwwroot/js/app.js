/**
 * SmartIQC — app.js
 * Fungsi global: SignalR client, camera utility, toast, confirm delete
 */

/* ═══════════════════════════════════════════════════════════════════════
   1. SIGNALR — Real-time notifications dari server
═══════════════════════════════════════════════════════════════════════ */
(function initSignalR() {
    // signalr.min.js harus ada di wwwroot/lib/signalr/
    if (typeof signalR === 'undefined') return;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/notificationHub')
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    // Event: server mengirim notifikasi baru
    connection.on('ReceiveNotification', (message, level) => {
        showToast(message, level || 'info');
        // Refresh notif counter jika ada komponen Alpine
        const notifComp = document.querySelector('[x-data*="notificationDropdown"]');
        if (notifComp && notifComp._x_dataStack) {
            notifComp._x_dataStack[0].fetchNotifications?.();
        }
    });

    // Event: server minta refresh tabel inspeksi
    connection.on('RefreshInspection', () => {
        if (window.location.pathname.includes('/Inspection')) {
            window.location.reload();
        }
    });

    connection.start()
        .then(() => console.log('SignalR connected'))
        .catch(err => console.warn('SignalR error:', err));
})();

/* ═══════════════════════════════════════════════════════════════════════
   2. TOAST — Menampilkan pesan popup sementara
═══════════════════════════════════════════════════════════════════════ */
/**
 * Tampilkan toast notification.
 * @param {string} message - Pesan yang ditampilkan
 * @param {'info'|'success'|'danger'} level - Tipe toast
 * @param {number} duration - Durasi tampil dalam ms (default 5000)
 */
function showToast(message, level = 'info', duration = 5000) {
    const container = document.getElementById('toast-container');
    if (!container) return;

    const colors = {
        info:    { border: '#1a237e', icon: '🔔', iconColor: '#1a237e' },
        success: { border: '#22c55e', icon: '✅', iconColor: '#16a34a' },
        danger:  { border: '#ef4444', icon: '⚠️', iconColor: '#dc2626' },
    };
    const c = colors[level] || colors.info;

    const toast = document.createElement('div');
    toast.className = 'toast';
    toast.style.borderLeftColor = c.border;
    toast.innerHTML = `
        <span style="font-size:1.1rem">${c.icon}</span>
        <div class="flex-1 text-sm text-gray-700 leading-snug">${message}</div>
        <button onclick="this.parentElement.remove()" class="text-gray-400 hover:text-gray-600 text-lg leading-none ml-1">✕</button>
    `;
    container.appendChild(toast);

    // Auto remove
    setTimeout(() => toast.remove(), duration);
}

/* ═══════════════════════════════════════════════════════════════════════
   3. CONFIRM DELETE — Dialog konfirmasi sebelum hapus
═══════════════════════════════════════════════════════════════════════ */
/**
 * Tampilkan konfirmasi sebelum submit form delete.
 * Pasang di form: onsubmit="return confirmDelete()"
 * @param {string} itemName - Nama item yang akan dihapus
 * @returns {boolean}
 */
function confirmDelete(itemName = 'data ini') {
    return confirm(`Apakah Anda yakin ingin menghapus ${itemName}?\nTindakan ini tidak dapat dibatalkan.`);
}

/* ═══════════════════════════════════════════════════════════════════════
   4. CAMERA — Akses webcam browser untuk foto NCN
═══════════════════════════════════════════════════════════════════════ */
const Camera = {
    stream: null,

    /**
     * Mulai stream kamera ke elemen <video>.
     * @param {string} videoId - id elemen <video>
     */
    async start(videoId = 'camera-preview') {
        try {
            this.stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: 'environment', width: { ideal: 1280 }, height: { ideal: 720 } },
                audio: false
            });
            const video = document.getElementById(videoId);
            if (video) {
                video.srcObject = this.stream;
                await video.play();
            }
            return true;
        } catch (err) {
            console.error('Kamera tidak dapat diakses:', err);
            showToast('Tidak dapat mengakses kamera. Pastikan izin kamera diberikan.', 'danger');
            return false;
        }
    },

    /**
     * Ambil foto dari video dan simpan ke <img> + input hidden (base64).
     * @param {string} videoId - id elemen <video>
     * @param {string} canvasId - id elemen <canvas>
     * @param {string} previewImgId - id elemen <img> untuk preview
     * @param {string} inputId - id input hidden untuk kirim ke server
     */
    capture(videoId = 'camera-preview', canvasId = 'camera-canvas',
            previewImgId = 'photo-preview', inputId = 'photo-data') {
        const video  = document.getElementById(videoId);
        const canvas = document.getElementById(canvasId);
        const img    = document.getElementById(previewImgId);
        const input  = document.getElementById(inputId);
        if (!video || !canvas) return;

        canvas.width  = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext('2d').drawImage(video, 0, 0);

        const dataUrl = canvas.toDataURL('image/jpeg', 0.85);
        if (img) { img.src = dataUrl; img.style.display = 'block'; }
        if (input) input.value = dataUrl;

        showToast('Foto berhasil diambil!', 'success', 2000);
    },

    /** Hentikan semua track kamera */
    stop() {
        this.stream?.getTracks().forEach(t => t.stop());
        this.stream = null;
    }
};

/* ═══════════════════════════════════════════════════════════════════════
   5. TABLE SEARCH — Filter baris tabel secara client-side
═══════════════════════════════════════════════════════════════════════ */
/**
 * Filter baris tabel berdasarkan teks pencarian.
 * Pasang di input: oninput="tableSearch(this, 'table-id')"
 * @param {HTMLInputElement} input
 * @param {string} tableId
 */
function tableSearch(input, tableId) {
    const q = input.value.toLowerCase();
    const rows = document.querySelectorAll(`#${tableId} tbody tr`);
    rows.forEach(row => {
        row.style.display = row.textContent.toLowerCase().includes(q) ? '' : 'none';
    });
}

/* ═══════════════════════════════════════════════════════════════════════
   6. PRINT — Cetak halaman report
═══════════════════════════════════════════════════════════════════════ */
function printReport(sectionId) {
    const section = document.getElementById(sectionId);
    if (!section) { window.print(); return; }

    const original = document.body.innerHTML;
    document.body.innerHTML = section.innerHTML;
    window.print();
    document.body.innerHTML = original;
    window.location.reload();
}
