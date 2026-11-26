/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/common/theme-toggle.js
 *  CHỨC NĂNG : Nút chuyển giao diện sáng / tối (#btnThemeToggle trên header)
 *              - Thêm / bỏ class "dark" trên <html> (Tailwind đã cấu hình darkMode: 'class')
 *              - Lưu lựa chọn vào localStorage "site-theme" ('dark' | 'light')
 *              - Chưa chọn lần nào -> theo cài đặt sáng/tối của máy, đổi theo khi máy đổi
 *              - Script nhỏ trong <head> của _Layout áp theme trước khi trang hiện (không nháy trắng)
 *  DÙNG TẠI  : Views/Shared/_Layout.cshtml (chỉ nạp ở trang đã có giao diện tối – biến themeReady)
 *  CSS       : wwwroot/css/home/dark-theme.css
 * ============================================================================
 */
(function () {
    'use strict';

    const KEY = 'site-theme';
    const root = document.documentElement;
    const btn = document.getElementById('btnThemeToggle');
    const media = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

    function saved() {
        try {
            const v = localStorage.getItem(KEY);
            return v === 'dark' || v === 'light' ? v : null;
        } catch (e) { return null; }
    }

    function save(v) {
        try { localStorage.setItem(KEY, v); } catch (e) { }
    }

    function isDark() {
        return root.classList.contains('dark');
    }

    function paintButton() {
        if (!btn) return;
        const dark = isDark();
        const label = dark ? btn.dataset.labelLight : btn.dataset.labelDark;
        btn.setAttribute('aria-pressed', dark ? 'true' : 'false');
        if (label) {
            btn.title = label;
            btn.setAttribute('aria-label', label);
        }
    }

    function apply(theme, animate) {
        // Tắt transition trong lúc đổi để mọi thành phần đổi màu cùng lúc (không bị "loang")
        root.classList.add('theme-switching');
        root.classList.toggle('dark', theme === 'dark');
        paintButton();
        window.dispatchEvent(new CustomEvent('site-theme-change', { detail: { theme } }));
        requestAnimationFrame(() => requestAnimationFrame(() => root.classList.remove('theme-switching')));

        if (animate && btn) {
            btn.classList.remove('is-spinning');
            void btn.offsetWidth;          // chạy lại hiệu ứng xoay
            btn.classList.add('is-spinning');
        }
    }

    btn?.addEventListener('click', () => {
        const next = isDark() ? 'light' : 'dark';
        save(next);
        apply(next, true);
    });

    btn?.addEventListener('animationend', () => btn.classList.remove('is-spinning'));

    // Người dùng chưa tự chọn -> đi theo cài đặt của máy khi máy đổi sáng/tối
    media?.addEventListener?.('change', (e) => {
        if (!saved()) apply(e.matches ? 'dark' : 'light', false);
    });

    // Đồng bộ giữa các tab đang mở
    window.addEventListener('storage', (e) => {
        if (e.key === KEY && (e.newValue === 'dark' || e.newValue === 'light')) apply(e.newValue, false);
    });

    paintButton();
})();
