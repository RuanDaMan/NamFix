// Leaflet.js interop for NamFix. Loaded as an ES module by LeafletMapService.
// Leaflet's own script/CSS is included via CDN in the host index.html.

const maps = {};

function ensureMap(elementId, lat, lng, zoom) {
    if (maps[elementId]) {
        maps[elementId].setView([lat, lng], zoom);
        return maps[elementId];
    }
    const map = L.map(elementId).setView([lat, lng], zoom);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '© OpenStreetMap contributors'
    }).addTo(map);
    maps[elementId] = map;
    map._namfixMarkers = [];
    return map;
}

export function render(elementId, lat, lng, zoom, markers) {
    const map = ensureMap(elementId, lat, lng, zoom);

    // Clear previous markers before re-rendering.
    (map._namfixMarkers || []).forEach(m => map.removeLayer(m));
    map._namfixMarkers = [];

    (markers || []).forEach(m => {
        const marker = L.marker([m.lat, m.lng]).addTo(map);
        const popupHtml = m.url
            ? `<strong>${m.title}</strong><br/><a href="${m.url}">View profile</a>`
            : `<strong>${m.title}</strong>${m.popup ? '<br/>' + m.popup : ''}`;
        marker.bindPopup(popupHtml);
        map._namfixMarkers.push(marker);
    });

    // Leaflet needs a nudge when rendered inside a freshly shown container.
    setTimeout(() => map.invalidateSize(), 100);
}

export function enablePinMode(elementId, dotNetRef) {
    const map = maps[elementId];
    if (!map) return;
    let pin = null;
    map.on('click', async (e) => {
        if (pin) map.removeLayer(pin);
        pin = L.marker(e.latlng).addTo(map);
        await dotNetRef.invokeMethodAsync('OnPinned', e.latlng.lat, e.latlng.lng);
    });
}

export function getCurrentLocation() {
    return new Promise((resolve) => {
        if (!navigator.geolocation) { resolve(null); return; }
        navigator.geolocation.getCurrentPosition(
            pos => resolve([pos.coords.latitude, pos.coords.longitude]),
            () => resolve(null),
            { enableHighAccuracy: true, timeout: 8000 }
        );
    });
}
