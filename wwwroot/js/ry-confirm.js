// Tarayıcının kendi confirm() kutusu yerine panelin tasarımına uygun onay penceresi.
// Kullanım: <form data-confirm="Mesaj" [data-confirm-title="Başlık"] [data-confirm-ok="Buton"] [data-confirm-danger]>
// JS'ten: ryConfirm("Mesaj", { title, okText, danger }).then(function (ok) { ... });
(function () {
    'use strict';

    var modalEl = null;
    var modal = null;
    var resolver = null;

    function build() {
        modalEl = document.createElement('div');
        modalEl.className = 'modal fade ry-confirm';
        modalEl.tabIndex = -1;
        modalEl.setAttribute('aria-hidden', 'true');
        modalEl.innerHTML =
            '<div class="modal-dialog modal-dialog-centered">' +
            '  <div class="modal-content">' +
            '    <div class="modal-body">' +
            '      <div class="ry-confirm-icon"><i class="bi"></i></div>' +
            '      <h5 class="ry-confirm-title"></h5>' +
            '      <p class="ry-confirm-message"></p>' +
            '    </div>' +
            '    <div class="modal-footer">' +
            '      <button type="button" class="btn btn-outline-secondary" data-ry-cancel>Vazgeç</button>' +
            '      <button type="button" class="btn" data-ry-ok></button>' +
            '    </div>' +
            '  </div>' +
            '</div>';
        document.body.appendChild(modalEl);
        modal = new bootstrap.Modal(modalEl);

        modalEl.querySelector('[data-ry-ok]').addEventListener('click', function () { finish(true); });
        modalEl.querySelector('[data-ry-cancel]').addEventListener('click', function () { finish(false); });
        modalEl.addEventListener('hidden.bs.modal', function () { finish(false); });
        modalEl.addEventListener('shown.bs.modal', function () {
            var focusTarget = modalEl.classList.contains('ry-confirm-danger')
                ? modalEl.querySelector('[data-ry-cancel]')
                : modalEl.querySelector('[data-ry-ok]');
            focusTarget.focus();
        });
    }

    function finish(result) {
        if (!resolver) { return; }
        var resolve = resolver;
        resolver = null;
        modal.hide();
        resolve(result);
    }

    window.ryConfirm = function (message, options) {
        options = options || {};
        if (typeof bootstrap === 'undefined' || !bootstrap.Modal) {
            return Promise.resolve(window.confirm(message));
        }
        if (!modalEl) { build(); }
        if (resolver) { resolver(false); resolver = null; }

        var danger = !!options.danger;
        modalEl.classList.toggle('ry-confirm-danger', danger);
        modalEl.querySelector('.ry-confirm-icon i').className = 'bi ' + (danger ? 'bi-exclamation-triangle' : 'bi-question-circle');
        modalEl.querySelector('.ry-confirm-title').textContent = options.title || 'Emin misiniz?';
        modalEl.querySelector('.ry-confirm-message').textContent = message || '';
        var okButton = modalEl.querySelector('[data-ry-ok]');
        okButton.textContent = options.okText || (danger ? 'Evet, devam et' : 'Onayla');
        okButton.className = 'btn ' + (danger ? 'btn-danger' : 'btn-primary');

        return new Promise(function (resolve) {
            resolver = resolve;
            modal.show();
        });
    };

    function optionsFrom(el) {
        return {
            title: el.getAttribute('data-confirm-title'),
            okText: el.getAttribute('data-confirm-ok'),
            danger: el.hasAttribute('data-confirm-danger')
        };
    }

    // Onaylanan formu tekrar gönderirken bu işaret sayesinde pencere ikinci kez açılmaz.
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-confirm')) { return; }
        if (form.__ryConfirmed) {
            form.__ryConfirmed = false;
            return;
        }

        e.preventDefault();
        var submitter = e.submitter || null;
        window.ryConfirm(form.getAttribute('data-confirm'), optionsFrom(form)).then(function (ok) {
            if (!ok) { return; }
            form.__ryConfirmed = true;
            if (typeof form.requestSubmit === 'function') {
                form.requestSubmit(submitter && submitter.form === form ? submitter : undefined);
            } else {
                form.submit();
            }
        });
    }, true);

    // Form dışındaki link/butonlar için (ör. <a href="..." data-confirm="...">).
    document.addEventListener('click', function (e) {
        var el = e.target.closest ? e.target.closest('a[data-confirm], button[data-confirm]') : null;
        if (!el || el.form) { return; }
        if (el.__ryConfirmed) {
            el.__ryConfirmed = false;
            return;
        }

        e.preventDefault();
        window.ryConfirm(el.getAttribute('data-confirm'), optionsFrom(el)).then(function (ok) {
            if (!ok) { return; }
            el.__ryConfirmed = true;
            el.click();
        });
    }, true);
})();
