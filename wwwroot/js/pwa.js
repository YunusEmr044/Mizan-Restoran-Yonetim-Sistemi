(function () {
    // Personel uygulaması: service worker kaydı ve "Uygulamayı yükle" düğmesi (yalnızca panel/personel sayfalarında yüklenir).
    if ('serviceWorker' in navigator) {
        window.addEventListener('load', function () { navigator.serviceWorker.register('/sw.js').catch(function () { }); });
    }

    var installed = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
    var button = document.getElementById('ryInstallApp');
    var iosHint = document.getElementById('ryInstallIos');
    if (installed || !button) { return; }

    var deferred = null;
    window.addEventListener('beforeinstallprompt', function (e) {
        e.preventDefault();
        deferred = e;
        button.hidden = false;
    });
    button.addEventListener('click', function () {
        if (!deferred) { return; }
        deferred.prompt();
        deferred.userChoice.finally(function () { deferred = null; button.hidden = true; });
    });
    window.addEventListener('appinstalled', function () { button.hidden = true; });

    // iPhone/iPad Safari kurulum olayı vermez; ana ekrana ekleme yolu gösterilir.
    var ios = /iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    if (ios && iosHint) { iosHint.hidden = false; }
})();
