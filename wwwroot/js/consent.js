(function () {
    const COOKIE = 'mizan_consent';
    const MAX_AGE = 60 * 60 * 24 * 180;
    const banner = document.getElementById('pyConsent');
    const prefs = document.getElementById('pyConsentPrefs');
    const analyticsBox = document.getElementById('pyConsentAnalytics');

    function readConsent() {
        const match = document.cookie.match(new RegExp('(?:^|; )' + COOKIE + '=([^;]*)'));
        return match ? decodeURIComponent(match[1]) : null;
    }

    function writeConsent(granted) {
        const secure = location.protocol === 'https:' ? '; Secure' : '';
        document.cookie = COOKIE + '=v1:analytics=' + (granted ? '1' : '0') +
            '; max-age=' + MAX_AGE + '; path=/; SameSite=Lax' + secure;
    }

    function activateAnalytics() {
        document.querySelectorAll('script[type="text/plain"][data-consent="analytics"]').forEach(function (placeholder) {
            const script = document.createElement('script');
            if (placeholder.dataset.src) {
                script.src = placeholder.dataset.src;
                script.async = true;
            } else {
                script.textContent = placeholder.textContent;
            }
            placeholder.replaceWith(script);
        });
    }

    function setPrefsOpen(open) {
        prefs.hidden = !open;
        banner.querySelector('[data-consent-action="save"]').hidden = !open;
        banner.querySelector('[data-consent-action="prefs"]').hidden = open;
    }

    function syncBodyClass() {
        document.body.classList.toggle('has-consent-banner', !banner.hidden);
    }

    function decide(granted) {
        writeConsent(granted);
        banner.hidden = true;
        syncBodyClass();
        if (granted) {
            activateAnalytics();
        }
    }

    if (readConsent() === 'v1:analytics=1') {
        activateAnalytics();
    }

    if (!banner) {
        return;
    }

    syncBodyClass();

    banner.addEventListener('click', function (event) {
        const action = event.target.closest('[data-consent-action]')?.dataset.consentAction;
        if (action === 'accept') decide(true);
        else if (action === 'deny') decide(false);
        else if (action === 'save') decide(analyticsBox.checked);
        else if (action === 'prefs') setPrefsOpen(true);
    });

    document.querySelectorAll('[data-cookie-settings]').forEach(function (link) {
        link.addEventListener('click', function (event) {
            event.preventDefault();
            analyticsBox.checked = readConsent() === 'v1:analytics=1';
            setPrefsOpen(true);
            banner.hidden = false;
            syncBodyClass();
            banner.querySelector('[data-consent-action="save"]').focus();
        });
    });
})();
