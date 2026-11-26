/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/common/donate.js
 *  CHỨC NĂNG : Nút trái tim "Ủng hộ" trên header -> mở hộp mã QR + số tài khoản
 *              - Mở / đóng (nút X, bấm nền, phím Esc), giữ focus trong hộp
 *              - Chọn số tiền (Tùy tâm / 10k / 20k / 50k / 100k) -> đổi mã QR tương ứng
 *              - Sao chép ngân hàng / chủ tài khoản / số tài khoản / nội dung chuyển khoản
 *  DÙNG TẠI  : Views/Shared/_Layout.cshtml (HTML: Views/Shared/_DonateModalPartial.cshtml)
 * ============================================================================
 */
(function () {
    'use strict';

    const modal = document.getElementById('donateModal');
    if (!modal) return;

    let lastFocus = null;

    function open() {
        lastFocus = document.activeElement;
        modal.hidden = false;
        document.body.classList.add('donate-open');
        requestAnimationFrame(() => modal.classList.add('is-open'));
        setTimeout(() => modal.querySelector('.donate-x')?.focus(), 60);
    }

    function close() {
        modal.classList.remove('is-open');
        document.body.classList.remove('donate-open');
        setTimeout(() => { modal.hidden = true; }, 200);
        lastFocus?.focus?.();
    }

    document.addEventListener('click', (e) => {
        if (e.target.closest('[data-donate-open]')) { e.preventDefault(); open(); return; }
        if (!modal.hidden && e.target.closest('[data-donate-close]')) close();
    });

    document.addEventListener('keydown', (e) => {
        if (modal.hidden) return;
        if (e.key === 'Escape') { close(); return; }
        // Giữ phím Tab trong hộp
        if (e.key === 'Tab') {
            const items = modal.querySelectorAll('button, [href], [tabindex]:not([tabindex="-1"])');
            if (!items.length) return;
            const first = items[0], last = items[items.length - 1];
            if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
            else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
        }
    });

    // Chọn số tiền: mỗi số tiền có sẵn 1 mã QR (vẽ sẵn trong view), chỉ bật mã đang chọn
    const amountLabel = modal.querySelector('.donate-amount');
    modal.addEventListener('click', (e) => {
        const chip = e.target.closest('.donate-chip');
        if (!chip) return;
        const amount = chip.dataset.amount;
        modal.querySelectorAll('.donate-chip').forEach(c => {
            const on = c === chip;
            c.classList.toggle('is-active', on);
            c.setAttribute('aria-checked', on ? 'true' : 'false');
        });
        // Lưu ý: thẻ <svg> không có thuộc tính .hidden như thẻ HTML -> phải bật/tắt attribute
        modal.querySelectorAll('[data-qr-amount]').forEach(svg => {
            svg.toggleAttribute('hidden', svg.getAttribute('data-qr-amount') !== amount);
        });
        if (amountLabel) amountLabel.textContent = chip.dataset.label || chip.textContent.trim();
    });

    // Phím mũi tên trái/phải để đổi số tiền khi đang ở nhóm chip
    modal.addEventListener('keydown', (e) => {
        const chip = e.target.closest('.donate-chip');
        if (!chip || (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft')) return;
        const chips = [...modal.querySelectorAll('.donate-chip')];
        const i = chips.indexOf(chip) + (e.key === 'ArrowRight' ? 1 : -1);
        const next = chips[(i + chips.length) % chips.length];
        next.focus(); next.click(); e.preventDefault();
    });

    // Sao chép (ngân hàng, chủ tài khoản, số tài khoản, nội dung)
    modal.addEventListener('click', async (e) => {
        const btn = e.target.closest('.donate-copy');
        if (!btn) return;
        const text = btn.dataset.copy || '';
        let ok = false;
        try {
            await navigator.clipboard.writeText(text);
            ok = true;
        } catch (err) {
            // Trình duyệt cũ / không phải https: bôi đen sẵn để người dùng tự chép
            const value = btn.closest('.donate-field')?.querySelector('.donate-field-value');
            if (value) {
                const r = document.createRange(); r.selectNodeContents(value);
                const sel = window.getSelection(); sel.removeAllRanges(); sel.addRange(r);
            }
        }
        if (!ok) return;
        const icon = btn.querySelector('i');
        btn.classList.add('is-copied');
        icon.className = 'fa-solid fa-check';
        clearTimeout(btn._t);
        btn._t = setTimeout(() => {
            btn.classList.remove('is-copied');
            icon.className = 'fa-regular fa-copy';
        }, 1600);
    });
})();
