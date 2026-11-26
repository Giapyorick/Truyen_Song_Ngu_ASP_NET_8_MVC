/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/reading/reading-lang-flags.js
 *  CHỨC NĂNG : Thay 2 ô chọn ngôn ngữ (#srcLangSelect, #targetLangSelect) bằng
 *              menu có ảnh cờ thật. Emoji cờ (🇺🇸) không hiện trên Windows nên
 *              dùng ảnh SVG ở /assets/flags/ (thư viện flag-icons, MIT).
 *  CÁCH LÀM  : Ô <select> gốc vẫn còn (chỉ ẩn đi) và vẫn là nơi giữ giá trị.
 *              Chọn trong menu -> gán value cho select + trigger 'change',
 *              nên logic cũ trong reading-viewer.js giữ nguyên hoàn toàn.
 *  ĐẢO NGÔN NGỮ: nút #btnSwapLang đổi chỗ 2 giá trị rồi trigger 'change' một lần,
 *              reading-viewer.js tự render lại chương theo cặp ngôn ngữ mới.
 *  DÙNG TẠI  : Views/Paragraphs/Content.cshtml (nạp SAU reading-viewer.js)
 * ============================================================================
 */
(function ($) {
    const FLAGS = {
        en: { flag: 'us', label: 'EN', name: 'English' },
        vi: { flag: 'vn', label: 'VN', name: 'Tiếng Việt' },
        zh: { flag: 'cn', label: 'ZH', name: '中文' },
        ja: { flag: 'jp', label: 'JP', name: '日本語' },
        fr: { flag: 'fr', label: 'FR', name: 'Français' }
    };

    const flagImg = (code) => {
        const f = (FLAGS[code] || {}).flag;
        return f
            ? `<img class="lf-flag" src="/assets/flags/${f}.svg" alt="" width="20" height="15">`
            : `<span class="lf-flag lf-flag-empty"></span>`;
    };
    const label = (code) => (FLAGS[code] && FLAGS[code].label) || (code || '').toUpperCase();

    let $openMenu = null;

    function closeMenu() {
        if ($openMenu) {
            $openMenu.data('trigger').attr('aria-expanded', 'false');
            $openMenu.remove();
            $openMenu = null;
        }
    }

    // Lấy theme hiện tại để menu (gắn vào body) cùng màu với thanh
    function currentTheme() {
        const el = $('#focusModeOverlay').is(':visible') ? $('#focusModeOverlay') : $('#mainWrapper');
        if (el.hasClass('theme-dark')) return 'dark';
        if (el.hasClass('theme-sepia')) return 'sepia';
        return 'light';
    }

    function buildPicker(selectId) {
        const $select = $('#' + selectId);
        if (!$select.length || $select.data('lf-ready')) return;
        $select.data('lf-ready', true).addClass('lf-native');

        const $btn = $(`<button type="button" class="lf-trigger" aria-haspopup="listbox" aria-expanded="false"></button>`);
        $btn.attr('aria-label', selectId === 'srcLangSelect' ? 'Source language' : 'Target language');
        $select.after($btn);

        // Hiển thị lại nút theo giá trị đang chọn của select
        function render() {
            const code = $select.val();
            $btn.html(`${flagImg(code)}<span class="lf-code">${label(code)}</span><i class="fa-solid fa-chevron-down lf-caret" aria-hidden="true"></i>`);
        }

        function openMenu() {
            closeMenu();
            const current = $select.val();
            const $menu = $(`<div class="lf-menu lf-theme-${currentTheme()}" role="listbox"></div>`);

            $select.find('option').each(function () {
                const code = this.value;
                const info = FLAGS[code] || { name: code };
                const $opt = $(`
                    <button type="button" class="lf-option" role="option" data-code="${code}">
                        ${flagImg(code)}
                        <span class="lf-code">${label(code)}</span>
                        <span class="lf-name">${info.name}</span>
                        <i class="fa-solid fa-check lf-check" aria-hidden="true"></i>
                    </button>`);
                if (code === current) $opt.addClass('is-selected').attr('aria-selected', 'true');
                $menu.append($opt);
            });

            $menu.on('click', '.lf-option', function () {
                const code = $(this).data('code');
                closeMenu();
                if (code !== $select.val()) {
                    $select.val(code).trigger('change');
                    // Nếu reading-viewer từ chối (trùng ngôn ngữ) nó tự đặt lại value -> vẽ lại
                    setTimeout(renderAll, 0);
                }
                $btn.trigger('focus');
            });

            $menu.on('keydown', function (e) {
                const $opts = $menu.find('.lf-option');
                const i = $opts.index(document.activeElement);
                if (e.key === 'ArrowDown') { e.preventDefault(); $opts.eq((i + 1) % $opts.length).trigger('focus'); }
                if (e.key === 'ArrowUp') { e.preventDefault(); $opts.eq((i - 1 + $opts.length) % $opts.length).trigger('focus'); }
                if (e.key === 'Escape') { e.preventDefault(); closeMenu(); $btn.trigger('focus'); }
            });

            // Gắn vào body + position: fixed để không bị thanh (overflow) cắt mất
            $('body').append($menu);
            const r = $btn[0].getBoundingClientRect();
            const mh = $menu.outerHeight();
            const mw = $menu.outerWidth();
            const openUp = r.bottom + mh + 8 > window.innerHeight;
            let left = Math.min(r.left, window.innerWidth - mw - 8);
            $menu.css({
                left: Math.max(8, left),
                top: openUp ? r.top - mh - 6 : r.bottom + 6
            });

            $menu.data('trigger', $btn);
            $btn.attr('aria-expanded', 'true');
            $openMenu = $menu;
            ($menu.find('.is-selected')[0] || $menu.find('.lf-option')[0]).focus();
        }

        $btn.on('click', function (e) {
            e.stopPropagation();
            if ($openMenu && $openMenu.data('trigger')[0] === $btn[0]) closeMenu();
            else openMenu();
        });

        $select.on('change', render);
        // reading-viewer.js thay toàn bộ <option> khi tải chương -> vẽ lại
        new MutationObserver(render).observe($select[0], { childList: true });

        $select.data('lf-render', render);
        render();
    }

    function renderAll() {
        ['srcLangSelect', 'targetLangSelect'].forEach(id => {
            const fn = $('#' + id).data('lf-render');
            if (fn) fn();
        });
    }

    // Đảo ngôn ngữ gốc <-> ngôn ngữ dịch
    function swapLanguages() {
        const $src = $('#srcLangSelect');
        const $tgt = $('#targetLangSelect');
        const a = $src.val(), b = $tgt.val();
        if (!a || !b || a === b) return;

        closeMenu();
        // Gán cả 2 giá trị trước rồi mới trigger, để reading-viewer không báo "trùng ngôn ngữ"
        $src.val(b);
        $tgt.val(a);
        $src.trigger('change');
        renderAll();

        const $btn = $('#btnSwapLang');
        $btn.removeClass('is-spin');
        void $btn[0].offsetWidth;   // chạy lại hiệu ứng xoay
        $btn.addClass('is-spin');
    }

    $(function () {
        buildPicker('srcLangSelect');
        buildPicker('targetLangSelect');

        $('#btnSwapLang').on('click', function (e) {
            e.stopPropagation();
            swapLanguages();
        });

        $(document).on('click', function (e) {
            if ($openMenu && !$(e.target).closest('.lf-menu').length) closeMenu();
        });
        $(window).on('resize scroll', closeMenu);
        $('#focusModeOverlay > div').on('scroll', closeMenu);
    });
})(jQuery);
