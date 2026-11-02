// Mizan personel uygulaması service worker'ı.
// Sayfalar her zaman sunucudan gelir ve cihaza kaydedilmez (giriş gerektiren içerik önbellekte tutulmaz);
// bağlantı yoksa çevrimdışı sayfası gösterilir. Yalnızca kendi sitemizin statik dosyaları (css/js/img) önbelleğe alınır.
var CACHE = 'mizan-shell-v1';
var SHELL = ['/offline.html', '/img/icon-192.png', '/img/logo-mark.svg', '/favicon.svg'];
var STATIC = /^\/(css|js|img)\//;

self.addEventListener('install', function (event) {
    event.waitUntil(caches.open(CACHE).then(function (cache) { return cache.addAll(SHELL); }).then(function () { return self.skipWaiting(); }));
});

self.addEventListener('activate', function (event) {
    event.waitUntil(caches.keys().then(function (keys) {
        return Promise.all(keys.filter(function (k) { return k !== CACHE; }).map(function (k) { return caches.delete(k); }));
    }).then(function () { return self.clients.claim(); }));
});

// Sürümlü dosyanın (?v=...) yenisi gelince aynı yolun eski sürümleri silinir.
function putVersioned(cache, request, response) {
    var path = new URL(request.url).pathname;
    return cache.keys().then(function (keys) {
        return Promise.all(keys.filter(function (k) { var u = new URL(k.url); return u.pathname === path && k.url !== request.url; })
            .map(function (k) { return cache.delete(k); }));
    }).then(function () { return cache.put(request, response); });
}

self.addEventListener('fetch', function (event) {
    var request = event.request;
    if (request.method !== 'GET') { return; }
    var url = new URL(request.url);
    if (url.origin !== self.location.origin) { return; }

    if (request.mode === 'navigate') {
        event.respondWith(fetch(request).catch(function () { return caches.match('/offline.html'); }));
        return;
    }

    if (!STATIC.test(url.pathname)) { return; }
    event.respondWith(caches.open(CACHE).then(function (cache) {
        return cache.match(request).then(function (hit) {
            if (hit) { return hit; }
            return fetch(request).then(function (response) {
                if (response.ok && response.type === 'basic') { putVersioned(cache, request, response.clone()); }
                return response;
            });
        });
    }));
});
