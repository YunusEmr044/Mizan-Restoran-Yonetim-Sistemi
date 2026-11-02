(function () {
    // data-print-url taşıyan düğme, adisyon yazdırma sayfasını görünmez bir çerçevede açar; sayfa kendini yazdırır, kullanıcı ekrandan ayrılmaz.
    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-print-url]');
        if (!button) { return; }

        var old = document.getElementById('ryPrintFrame');
        if (old) { old.remove(); }

        var frame = document.createElement('iframe');
        frame.id = 'ryPrintFrame';
        frame.title = 'Adisyon yazdırma';
        frame.setAttribute('aria-hidden', 'true');
        frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;visibility:hidden';
        frame.src = button.getAttribute('data-print-url');
        document.body.appendChild(frame);
    });
})();
