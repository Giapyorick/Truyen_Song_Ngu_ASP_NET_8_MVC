/**
 * ============================================================================
 *  FILE      : wwwroot/js/admin/moderation/moderation.js
 *  CHỨC NĂNG : Trang Admin > Kiểm duyệt cộng đồng
 *              - Tab Báo cáo (gom theo nội dung), Bài viết, Bình luận
 *              - Tìm kiếm, lọc theo truyện / trạng thái, phân trang
 *              - Ẩn (kèm lý do) / Hiện lại / Xóa vĩnh viễn / Bỏ qua báo cáo
 *  DÙNG TẠI  : Areas/Admin/Views/CommunityModeration/Index.cshtml
 *  PHỤ THUỘC : showToast(), customConfirm(), IS_VIEWER_MODE (_LayoutAdmin.cshtml), window.MOD_LANG
 *  API       : /Admin/CommunityModeration/{Stats, Stories, Reports, PostList, CommentList,
 *                                           SetHidden, Delete, DismissReports}
 * ============================================================================
 */
window.Moderation = (function () {
    const L = window.MOD_LANG || {};
    const API = '/Admin/CommunityModeration/';
    const state = { tab: 'reports', page: 1, pageSize: 10, search: '', story: '', visibility: 'all', reportStatus: 'pending' };
    let searchTimer = null;
    let pendingHide = null;   // { type, id }

    // ---------- tiện ích ----------
    const $ = (s) => document.querySelector(s);
    const esc = (v) => String(v ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
    // Ảnh đại diện: đường dẫn trong CSDL dạng "assets/image/users/..." (không có "/" đầu).
    // Không có ảnh hoặc ảnh lỗi -> hiện chữ cái đầu của tên (không phụ thuộc file placeholder).
    const avatarUrl = (img) => {
        if (!img) return '';
        if (/^(https?:)?\/\//i.test(img) || img.startsWith('/')) return img;
        return '/' + img;
    };
    const initial = (name) => (String(name || '?').trim().charAt(0) || '?').toUpperCase();
    function avatarHtml(item) {
        const url = avatarUrl(item.userImg);
        const fallback = `<span class="mod-avatar-fallback"${url ? ' hidden' : ''}>${esc(initial(item.userName))}</span>`;
        const img = url
            ? `<img src="${esc(url)}" alt="" loading="lazy" onerror="this.hidden=true;this.nextElementSibling.hidden=false">`
            : '';
        return `<span class="mod-avatar">${img}${fallback}</span>`;
    }
    const toast = (msg, ok = true) => typeof showToast === 'function' && showToast(msg, ok ? 'success' : 'error', 4000);
    const isViewer = () => window.IS_VIEWER_MODE === true;

    async function getJson(url) {
        const res = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!res.ok) throw new Error(res.status);
        return res.json();
    }

    async function postJson(action, body) {
        const res = await fetch(API + action, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
            body: JSON.stringify(body)
        });
        return res.json();
    }

    // ---------- thống kê + danh sách truyện ----------
    async function loadStats() {
        try {
            const s = await getJson(API + 'Stats');
            $('#statPending').textContent = s.pendingReports;
            $('#statPosts').textContent = s.totalPosts;
            $('#statHiddenPosts').textContent = s.hiddenPosts;
            $('#statComments').textContent = s.totalComments;
            $('#statHiddenComments').textContent = s.hiddenComments;
            const badge = $('#tabBadgeReports');
            badge.textContent = s.pendingReports;
            badge.hidden = !s.pendingReports;
        } catch (e) { console.error(e); }
    }

    async function loadStories() {
        try {
            const list = await getJson(API + 'Stories');
            const sel = $('#modStory');
            list.forEach(s => {
                const o = document.createElement('option');
                o.value = s.id;
                o.textContent = s.title;
                sel.appendChild(o);
            });
        } catch (e) { console.error(e); }
    }

    // ---------- render ----------
    function actionButtons(type, id, isHidden, withDismiss) {
        const dis = isViewer() ? 'disabled' : '';
        return `
            <div class="mod-actions">
                ${withDismiss ? `<button type="button" class="mod-btn btn-grad-accepted" ${dis} onclick="Moderation.dismiss('${type}', ${id})"><i class="fa-solid fa-check"></i> ${esc(L.dismiss)}</button>` : ''}
                ${isHidden
                    ? `<button type="button" class="mod-btn btn-grad-accepted" ${dis} onclick="Moderation.setHidden('${type}', ${id}, false)"><i class="fa-solid fa-eye"></i> ${esc(L.unhide)}</button>`
                    : `<button type="button" class="mod-btn btn-grad-wait" ${dis} onclick="Moderation.openHide('${type}', ${id})"><i class="fa-solid fa-eye-slash"></i> ${esc(L.hide)}</button>`}
                <button type="button" class="mod-btn btn-grad-deny" ${dis} onclick="Moderation.remove('${type}', ${id})"><i class="fa-regular fa-trash-can"></i> ${esc(L.del)}</button>
            </div>`;
    }

    function metaRow(item) {
        return `
            <div class="mod-meta">
                ${avatarHtml(item)}
                <div>
                    <b>${esc(item.userName)}</b>
                    <small>${esc(item.createdAt)}${item.storyTitle ? ` · <i class="fa-solid fa-book"></i> ${esc(item.storyTitle)}` : ''}</small>
                </div>
            </div>`;
    }

    function hiddenNote(item) {
        if (!item.isHidden) return '';
        return `<div class="mod-hidden-note"><i class="fa-solid fa-eye-slash"></i> ${esc(L.hidden)}${item.hiddenAt ? ' · ' + esc(item.hiddenAt) : ''}${item.hiddenReason ? ' — ' + esc(item.hiddenReason) : ''}</div>`;
    }

    function renderPosts(items) {
        return items.map(p => `
            <article class="mod-item ${p.isHidden ? 'is-hidden' : ''}">
                <div class="mod-item-top">
                    ${metaRow(p)}
                    <div class="mod-tags">
                        <span class="mod-tag">${esc(L.post)} #${p.id}</span>
                        ${p.isSpoiler ? `<span class="mod-tag mod-tag--muted">${esc(L.spoiler)}</span>` : ''}
                        ${p.pendingReports ? `<span class="mod-tag mod-tag--warn"><i class="fa-solid fa-flag"></i> ${p.pendingReports} ${esc(L.reports)}</span>` : ''}
                    </div>
                </div>
                <p class="mod-content">${esc(p.content)}</p>
                ${hiddenNote(p)}
                <div class="mod-item-bottom">
                    <span class="mod-counts"><i class="fa-regular fa-heart"></i> ${p.likes} ${esc(L.likes)} · <i class="fa-regular fa-comment"></i> ${p.comments} ${esc(L.comments)}</span>
                    ${actionButtons('post', p.id, p.isHidden, false)}
                </div>
            </article>`).join('');
    }

    function renderComments(items) {
        return items.map(c => `
            <article class="mod-item ${c.isHidden ? 'is-hidden' : ''}">
                <div class="mod-item-top">
                    ${metaRow(c)}
                    <div class="mod-tags">
                        <span class="mod-tag">${esc(c.isReply ? L.reply : L.comment)} #${c.id}</span>
                        ${c.isSpoiler ? `<span class="mod-tag mod-tag--muted">${esc(L.spoiler)}</span>` : ''}
                        ${c.pendingReports ? `<span class="mod-tag mod-tag--warn"><i class="fa-solid fa-flag"></i> ${c.pendingReports} ${esc(L.reports)}</span>` : ''}
                    </div>
                </div>
                <p class="mod-content">${esc(c.content)}</p>
                <p class="mod-context"><i class="fa-solid fa-reply"></i> ${esc(L.inPost)} “${esc(c.postSnippet)}” <span>(${esc(L.post)} #${c.postId})</span></p>
                ${hiddenNote(c)}
                <div class="mod-item-bottom">
                    <span class="mod-counts">${c.replies ? `<i class="fa-regular fa-comments"></i> ${c.replies} ${esc(L.replies)}` : ''}</span>
                    ${actionButtons('comment', c.id, c.isHidden, false)}
                </div>
            </article>`).join('');
    }

    function renderReports(items) {
        return items.map(r => {
            const t = r.target;
            const reasons = r.reasons.map(x => `<span class="mod-tag mod-tag--warn">${esc(L.reasons[x.reason] || x.reason)} × ${x.count}</span>`).join('');
            const notes = r.notes.map(n => `<li><b>${esc(n.reporter)}:</b> ${esc(n.note)}</li>`).join('');
            const typeLabel = r.targetType === 'post' ? L.post : L.comment;
            const pending = state.reportStatus === 'pending';

            return `
            <article class="mod-item mod-item--report ${t && t.isHidden ? 'is-hidden' : ''}">
                <div class="mod-item-top">
                    ${t ? metaRow(t) : `<div class="mod-meta"><div><b>${esc(L.deletedContent)}</b></div></div>`}
                    <div class="mod-tags">
                        <span class="mod-tag">${esc(typeLabel)} #${r.targetId}</span>
                        <span class="mod-tag mod-tag--danger"><i class="fa-solid fa-flag"></i> ${r.count} ${esc(L.reports)}</span>
                    </div>
                </div>
                ${t ? `<p class="mod-content">${esc(t.content)}</p>` : ''}
                <div class="mod-reasons">${reasons}<small>${esc(r.lastAt)}</small></div>
                ${notes ? `<ul class="mod-notes">${notes}</ul>` : ''}
                ${t ? hiddenNote(t) : ''}
                ${t && pending ? `<div class="mod-item-bottom"><span></span>${actionButtons(r.targetType, r.targetId, t.isHidden, true)}</div>` : ''}
            </article>`;
        }).join('');
    }

    function renderPagination(total) {
        const pages = Math.max(1, Math.ceil(total / state.pageSize));
        const box = $('#modPagination');
        if (pages <= 1) { box.innerHTML = ''; return; }

        const btn = (p, label, disabled, active) =>
            `<button type="button" class="mod-page-btn ${active ? 'is-active btn-grad-cus' : ''}" ${disabled ? 'disabled' : ''} onclick="Moderation.go(${p})">${label}</button>`;

        let html = btn(state.page - 1, `<i class="fa-solid fa-chevron-left"></i>`, state.page === 1, false);
        const from = Math.max(1, state.page - 2), to = Math.min(pages, state.page + 2);
        if (from > 1) html += btn(1, '1', false, false) + (from > 2 ? '<span class="mod-page-gap">…</span>' : '');
        for (let p = from; p <= to; p++) html += btn(p, p, false, p === state.page);
        if (to < pages) html += (to < pages - 1 ? '<span class="mod-page-gap">…</span>' : '') + btn(pages, pages, false, false);
        html += btn(state.page + 1, `<i class="fa-solid fa-chevron-right"></i>`, state.page === pages, false);
        box.innerHTML = html;
    }

    // ---------- tải danh sách ----------
    async function load() {
        const list = $('#modList');
        list.classList.add('is-loading');
        try {
            let url;
            const qs = new URLSearchParams({ page: state.page, pageSize: state.pageSize });
            if (state.tab === 'reports') {
                qs.set('status', state.reportStatus);
                url = API + 'Reports?' + qs;
            } else {
                if (state.search) qs.set('search', state.search);
                if (state.story) qs.set('storyId', state.story);
                qs.set('visibility', state.visibility);
                url = API + (state.tab === 'posts' ? 'PostList?' : 'CommentList?') + qs;
            }

            const data = await getJson(url);
            if (!data.items.length) {
                list.innerHTML = `<div class="mod-empty"><i class="fa-regular fa-face-smile"></i><p>${esc(state.tab === 'reports' && state.reportStatus === 'pending' ? L.emptyReports : L.empty)}</p></div>`;
            } else {
                list.innerHTML = state.tab === 'reports' ? renderReports(data.items)
                    : state.tab === 'posts' ? renderPosts(data.items)
                        : renderComments(data.items);
            }
            renderPagination(data.total);
        } catch (e) {
            console.error(e);
            list.innerHTML = `<div class="mod-empty mod-empty--error"><i class="fa-solid fa-triangle-exclamation"></i><p>${esc(L.loadError)}</p></div>`;
        } finally {
            list.classList.remove('is-loading');
        }
    }

    function reload() {
        loadStats();
        load();
    }

    function switchTab(tab) {
        state.tab = tab;
        state.page = 1;
        document.querySelectorAll('.mod-tab').forEach(b => {
            const on = b.dataset.tab === tab;
            b.classList.toggle('is-active', on);
            b.setAttribute('aria-selected', on ? 'true' : 'false');
        });
        // Hiện ô lọc phù hợp với tab
        document.querySelectorAll('.mod-toolbar [data-for]').forEach(el => {
            el.hidden = !el.dataset.for.split(' ').includes(tab);
        });
        load();
    }

    // ---------- hành động ----------
    async function run(action, body) {
        if (isViewer()) return;
        try {
            const res = await postJson(action, body);
            toast(res.message, res.success);
            if (res.success) reload();
        } catch (e) {
            console.error(e);
            toast(L.loadError, false);
        }
    }

    function openHide(type, id) {
        if (isViewer()) return;
        pendingHide = { type, id };
        const m = $('#hideModal');
        $('#hideReason').value = '';
        m.hidden = false;
        requestAnimationFrame(() => m.classList.add('is-open'));
        $('#hideReason').focus();
    }

    function closeHide() {
        const m = $('#hideModal');
        m.classList.remove('is-open');
        setTimeout(() => { m.hidden = true; }, 160);
        pendingHide = null;
    }

    function setHidden(type, id, hidden, reason) {
        return run('SetHidden', { type, id, hidden, reason: reason || null });
    }

    async function remove(type, id) {
        if (isViewer()) return;
        const msg = type === 'post' ? L.confirmDeletePost : L.confirmDeleteComment;
        const ok = typeof customConfirm === 'function' ? await customConfirm(esc(msg), L.confirmDeleteTitle) : confirm(msg);
        if (ok) run('Delete', { type, id });
    }

    function dismiss(type, id) {
        return run('DismissReports', { type, id });
    }

    function go(page) {
        state.page = Math.max(1, page);
        load();
        document.querySelector('.mod-card')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    // ---------- khởi tạo ----------
    function init() {
        document.querySelectorAll('.mod-tab').forEach(b => b.addEventListener('click', () => switchTab(b.dataset.tab)));

        $('#modSearch').addEventListener('input', e => {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(() => { state.search = e.target.value.trim(); state.page = 1; load(); }, 350);
        });
        $('#modStory').addEventListener('change', e => { state.story = e.target.value; state.page = 1; load(); });
        $('#modVisibility').addEventListener('change', e => { state.visibility = e.target.value; state.page = 1; load(); });
        $('#modReportStatus').addEventListener('change', e => { state.reportStatus = e.target.value; state.page = 1; load(); });

        // Hộp lý do ẩn
        const m = $('#hideModal');
        m.addEventListener('click', e => {
            if (e.target.closest('[data-close]')) closeHide();
            const preset = e.target.closest('[data-reason]');
            if (preset) $('#hideReason').value = preset.dataset.reason;
        });
        m.addEventListener('keydown', e => {
            if (e.key === 'Escape') closeHide();
            if (e.key === 'Enter' && e.target.id === 'hideReason') $('#hideConfirm').click();
        });
        $('#hideConfirm').addEventListener('click', () => {
            if (!pendingHide) return;
            const { type, id } = pendingHide;
            const reason = $('#hideReason').value.trim();
            closeHide();
            setHidden(type, id, true, reason);
        });

        loadStories();
        switchTab('reports');
        loadStats();
    }

    document.addEventListener('DOMContentLoaded', init);

    return { reload, switchTab, go, openHide, setHidden, remove, dismiss };
})();
