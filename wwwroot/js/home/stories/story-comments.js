/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/stories/story-comments.js
 *  CHỨC NĂNG : Bình luận truyện ở trang chi tiết (Views/Stories/Detail.cshtml)
 *              - Tải danh sách (mới nhất trước), "Xem thêm" theo trang
 *              - Gửi bình luận (cần đăng nhập, tối đa 1000 ký tự, Ctrl+Enter để gửi)
 *              - Xóa bình luận của mình (bấm 2 lần để xác nhận); Admin xóa được mọi bình luận
 *  API       : StoryCommentsController – /StoryComments/List | Add | Delete
 *  PHỤ THUỘC : showToast() (nếu có), openLoginModal() (nếu có)
 *  AN TOÀN   : mọi nội dung người dùng đều được escape trước khi chèn vào trang
 * ============================================================================
 */
(function () {
    'use strict';

    const root = document.getElementById('storyComments');
    if (!root) return;

    const storyId = parseInt(root.dataset.storyId, 10) || 0;
    const isEn = root.dataset.lang === 'en';
    const L = isEn ? {
        reader: 'Reader', you: 'You', del: 'Delete', confirmDel: 'Delete?', more: 'Load more comments',
        loading: 'Loading comments…', empty: 'No comments yet. Be the first to share your thoughts!',
        failed: 'Could not load comments.', retry: 'Try again', sent: 'Comment posted!', deleted: 'Comment deleted.',
        tooShort: 'Your comment is too short.', error: 'Something went wrong, please try again.',
        justNow: 'just now', min: 'min ago', hour: 'h ago', day: 'd ago'
    } : {
        reader: 'Độc giả', you: 'Bạn', del: 'Xóa', confirmDel: 'Xóa thật?', more: 'Xem thêm bình luận',
        loading: 'Đang tải bình luận…', empty: 'Chưa có bình luận nào. Hãy là người đầu tiên chia sẻ cảm nghĩ!',
        failed: 'Không tải được bình luận.', retry: 'Thử lại', sent: 'Đã gửi bình luận!', deleted: 'Đã xóa bình luận.',
        tooShort: 'Bình luận quá ngắn.', error: 'Có lỗi xảy ra, vui lòng thử lại.',
        justNow: 'vừa xong', min: 'phút trước', hour: 'giờ trước', day: 'ngày trước'
    };

    const $list = root.querySelector('.sdc-list');
    const $more = root.querySelector('.sdc-more');
    const $count = root.querySelectorAll('[data-comment-count]');
    const $form = root.querySelector('.sdc-form');
    const $input = root.querySelector('.sdc-input');
    const $submit = root.querySelector('.sdc-submit');
    const $chars = root.querySelector('.sdc-chars');
    const MAX = 1000;

    let page = 0;
    let loading = false;

    // ---------- Tiện ích ----------
    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const toast = (msg, type) => (typeof window.showToast === 'function') ? window.showToast(msg, type || 'info') : console.info(msg);

    function avatarUrl(img) {
        if (!img) return '';
        if (/^(https?:)?\/\//i.test(img) || img.startsWith('/')) return img;
        return '/' + img;
    }

    function initial(name) {
        return (String(name || '?').trim().charAt(0) || '?').toUpperCase();
    }

    function timeAgo(ms) {
        if (!ms) return '';
        const diff = Math.max(0, Date.now() - ms) / 1000;
        if (diff < 60) return L.justNow;
        if (diff < 3600) return `${Math.floor(diff / 60)} ${L.min}`;
        if (diff < 86400) return `${Math.floor(diff / 3600)} ${L.hour}`;
        if (diff < 86400 * 7) return `${Math.floor(diff / 86400)} ${L.day}`;
        const d = new Date(ms);
        return d.toLocaleDateString(isEn ? 'en-US' : 'vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    function fullDate(ms) {
        return ms ? new Date(ms).toLocaleString(isEn ? 'en-US' : 'vi-VN') : '';
    }

    function setCount(n) {
        $count.forEach(el => { el.textContent = n; });
    }

    async function api(url, body) {
        const opt = body
            ? { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body), credentials: 'same-origin' }
            : { credentials: 'same-origin' };
        const res = await fetch(url, opt);
        if (!res.ok) throw new Error('HTTP ' + res.status);
        return res.json();
    }

    // ---------- Hiển thị ----------
    function avatarHtml(item) {
        const url = avatarUrl(item.userImg);
        const fb = `<span class="sdc-avatar-fb"${url ? ' hidden' : ''}>${esc(initial(item.userName))}</span>`;
        const img = url ? `<img src="${esc(url)}" alt="" loading="lazy" onerror="this.hidden=true;this.nextElementSibling.hidden=false">` : '';
        return `<span class="sdc-avatar">${img}${fb}</span>`;
    }

    function itemHtml(item, isNew) {
        return `
            <li class="sdc-item${item.isMine ? ' is-mine' : ''}${isNew ? ' is-new' : ''}" data-id="${Number(item.id) || 0}">
                ${avatarHtml(item)}
                <div class="sdc-body">
                    <div class="sdc-meta">
                        <b class="sdc-name">${esc(item.userName || L.reader)}</b>
                        ${item.isMine ? `<span class="sdc-you">${esc(L.you)}</span>` : ''}
                        <time class="sdc-time" datetime="${item.createdAt ? new Date(item.createdAt).toISOString() : ''}" title="${esc(fullDate(item.createdAt))}">${esc(timeAgo(item.createdAt))}</time>
                        ${item.canDelete ? `<button type="button" class="sdc-del" data-del="${Number(item.id) || 0}" title="${esc(L.del)}"><i class="fa-regular fa-trash-can"></i><span>${esc(L.del)}</span></button>` : ''}
                    </div>
                    <p class="sdc-text">${esc(item.contents)}</p>
                </div>
            </li>`;
    }

    function showEmpty() {
        if (!$list.querySelector('.sdc-item')) {
            $list.innerHTML = `<li class="sdc-empty"><i class="fa-regular fa-comments"></i><span>${esc(L.empty)}</span></li>`;
        }
    }

    // ---------- Tải danh sách ----------
    async function load(next) {
        if (loading) return;
        loading = true;
        const target = next ? page + 1 : 1;
        if (!next) $list.innerHTML = `<li class="sdc-loading"><i class="fa-solid fa-circle-notch fa-spin"></i> ${esc(L.loading)}</li>`;
        if ($more) $more.disabled = true;

        try {
            const res = await api(`/StoryComments/List?storyId=${storyId}&page=${target}`);
            if (!res || !res.success) throw new Error('fail');

            page = target;
            if (!next) $list.innerHTML = '';
            $list.querySelector('.sdc-loading')?.remove();

            // Bỏ các bình luận đã có (tránh trùng khi vừa gửi xong rồi bấm "Xem thêm")
            const html = (res.items || [])
                .filter(it => !$list.querySelector(`.sdc-item[data-id="${Number(it.id) || 0}"]`))
                .map(it => itemHtml(it, false)).join('');
            $list.insertAdjacentHTML('beforeend', html);

            setCount(res.total ?? 0);
            showEmpty();
            if ($more) $more.hidden = !res.hasMore;
        } catch (e) {
            if (!next) {
                $list.innerHTML = `<li class="sdc-empty sdc-error"><i class="fa-solid fa-triangle-exclamation"></i><span>${esc(L.failed)}</span>
                    <button type="button" class="sdc-retry">${esc(L.retry)}</button></li>`;
            } else {
                toast(L.error, 'error');
            }
        } finally {
            loading = false;
            if ($more) $more.disabled = false;
        }
    }

    // ---------- Gửi bình luận ----------
    function updateChars() {
        if (!$input || !$chars) return;
        const n = $input.value.length;
        $chars.textContent = `${n}/${MAX}`;
        $chars.classList.toggle('is-near', n > MAX * 0.9);
        if ($submit) $submit.disabled = $input.value.trim().length < 2 || n > MAX;
    }

    function autoGrow() {
        if (!$input) return;
        $input.style.height = 'auto';
        $input.style.height = Math.min($input.scrollHeight, 260) + 'px';
    }

    async function submit(e) {
        e?.preventDefault();
        if (!$input || !$submit || $submit.disabled) return;
        const contents = $input.value.trim();
        if (contents.length < 2) { toast(L.tooShort, 'warning'); return; }

        $submit.disabled = true;
        $submit.classList.add('is-loading');
        try {
            const res = await api('/StoryComments/Add', { storyId, contents });
            if (res.requireLogin) {
                if (typeof window.openLoginModal === 'function') window.openLoginModal();
                else toast(res.message, 'error');
                return;
            }
            if (!res.success) { toast(res.message || L.error, 'error'); return; }

            $list.querySelector('.sdc-empty')?.remove();
            $list.insertAdjacentHTML('afterbegin', itemHtml(res.item, true));
            setCount(res.total ?? 0);
            $input.value = '';
            autoGrow();
            toast(L.sent, 'success');
        } catch (err) {
            toast(L.error, 'error');
        } finally {
            $submit.classList.remove('is-loading');
            updateChars();
        }
    }

    // ---------- Xóa bình luận (bấm lần 1 hỏi lại, lần 2 mới xóa) ----------
    async function remove(btn) {
        if (!btn.classList.contains('is-confirm')) {
            root.querySelectorAll('.sdc-del.is-confirm').forEach(b => {
                b.classList.remove('is-confirm'); b.querySelector('span').textContent = L.del;
            });
            btn.classList.add('is-confirm');
            btn.querySelector('span').textContent = L.confirmDel;
            clearTimeout(btn._t);
            btn._t = setTimeout(() => {
                btn.classList.remove('is-confirm');
                btn.querySelector('span').textContent = L.del;
            }, 3500);
            return;
        }

        clearTimeout(btn._t);
        btn.disabled = true;
        const id = parseInt(btn.dataset.del, 10) || 0;
        try {
            const res = await api('/StoryComments/Delete', { id });
            if (!res.success) { toast(res.message || L.error, 'error'); btn.disabled = false; return; }

            const li = btn.closest('.sdc-item');
            li.classList.add('is-removing');
            setTimeout(() => { li.remove(); showEmpty(); }, 250);
            setCount(res.total ?? 0);
            toast(L.deleted, 'success');
        } catch (e) {
            toast(L.error, 'error');
            btn.disabled = false;
        }
    }

    // ---------- Sự kiện ----------
    $form?.addEventListener('submit', submit);
    $input?.addEventListener('input', () => { updateChars(); autoGrow(); });
    $input?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) submit(e);
    });
    $more?.addEventListener('click', () => load(true));

    root.addEventListener('click', (e) => {
        const del = e.target.closest('.sdc-del');
        if (del) { remove(del); return; }
        if (e.target.closest('.sdc-retry')) { load(false); return; }
        if (e.target.closest('.sdc-login')) {
            if (typeof window.openLoginModal === 'function') window.openLoginModal();
            else window.location.href = '/Login';
        }
    });

    // Cập nhật "x phút trước" mỗi phút
    setInterval(() => {
        root.querySelectorAll('.sdc-time[datetime]').forEach(t => {
            const ms = Date.parse(t.getAttribute('datetime'));
            if (ms) t.textContent = timeAgo(ms);
        });
    }, 60000);

    updateChars();

    // Chỉ tải khi người đọc cuộn gần tới khu bình luận (đỡ tốn truy vấn)
    if ('IntersectionObserver' in window) {
        const io = new IntersectionObserver((entries) => {
            if (entries.some(en => en.isIntersecting)) { io.disconnect(); load(false); }
        }, { rootMargin: '400px' });
        io.observe(root);
    } else {
        load(false);
    }
})();
