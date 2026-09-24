// Gerçek zamanlı mutfak/bar/personel ekranları + bildirim zili için ince bir SignalR
// istemcisi. Sunucu tarafı: Hubs/OpsHub.cs, Services/RealtimeNotifier.cs.
//
// Kullanım: bir sayfa <body data-ry-realtime="mutfak"> gibi hangi gruba ait olduğunu
// belirtir (birden çok grup boşlukla ayrılır, örn. "mutfak notifications"). Üst layout
// (_Layout.cshtml) her girişli sayfada otomatik olarak "notifications" grubuna da
// katılır (bildirim zili için). Bu dosya:
//  1) /hubs/ops'a bağlanır, ilgili grup(lar)a katılır,
//  2) bir "update" mesajı geldiğinde kısa bir bip sesi çalar (Web Audio API ile
//     sentezlenmiş - harici ses dosyasına gerek yok),
//  3) `ry:realtime` adında bir DOM custom event fırlatır ki sayfalar kendi tepkisini
//     verebilsin (ör. bildirim zili sayacını hemen yenile, mutfak ekranı kısa bir
//     gecikmeyle kendini yenilesin).
(function () {
    if (typeof signalR === 'undefined') {
        return; // CDN'den signalr.min.js yüklenemediyse gerçek zamanlı katman sessizce devre dışı kalır - sayfalar zaten normal yüklemeyle çalışmaya devam eder.
    }

    var body = document.body;
    var groups = (body.getAttribute('data-ry-realtime') || '').split(/\s+/).filter(Boolean);
    if (!groups.length) {
        return;
    }

    function beep() {
        try {
            var Ctx = window.AudioContext || window.webkitAudioContext;
            if (!Ctx) { return; }
            var ctx = new Ctx();
            var now = ctx.currentTime;
            [880, 1180].forEach(function (freq, i) {
                var osc = ctx.createOscillator();
                var gain = ctx.createGain();
                osc.type = 'sine';
                osc.frequency.value = freq;
                var start = now + i * 0.14;
                gain.gain.setValueAtTime(0, start);
                gain.gain.linearRampToValueAtTime(0.18, start + 0.02);
                gain.gain.exponentialRampToValueAtTime(0.001, start + 0.22);
                osc.connect(gain).connect(ctx.destination);
                osc.start(start);
                osc.stop(start + 0.24);
            });
            setTimeout(function () { ctx.close(); }, 700);
        } catch (e) { /* sessizce yoksay - ses opsiyonel bir katman */ }
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/ops')
        .withAutomaticReconnect()
        .build();

    connection.on('update', function (payload) {
        beep();
        document.dispatchEvent(new CustomEvent('ry:realtime', { detail: payload || {} }));
    });

    connection.start()
        .then(function () { return connection.invoke('JoinGroups', groups); })
        .catch(function () { /* bağlantı kurulamazsa sayfa normal (polling/elle yenileme) davranışına döner */ });
})();
