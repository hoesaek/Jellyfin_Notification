/**
 * Jellyfin Notification — Script Client v2.3.0
 * Injecté dans index.html par Plugin.cs.
 *
 * Panneau de notifications, modale de détail, suppression côté client.
 * Design aligné sur le language system natif Jellyfin.
 */
(function () {
    'use strict';

    const POLL_INTERVAL = 60_000;

    function getServerBase() {
        try {
            if (window.ApiClient && typeof window.ApiClient.serverAddress === 'function') {
                return window.ApiClient.serverAddress().replace(/\/$/, '');
            }
        } catch (_) {}
        return '';
    }

    // ── API ──────────────────────────────────────────────────────────
    async function apiRequest(path, method = 'GET') {
        let url = path;
        try {
            if (window.ApiClient && typeof window.ApiClient.getUrl === 'function') {
                url = window.ApiClient.getUrl(path.replace(/^\//, ''));
            }
        } catch (_) {}

        try {
            if (!window.ApiClient || typeof window.ApiClient.ajax !== 'function') return null;
            const opts = { url, type: method };
            if (method === 'GET') opts.dataType = 'json';
            const result = await window.ApiClient.ajax(opts);
            return result ?? null;
        } catch (err) {
            console.error(`[JellyNotif] ${method} ${url}`, err);
            return null;
        }
    }

    async function fetchNotifications() {
        const data = await apiRequest('/Notification/List');
        return Array.isArray(data) ? data : [];
    }

    async function markAsRead(id) {
        await apiRequest(`/Notification/MarkAsRead/${id}`, 'POST');
    }

    // ── SVG ──────────────────────────────────────────────────────────
    const SVG_BELL = `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20"
        fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"
        stroke-linejoin="round" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
        <path d="M13.73 21a2 2 0 0 1-3.46 0"/>
    </svg>`;

    const TYPE_COLOR = {
        Info:    '#00a4dc',
        Warning: '#c8890e',
        Alert:   '#c03030'
    };

    // ── Styles ───────────────────────────────────────────────────────
    function injectStyles() {
        if (document.getElementById('vn-styles')) return;
        const s = document.createElement('style');
        s.id = 'vn-styles';
        s.textContent = `
            #vn-bell {
                position: relative; background: transparent; border: none;
                color: rgba(255,255,255,.5); cursor: pointer; padding: 8px;
                border-radius: 50%; display: flex; align-items: center;
                justify-content: center; transition: color .15s; margin: 0 4px;
            }
            #vn-bell:hover { color: rgba(255,255,255,.85); }

            #vn-badge {
                position: absolute; top: 2px; right: 2px;
                min-width: 15px; height: 15px;
                background: #c03030; color: #fff;
                font-size: 9px; font-weight: 700; border-radius: 8px;
                display: flex; align-items: center; justify-content: center;
                padding: 0 3px; pointer-events: none;
                opacity: 0; transform: scale(0);
                transition: opacity .2s, transform .2s;
            }
            #vn-badge.on { opacity: 1; transform: scale(1); }

            /* Panel */
            #vn-panel {
                position: fixed; top: 56px; right: 12px;
                width: 350px; max-height: 480px;
                background: #181818; border: 1px solid rgba(255,255,255,.06);
                border-radius: 6px;
                box-shadow: 0 8px 32px rgba(0,0,0,.5);
                display: flex; flex-direction: column; overflow: hidden;
                z-index: 9990;
                opacity: 0; transform: translateY(-6px);
                pointer-events: none;
                transition: opacity .15s, transform .15s;
            }
            #vn-panel.open { opacity: 1; transform: translateY(0); pointer-events: auto; }

            #vn-panel-head {
                display: flex; align-items: center; justify-content: space-between;
                padding: 12px 14px 10px; border-bottom: 1px solid rgba(255,255,255,.04);
            }
            #vn-panel-head span {
                font-size: .82rem; font-weight: 600; color: rgba(255,255,255,.7);
            }
            .vn-head-actions { display: flex; gap: 4px; }
            .vn-head-btn {
                font-size: .68rem; padding: 3px 8px; border-radius: 3px;
                border: 1px solid rgba(255,255,255,.06);
                background: transparent; color: rgba(255,255,255,.35);
                cursor: pointer; transition: color .15s, border-color .15s;
            }
            .vn-head-btn:hover { color: rgba(255,255,255,.6); border-color: rgba(255,255,255,.12); }

            #vn-list { overflow-y: auto; flex: 1; scrollbar-width: thin; scrollbar-color: rgba(255,255,255,.08) transparent; }

            .vn-item {
                display: flex; gap: 10px; padding: 11px 14px;
                border-bottom: 1px solid rgba(255,255,255,.025);
                cursor: pointer; position: relative;
                transition: background .1s, opacity .15s, transform .15s;
            }
            .vn-item:hover { background: rgba(255,255,255,.02); }
            .vn-item:last-child { border-bottom: none; }

            .vn-item-bar {
                width: 2px; border-radius: 1px; flex-shrink: 0; align-self: stretch;
                background: transparent;
            }
            .vn-item.unread .vn-item-bar { background: var(--vn-color); }

            .vn-item-body { flex: 1; min-width: 0; }
            .vn-item-row1 { display: flex; align-items: center; gap: 6px; }
            .vn-item-title {
                font-size: .8rem; font-weight: 500; color: rgba(255,255,255,.55);
                flex: 1; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
            }
            .vn-item.unread .vn-item-title { color: rgba(255,255,255,.85); font-weight: 600; }
            .vn-item-time { font-size: .68rem; color: rgba(255,255,255,.2); white-space: nowrap; }
            .vn-item-preview {
                font-size: .75rem; color: rgba(255,255,255,.3); margin-top: 2px;
                white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
            }

            .vn-item-x {
                position: absolute; top: 6px; right: 8px;
                width: 18px; height: 18px; border-radius: 3px;
                background: transparent; border: none;
                color: rgba(255,255,255,.15); font-size: 10px; cursor: pointer;
                display: flex; align-items: center; justify-content: center;
                opacity: 0; transition: opacity .1s, color .1s, background .1s;
            }
            .vn-item:hover .vn-item-x { opacity: 1; }
            .vn-item-x:hover { color: #c03030; background: rgba(192,48,48,.08); }

            #vn-empty {
                text-align: center; padding: 32px 16px;
                color: rgba(255,255,255,.2); font-size: .8rem;
            }

            /* Modal */
            #vn-overlay {
                position: fixed; inset: 0; background: rgba(0,0,0,.65);
                z-index: 9999; display: flex; align-items: center; justify-content: center;
                opacity: 0; pointer-events: none; transition: opacity .15s;
            }
            #vn-overlay.open { opacity: 1; pointer-events: auto; }

            #vn-modal {
                background: #1a1a1a; border: 1px solid rgba(255,255,255,.06);
                border-radius: 8px; width: 90%; max-width: 500px; max-height: 75vh;
                display: flex; flex-direction: column; overflow: hidden;
                transform: translateY(10px); transition: transform .15s;
                box-shadow: 0 12px 40px rgba(0,0,0,.5);
            }
            #vn-overlay.open #vn-modal { transform: translateY(0); }

            .vn-m-head {
                display: flex; align-items: flex-start; gap: 12px;
                padding: 18px 18px 0;
            }
            .vn-m-type {
                display: inline-block; width: 4px; border-radius: 2px;
                align-self: stretch; flex-shrink: 0;
            }
            .vn-m-title-wrap { flex: 1; }
            .vn-m-title {
                font-size: 1.05rem; font-weight: 600; color: rgba(255,255,255,.85);
                margin: 0; line-height: 1.4;
            }
            .vn-m-meta {
                font-size: .72rem; color: rgba(255,255,255,.25); margin-top: 4px;
            }
            .vn-m-close {
                background: transparent; border: 1px solid rgba(255,255,255,.06);
                width: 26px; height: 26px; border-radius: 4px;
                color: rgba(255,255,255,.3); font-size: 12px; cursor: pointer;
                display: flex; align-items: center; justify-content: center;
                flex-shrink: 0; transition: color .15s, border-color .15s;
            }
            .vn-m-close:hover { color: rgba(255,255,255,.7); border-color: rgba(255,255,255,.12); }

            .vn-m-body {
                padding: 16px 18px; overflow-y: auto; flex: 1;
                color: rgba(255,255,255,.55); font-size: .88rem; line-height: 1.65;
                white-space: pre-wrap; word-break: break-word;
            }

            .vn-m-foot {
                padding: 12px 18px; border-top: 1px solid rgba(255,255,255,.04);
                display: flex; justify-content: flex-end; gap: 6px;
            }
            .vn-m-btn {
                padding: 7px 18px; border-radius: 4px;
                font-size: .8rem; font-weight: 600; cursor: pointer;
                transition: background .15s, color .15s;
            }
            .vn-m-btn-del {
                background: transparent; border: 1px solid rgba(255,255,255,.06);
                color: rgba(255,255,255,.35);
            }
            .vn-m-btn-del:hover { border-color: rgba(192,48,48,.2); color: #c03030; }
            .vn-m-btn-ok {
                background: #00a4dc; border: none; color: #fff;
            }
            .vn-m-btn-ok:hover { background: #0090c5; }

            /* Sidebar */
            #vn-sidebar-link {
                display: flex; align-items: center; gap: 10px;
                padding: 10px 16px; color: rgba(255,255,255,.55);
                text-decoration: none; font-size: .9rem;
                border-radius: 4px; transition: background .1s, color .1s;
            }
            #vn-sidebar-link:hover { background: rgba(255,255,255,.04); color: rgba(255,255,255,.8); }
        `;
        document.head.appendChild(s);
    }

    // ── Build UI ─────────────────────────────────────────────────────
    function buildUI() {
        if (document.getElementById('vn-bell')) return;

        const bell = document.createElement('button');
        bell.id = 'vn-bell';
        bell.title = 'Notifications';
        bell.setAttribute('aria-haspopup', 'true');
        bell.setAttribute('aria-expanded', 'false');
        bell.innerHTML = `${SVG_BELL}<span id="vn-badge"></span>`;

        function inject() {
            const sels = ['.headerRight','.skinHeader-withBackground .headerButtons','.headerButtons','.viewManagerContainer .skinHeader','header .flex'];
            for (const s of sels) {
                const c = document.querySelector(s);
                if (c) { c.insertBefore(bell, c.firstChild); return true; }
            }
            return false;
        }
        if (!inject()) {
            const obs = new MutationObserver(() => { if (inject()) obs.disconnect(); });
            obs.observe(document.body, { childList: true, subtree: true });
            setTimeout(() => obs.disconnect(), 10_000);
        }

        // Panel
        const panel = document.createElement('div');
        panel.id = 'vn-panel';
        panel.setAttribute('role', 'dialog');
        panel.setAttribute('aria-label', 'Notifications');
        panel.innerHTML = `
            <div id="vn-panel-head">
                <span>Notifications</span>
                <div class="vn-head-actions">
                    <button id="vn-mark-all" class="vn-head-btn">Tout lu</button>
                    <button id="vn-clear-all" class="vn-head-btn">Effacer</button>
                </div>
            </div>
            <div id="vn-list">
                <div id="vn-empty">Aucune notification</div>
            </div>
        `;
        document.body.appendChild(panel);

        // Modal
        const overlay = document.createElement('div');
        overlay.id = 'vn-overlay';
        overlay.innerHTML = `
            <div id="vn-modal" role="dialog" aria-modal="true">
                <div class="vn-m-head">
                    <div class="vn-m-type" id="vn-m-type"></div>
                    <div class="vn-m-title-wrap">
                        <h3 class="vn-m-title" id="vn-m-title"></h3>
                        <div class="vn-m-meta" id="vn-m-meta"></div>
                    </div>
                    <button class="vn-m-close" id="vn-m-close" aria-label="Fermer">&times;</button>
                </div>
                <div class="vn-m-body" id="vn-m-body"></div>
                <div class="vn-m-foot">
                    <button class="vn-m-btn vn-m-btn-del" id="vn-m-del">Supprimer</button>
                    <button class="vn-m-btn vn-m-btn-ok" id="vn-m-ok">Fermer</button>
                </div>
            </div>
        `;
        document.body.appendChild(overlay);

        // Events
        bell.addEventListener('click', (e) => {
            e.stopPropagation();
            const open = panel.classList.toggle('open');
            bell.setAttribute('aria-expanded', String(open));
        });
        document.addEventListener('click', (e) => {
            if (!panel.contains(e.target) && e.target !== bell) {
                panel.classList.remove('open');
                bell.setAttribute('aria-expanded', 'false');
            }
        });

        document.getElementById('vn-mark-all').addEventListener('click', async () => {
            const unread = _notifs.filter(n => !n.isRead);
            await Promise.all(unread.map(n => markAsRead(n.id)));
            unread.forEach(n => { n.isRead = true; });
            render();
        });

        document.getElementById('vn-clear-all').addEventListener('click', async () => {
            const unread = _notifs.filter(n => !n.isRead);
            await Promise.all(unread.map(n => markAsRead(n.id)));
            _dismissed = _notifs.map(n => n.id);
            saveDismissed();
            _notifs = [];
            render();
        });

        document.getElementById('vn-m-close').addEventListener('click', closeModal);
        document.getElementById('vn-m-ok').addEventListener('click', closeModal);
        document.getElementById('vn-m-del').addEventListener('click', () => {
            if (_modalNotif) dismiss(_modalNotif.id);
            closeModal();
        });
        overlay.addEventListener('click', (e) => { if (e.target === overlay) closeModal(); });
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') { closeModal(); panel.classList.remove('open'); }
        });
    }

    // ── Dismissed ────────────────────────────────────────────────────
    let _dismissed = [];
    const DK = 'JellyNotif_dismissed';
    function loadDismissed() { try { _dismissed = JSON.parse(localStorage.getItem(DK) || '[]'); } catch(_) { _dismissed = []; } }
    function saveDismissed() {
        if (_dismissed.length > 200) _dismissed = _dismissed.slice(-200);
        try { localStorage.setItem(DK, JSON.stringify(_dismissed)); } catch(_) {}
    }
    function dismiss(id) {
        const n = _notifs.find(x => x.id === id);
        if (n && !n.isRead) markAsRead(id);
        _dismissed.push(id);
        saveDismissed();
        _notifs = _notifs.filter(x => x.id !== id);
        render();
    }

    // ── Render ───────────────────────────────────────────────────────
    let _notifs = [];

    function render() {
        const list = document.getElementById('vn-list');
        const empty = document.getElementById('vn-empty');
        const badge = document.getElementById('vn-badge');
        if (!list || !badge) return;

        const unread = _notifs.filter(n => !n.isRead).length;
        badge.textContent = unread > 99 ? '99+' : String(unread);
        badge.classList.toggle('on', unread > 0);

        Array.from(list.children).forEach(c => { if (c.id !== 'vn-empty') c.remove(); });
        empty.style.display = _notifs.length ? 'none' : 'block';

        _notifs.forEach(n => {
            const color = TYPE_COLOR[n.type] || TYPE_COLOR.Info;
            const item = document.createElement('div');
            item.className = 'vn-item' + (!n.isRead ? ' unread' : '');
            item.style.setProperty('--vn-color', color);

            item.innerHTML = `
                <div class="vn-item-bar"></div>
                <div class="vn-item-body">
                    <div class="vn-item-row1">
                        <span class="vn-item-title">${esc(n.title)}</span>
                        <span class="vn-item-time">${esc(n.date)}</span>
                    </div>
                    <div class="vn-item-preview">${esc(n.message)}</div>
                </div>
                <button class="vn-item-x" title="Supprimer">&times;</button>
            `;

            item.querySelector('.vn-item-x').addEventListener('click', (e) => {
                e.stopPropagation();
                item.style.opacity = '0';
                item.style.transform = 'translateX(12px)';
                setTimeout(() => dismiss(n.id), 150);
            });

            item.addEventListener('click', async () => {
                if (!n.isRead) {
                    await markAsRead(n.id);
                    n.isRead = true;
                    render();
                }
                openModal(n);
            });

            list.appendChild(item);
        });
    }

    // ── Modal ────────────────────────────────────────────────────────
    let _modalNotif = null;

    function openModal(n) {
        _modalNotif = n;
        const color = TYPE_COLOR[n.type] || TYPE_COLOR.Info;

        document.getElementById('vn-m-type').style.background = color;
        document.getElementById('vn-m-title').textContent = n.title;
        document.getElementById('vn-m-meta').textContent = n.type + '  ·  ' + n.date;
        document.getElementById('vn-m-body').textContent = n.message;

        document.getElementById('vn-overlay').classList.add('open');
        document.getElementById('vn-panel').classList.remove('open');
        document.getElementById('vn-bell')?.setAttribute('aria-expanded', 'false');
    }

    function closeModal() {
        _modalNotif = null;
        document.getElementById('vn-overlay').classList.remove('open');
    }

    // ── Sidebar ──────────────────────────────────────────────────────
    let _sidebarDone = false;

    async function injectSidebar() {
        if (_sidebarDone || !window.ApiClient) return;
        try {
            const uid = window.ApiClient.getCurrentUserId?.() ?? null;
            if (!uid) return;
            const user = await apiRequest(`/Users/${uid}`);
            if (!user?.Policy?.IsAdministrator) { _sidebarDone = true; return; }
        } catch (_) { return; }

        const sidebar = document.querySelector(
            '.adminDrawer .adminDrawerContent, .dashboardDocument .sidebarLinks, [data-role="panel"] .scrollY'
        );
        if (!sidebar || document.getElementById('vn-sidebar-link')) {
            if (document.getElementById('vn-sidebar-link')) _sidebarDone = true;
            return;
        }

        const a = document.createElement('a');
        a.id = 'vn-sidebar-link';
        a.href = `${getServerBase()}/web/index.html#!/configurationpage?name=JellyNotifSend`;
        a.innerHTML = `
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2"
                 stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24">
                <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
                <path d="M13.73 21a2 2 0 0 1-3.46 0"/>
            </svg>
            Notifications
        `;
        sidebar.appendChild(a);
        _sidebarDone = true;
    }

    // ── Polling ──────────────────────────────────────────────────────
    async function refresh() {
        if (!window.ApiClient) return;
        const uid = window.ApiClient.getCurrentUserId?.() ?? null;
        if (!uid) return;
        const data = await fetchNotifications();
        _notifs = data.filter(n => !_dismissed.includes(n.id));
        render();
    }

    // ── XSS ─────────────────────────────────────────────────────────
    function esc(s) {
        return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;')
            .replace(/>/g,'&gt;').replace(/"/g,'&quot;').replace(/'/g,'&#39;');
    }

    // ── Init ────────────────────────────────────────────────────────
    function init() {
        loadDismissed();
        injectStyles();
        buildUI();
        setTimeout(refresh, 2000);
        setInterval(refresh, POLL_INTERVAL);

        let sa = 0;
        const st = setInterval(() => {
            sa++;
            if (_sidebarDone || sa > 10) { clearInterval(st); return; }
            injectSidebar();
        }, 2000);
    }

    let _done = false;
    function tryInit() {
        if (_done) return;
        try {
            const ok = !!window.ApiClient
                && typeof window.ApiClient.serverAddress === 'function'
                && window.ApiClient.serverAddress()
                && typeof window.ApiClient.getCurrentUserId === 'function'
                && window.ApiClient.getCurrentUserId();
            if (ok) { _done = true; init(); }
        } catch (_) {}
    }

    document.addEventListener('apiclientcreated', () => setTimeout(tryInit, 500));
    document.addEventListener('signalr:connected', () => setTimeout(tryInit, 300));

    let _pa = 0;
    const _pt = setInterval(() => { if (_done || ++_pa > 60) { clearInterval(_pt); return; } tryInit(); }, 500);

    function onNav() {
        setTimeout(() => {
            if (!_done) tryInit();
            else { buildUI(); if (!_sidebarDone) injectSidebar(); }
        }, 800);
    }

    const _ps = history.pushState.bind(history);
    const _rs = history.replaceState.bind(history);
    history.pushState = function (...a) { _ps(...a); onNav(); };
    history.replaceState = function (...a) { _rs(...a); onNav(); };
})();
