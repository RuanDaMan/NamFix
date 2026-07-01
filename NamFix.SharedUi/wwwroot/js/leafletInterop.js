// Leaflet.js interop for NamFix. Loaded as an ES module by LeafletMapService.
// Leaflet's own script/CSS is included via CDN in the host index.html.

const maps = {};

function ensureMap(elementId, lat, lng, zoom) {
    if (maps[elementId]) {
        const map = maps[elementId];
        const v = map._namfixView;
        // Only move the view when the *requested* center/zoom actually changes. A plain
        // re-render (e.g. after dropping a pin) must not undo the user's manual zoom/pan.
        if (!v || v.lat !== lat || v.lng !== lng || v.zoom !== zoom) {
            map.setView([lat, lng], zoom);
            map._namfixView = { lat, lng, zoom };
        }
        return map;
    }
    const map = L.map(elementId).setView([lat, lng], zoom);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '© OpenStreetMap contributors'
    }).addTo(map);
    maps[elementId] = map;
    map._namfixMarkers = [];
    map._namfixView = { lat, lng, zoom };
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
    // Report the click; the marker itself is drawn by render() from the .NET-side state,
    // so there's a single pin that also reflects "use my current location".
    map.on('click', async (e) => {
        await dotNetRef.invokeMethodAsync('OnPinned', e.latlng.lat, e.latlng.lng);
    });
}

export function getCurrentLocation() {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject("Location isn't supported by this browser.");
            return;
        }
        navigator.geolocation.getCurrentPosition(
            pos => resolve([pos.coords.latitude, pos.coords.longitude]),
            err => {
                // 1 = permission denied, 2 = position unavailable, 3 = timeout
                const msg =
                    err.code === 1 ? "Location access was blocked. Allow location for this site and try again." :
                    err.code === 3 ? "Getting your location timed out. Please try again." :
                    "Your location is currently unavailable.";
                reject(msg);
            },
            { enableHighAccuracy: true, timeout: 8000 }
        );
    });
}
