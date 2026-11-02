(function () {
    // Gönderilen formun düğmesini kilitler ve gösterge ekler; çift gönderimi de engeller.
    // ry-confirm.js onay penceresi açan formlarda submit'i capture aşamasında durdurur; onaydan sonra yeni bir submit gelir.
    function reset(button) {
        button.disabled = false;
        button.removeAttribute('aria-busy');
        var spinner = button.querySelector('.ry-busy-spinner');
        if (spinner) { spinner.remove(); }
        var proxy = button.form && button.form.querySelector('input[data-busy-proxy]');
        if (proxy) { proxy.remove(); }
    }

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (e.defaultPrevented || !(form instanceof HTMLFormElement) || form.hasAttribute('data-no-busy')) { return; }
        if (form.getAttribute('target') === '_blank') { return; }

        var button = e.submitter || form.querySelector('[type="submit"]');
        if (!button || button.disabled) { return; }

        if (button.name) {
            var proxy = document.createElement('input');
            proxy.type = 'hidden';
            proxy.name = button.name;
            proxy.value = button.value;
            proxy.setAttribute('data-busy-proxy', '');
            form.appendChild(proxy);
        }

        // Düğme hemen kilitlenirse bazı tarayıcılar gönderimi iptal eder; bu yüzden bir sonraki adımda kilitlenir.
        setTimeout(function () {
            button.disabled = true;
            button.setAttribute('aria-busy', 'true');
            if (!button.querySelector('.ry-busy-spinner') && button.tagName === 'BUTTON') {
                var spinner = document.createElement('span');
                spinner.className = 'spinner-border spinner-border-sm me-1 ry-busy-spinner';
                spinner.setAttribute('aria-hidden', 'true');
                button.prepend(spinner);
            }
        }, 0);
    });

    window.addEventListener('pageshow', function (e) {
        if (!e.persisted) { return; }
        document.querySelectorAll('[aria-busy="true"]').forEach(reset);
    });
})();
