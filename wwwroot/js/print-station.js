(function () {
    // Yazıcı istasyonu: "kitchen-ticket" sinyali gelince fişi görünmez çerçevede basar, basıldıktan sonra sunucuya onay gönderir.
    var status = document.getElementById('ryStationStatus');
    var log = document.getElementById('ryStationLog');
    var form = document.getElementById('ryStationAck');
    if (!status || !log || !form) { return; }

    var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');

    function setStatus(ok, text) {
        status.className = 'alert d-flex align-items-center gap-2 ' + (ok ? 'alert-success' : 'alert-danger');
        status.innerHTML = '';
        var icon = document.createElement('i');
        icon.className = 'bi ' + (ok ? 'bi-check-circle-fill' : 'bi-exclamation-triangle-fill');
        var span = document.createElement('span');
        span.textContent = text;
        status.appendChild(icon);
        status.appendChild(span);
    }

    function addLog(text) {
        var empty = log.querySelector('[data-empty]');
        if (empty) { empty.remove(); }
        var item = document.createElement('li');
        item.className = 'list-group-item small';
        item.textContent = new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }) + ' · ' + text;
        log.insertBefore(item, log.firstChild);
        while (log.children.length > 20) { log.removeChild(log.lastChild); }
    }

    function print(token) {
        var frame = document.createElement('iframe');
        frame.title = 'Mutfak fişi';
        frame.setAttribute('aria-hidden', 'true');
        frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;visibility:hidden';
        frame.src = '/Yazici/Fis?token=' + encodeURIComponent(token);
        frame.addEventListener('load', function () {
            addLog(frame.contentDocument && frame.contentDocument.title ? frame.contentDocument.title : 'Fiş');
        });
        document.body.appendChild(frame);
        setTimeout(function () { frame.remove(); }, 60000);
    }

    function acknowledge(token) {
        var body = new URLSearchParams();
        body.set('token', token);
        if (tokenInput) { body.set('__RequestVerificationToken', tokenInput.value); }
        fetch(form.action, { method: 'POST', body: body, credentials: 'same-origin' });
    }

    window.addEventListener('message', function (e) {
        if (e.origin !== location.origin || !e.data || e.data.type !== 'ry-ticket-printed') { return; }
        acknowledge(e.data.token);
    });

    document.addEventListener('ry:realtime', function (e) {
        var detail = e.detail || {};
        if (detail.type === 'kitchen-ticket' && detail.message) { print(detail.message); }
    });

    document.addEventListener('ry:realtime-state', function (e) {
        var connected = e.detail && e.detail.connected;
        setStatus(!!connected, connected ? 'Bağlı · fişler otomatik basılacak' : 'Bağlantı yok · fişler basılamaz');
    });
})();
