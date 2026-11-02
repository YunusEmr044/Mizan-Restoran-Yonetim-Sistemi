(function () {
    var preview = document.getElementById('ryNavPreview');
    if (preview) {
        preview.addEventListener('change', function () {
            var name = preview.dataset.cookie;
            document.cookie = preview.value
                ? name + '=' + encodeURIComponent(preview.value) + '; path=/; SameSite=Lax'
                : name + '=; path=/; max-age=0; SameSite=Lax';
            location.reload();
        });
    }

    document.querySelectorAll('[data-nav-preview-exit]').forEach(function (button) {
        button.addEventListener('click', function () {
            document.cookie = button.dataset.navPreviewExit + '=; path=/; max-age=0; SameSite=Lax';
            location.reload();
        });
    });

    var nav = document.getElementById('ryNav');
    var search = document.getElementById('ryNavSearch');
    if (!nav) { return; }

    var storageKey = 'ry-nav-collapsed';
    var groups = Array.prototype.slice.call(nav.querySelectorAll('.ry-nav-group'));
    var empty = nav.querySelector('.ry-nav-empty');

    // Tercih kaydedilmemişse yalnızca bulunduğun sayfanın grubu açık başlar.
    function readCollapsed() {
        try {
            var saved = localStorage.getItem(storageKey);
            if (saved !== null) { return JSON.parse(saved) || []; }
        } catch (e) { }
        return groups.map(function (g) { return g.dataset.key; });
    }
    function writeCollapsed(keys) {
        try { localStorage.setItem(storageKey, JSON.stringify(keys)); } catch (e) { }
    }
    function setOpen(group, open) {
        group.classList.toggle('ry-collapsed', !open);
        group.querySelector('.ry-nav-section').setAttribute('aria-expanded', open ? 'true' : 'false');
    }
    function applyCollapsed() {
        var collapsed = readCollapsed();
        groups.forEach(function (g) {
            setOpen(g, g.dataset.active === '1' || collapsed.indexOf(g.dataset.key) === -1);
        });
    }

    groups.forEach(function (g) {
        g.querySelector('.ry-nav-section').addEventListener('click', function () {
            var open = g.classList.contains('ry-collapsed');
            setOpen(g, open);
            var collapsed = readCollapsed().filter(function (k) { return k !== g.dataset.key; });
            if (!open) { collapsed.push(g.dataset.key); }
            writeCollapsed(collapsed);
        });
    });
    applyCollapsed();

    if (!search) { return; }

    function norm(s) {
        return s.toLocaleLowerCase('tr').replace(/ı/g, 'i').normalize('NFD').replace(/[̀-ͯ]/g, '');
    }
    groups.forEach(function (g) {
        g.querySelectorAll('.ry-nav-link').forEach(function (a) { a.dataset.search = norm(a.textContent); });
    });

    function filter() {
        var q = norm(search.value.trim());
        var any = false;
        groups.forEach(function (g) {
            var groupHit = false;
            g.querySelectorAll('.ry-nav-link').forEach(function (a) {
                var hit = !q || a.dataset.search.indexOf(q) !== -1;
                a.hidden = !hit;
                groupHit = groupHit || hit;
            });
            g.hidden = !groupHit;
            any = any || groupHit;
            if (q && groupHit) { setOpen(g, true); }
        });
        if (empty) { empty.hidden = any; }
        if (!q) { applyCollapsed(); }
    }

    search.addEventListener('input', filter);
    search.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') {
            var first = nav.querySelector('.ry-nav-group:not([hidden]) .ry-nav-link:not([hidden])');
            if (first && search.value.trim()) { e.preventDefault(); window.location.href = first.href; }
        } else if (e.key === 'Escape') {
            search.value = '';
            filter();
        }
    });

    document.addEventListener('keydown', function (e) {
        if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
            e.preventDefault();
            var sidebar = document.getElementById('rySidebar');
            var toggle = document.getElementById('rySidebarToggle');
            if (sidebar && toggle && getComputedStyle(toggle).display !== 'none' && !sidebar.classList.contains('ry-open')) {
                toggle.click();
            }
            search.focus();
            search.select();
        }
    });
})();
