/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/community/community-report.js
 *  CHỨC NĂNG : Nút "Báo cáo" bài viết / bình luận ở trang Cộng đồng:
 *              mở hộp chọn lý do -> POST /Community/Report
 *              Admin xử lý tại Admin > Kiểm duyệt cộng đồng.
 *  DÙNG TẠI  : Views/Community/Index.cshtml (nạp sau community.js)
 *  PHỤ THUỘC : CURRENT_LANG (view), showToast(), openLoginModal() (_Layout)
 * ============================================================================
 */
(function () {
    const isEn = typeof CURRENT_LANG !== 'undefined' && CURRENT_LANG === 'en';
    const T = (vi, en) => (isEn ? en : vi);

    const REASONS = [
        { value: 'spam', label: T('Spam / quảng cáo', 'Spam / advertising') },
        { value: 'offensive', label: T('Ngôn từ xúc phạm, thù ghét', 'Offensive or hateful language') },
        { value: 'spoiler', label: T('Tiết lộ nội dung (spoiler) không cảnh báo', 'Unmarked spoiler') },
        { value: 'inappropriate', label: T('Nội dung khiêu dâm, bạo lực, không phù hợp', 'Sexual, violent or inappropriate content') },
        { value: 'other', label: T('Lý do khác', 'Other') }
    ];

    let modal = null;
    let current = null;     // { type, id }
    let lastFocus = null;

    function buildModal() {
        modal = document.createElement('div');
        modal.id = 'reportModal';
        modal.className = 'report-modal';
        modal.setAttribute('role', 'dialog');
        modal.setAttribute('aria-modal', 'true');
        modal.setAttribute('aria-labelledby', 'reportModalTitle');
        modal.hidden = true;
        modal.innerHTML = `
            <div class="report-backdrop" data-close></div>
            <div class="report-box">
                <div class="report-head">
                    <span class="report-icon"><i class="fa-solid fa-flag"></i></span>
                    <div>
                        <h3 id="reportModalTitle">${T('Báo cáo vi phạm', 'Report content')}</h3>
                        <p>${T('Chọn lý do. Báo cáo được gửi ẩn danh tới quản trị viên.', 'Choose a reason. Reports are sent anonymously to moderators.')}</p>
                    </div>
                    <button type="button" class="report-x" data-close aria-label="${T('Đóng', 'Close')}"><i class="fa-solid fa-xmark"></i></button>
                </div>
                <form class="report-form">
                    <fieldset class="report-reasons">
                        <legend class="sr-only">${T('Lý do', 'Reason')}</legend>
                        ${REASONS.map((r, i) => `
                            <label class="report-reason">
                                <input type="radio" name="reportReason" value="${r.value}" ${i === 0 ? 'checked' : ''}>
                                <span>${r.label}</span>
                            </label>`).join('')}
                    </fieldset>
                    <label class="report-note">
                        <span>${T('Ghi chú thêm (không bắt buộc)', 'Additional details (optional)')}</span>
                        <textarea name="reportNote" rows="3" maxlength="300" placeholder="${T('Mô tả ngắn vấn đề...', 'Briefly describe the issue...')}"></textarea>
                    </label>
                    <div class="report-actions">
                        <button type="button" class="report-cancel btn-grad-cus" data-close>${T('Hủy', 'Cancel')}</button>
                        <button type="submit" class="report-submit btn-grad-deny">
                            <i class="fa-solid fa-paper-plane"></i> ${T('Gửi báo cáo', 'Send report')}
                        </button>
                    </div>
                </form>
            </div>`;
        document.body.appendChild(modal);

        modal.addEventListener('click', e => { if (e.target.closest('[data-close]')) close(); });
        modal.addEventListener('keydown', e => { if (e.key === 'Escape') close(); });
        modal.querySelector('.report-form').addEventListener('submit', submit);
    }

    function open(type, id) {
        if (!modal) buildModal();
        current = { type, id };
        lastFocus = document.activeElement;
        modal.querySelector('.report-form').reset();
        modal.hidden = false;
        requestAnimationFrame(() => modal.classList.add('is-open'));
        modal.querySelector('input[name="reportReason"]').focus();
    }

    function close() {
        if (!modal || modal.hidden) return;
        modal.classList.remove('is-open');
        setTimeout(() => { modal.hidden = true; }, 180);
        if (lastFocus && lastFocus.focus) lastFocus.focus();
    }

    async function submit(e) {
        e.preventDefault();
        if (!current) return;
        const form = e.target;
        const btn = form.querySelector('.report-submit');
        btn.disabled = true;

        try {
            const res = await fetch('/Community/Report', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    targetType: current.type,
                    targetId: current.id,
                    reason: form.reportReason.value,
                    note: form.reportNote.value
                })
            });
            const data = await res.json();

            if (data.requireLogin) {
                close();
                if (typeof openLoginModal === 'function') openLoginModal();
                else if (typeof showToast === 'function') showToast(data.message, 'error');
                return;
            }

            if (typeof showToast === 'function') showToast(data.message, data.success ? 'success' : 'error');
            if (data.success) {
                close();
                // Đánh dấu nút đã báo cáo
                document.querySelectorAll(`[onclick="reportContent('${current.type}', ${current.id})"]`).forEach(b => {
                    b.classList.add('is-reported');
                    b.title = T('Đã báo cáo', 'Reported');
                    const i = b.querySelector('i');
                    if (i) i.className = 'fa-solid fa-flag';
                });
            }
        } catch (err) {
            console.error(err);
            if (typeof showToast === 'function') showToast(T('Không gửi được báo cáo, vui lòng thử lại.', 'Could not send the report. Please try again.'), 'error');
        } finally {
            btn.disabled = false;
        }
    }

    window.reportContent = open;
})();
