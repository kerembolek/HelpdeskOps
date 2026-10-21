// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Live network status: fills every .ping-dot[data-device-id] with one request to the URL in data-ping-url
function refreshPingDots() {
    const dots = [...document.querySelectorAll('.ping-dot[data-device-id]')];
    if (dots.length === 0) return;

    dots.forEach(dot => {
        dot.classList.remove('online', 'offline');
        dot.title = 'Kontrol ediliyor…';
    });

    const params = new URLSearchParams();
    dots.forEach(dot => params.append('ids', dot.dataset.deviceId));

    fetch(dots[0].dataset.pingUrl + '?' + params, { headers: { 'Accept': 'application/json' } })
        .then(response => response.ok ? response.json() : Promise.reject(response.status))
        .then(status => dots.forEach(dot => {
            const online = status[dot.dataset.deviceId] === true;
            dot.classList.add(online ? 'online' : 'offline');
            dot.title = online ? 'Çevrimiçi' : 'Yanıt yok';
        }))
        .catch(() => dots.forEach(dot => dot.title = 'Durum alınamadı'));
}

document.addEventListener('DOMContentLoaded', () => {
    refreshPingDots();
    document.querySelectorAll('[data-ping-refresh]').forEach(button => button.addEventListener('click', refreshPingDots));
});
