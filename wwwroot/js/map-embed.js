(function () {
    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-map-load]');
        if (!button) { return; }
        var holder = button.closest('[data-map-src]');
        if (!holder) { return; }
        var iframe = document.createElement('iframe');
        iframe.src = holder.getAttribute('data-map-src');
        iframe.title = 'Konum haritası';
        iframe.loading = 'lazy';
        iframe.referrerPolicy = 'no-referrer-when-downgrade';
        iframe.allowFullscreen = true;
        holder.replaceWith(iframe);
    });
})();
