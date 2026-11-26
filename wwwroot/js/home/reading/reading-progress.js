/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/reading/reading-progress.js
 *  CHỨC NĂNG : Vẽ đường viền tiến độ đọc chạy quanh thanh điều khiển (#fixedBottomBar).
 *              Viền chạy đủ 1 vòng = đã đọc hết chương.
 *  DÙNG TẠI  : Views/Paragraphs/Content.cshtml (nạp SAU reading-viewer.js)
 *  GHI CHÚ   : Không phụ thuộc ReadingViewer; tự theo dõi cuộn ở trang thường
 *              (window) và ở chế độ Focus (#focusModeOverlay > div).
 * ============================================================================
 */
(function () {
    const SVG_NS = 'http://www.w3.org/2000/svg';
    const STROKE = 3;

    function init() {
        const bar = document.getElementById('fixedBottomBar');
        if (!bar) return;

        // ---- Tạo SVG viền ----
        const svg = document.createElementNS(SVG_NS, 'svg');
        svg.setAttribute('class', 'reader-progress-ring');
        svg.setAttribute('aria-hidden', 'true');
        svg.innerHTML = `
            <defs>
                <linearGradient id="readerProgressGrad" x1="0" y1="0" x2="1" y2="1">
                    <stop offset="0%" stop-color="#50C9C3"/>
                    <stop offset="100%" stop-color="#96DEDA"/>
                </linearGradient>
            </defs>
            <rect class="ring-track" pathLength="100"></rect>
            <rect class="ring-fill" pathLength="100" stroke="url(#readerProgressGrad)"></rect>`;
        bar.appendChild(svg);

        const rects = svg.querySelectorAll('rect');
        const fill = svg.querySelector('.ring-fill');
        const scroller = document.querySelector('#focusModeOverlay > div');

        // Cho thanh có thanh tiến độ (aria) để trình đọc màn hình biết
        bar.setAttribute('role', 'progressbar');
        bar.setAttribute('aria-valuemin', '0');
        bar.setAttribute('aria-valuemax', '100');

        // ---- Khớp kích thước viền với thanh (kể cả khi đổi layout Focus) ----
        function fitRing() {
            const w = bar.clientWidth;
            const h = bar.clientHeight;
            const radius = parseFloat(getComputedStyle(bar).borderTopLeftRadius) || 16;
            svg.setAttribute('viewBox', `0 0 ${w} ${h}`);
            rects.forEach(r => {
                r.setAttribute('x', STROKE / 2);
                r.setAttribute('y', STROKE / 2);
                r.setAttribute('width', Math.max(0, w - STROKE));
                r.setAttribute('height', Math.max(0, h - STROKE));
                r.setAttribute('rx', Math.max(0, radius - STROKE / 2));
            });
        }

        // ---- Tính % đã đọc ----
        function isFocus() {
            return bar.classList.contains('focus-layout-active');
        }

        function getProgress() {
            let top, max;
            if (isFocus() && scroller) {
                top = scroller.scrollTop;
                max = scroller.scrollHeight - scroller.clientHeight;
            } else {
                const article = document.getElementById('mainWrapper');
                if (!article) return 0;
                // Hết chương = đáy khung đọc chạm đáy màn hình
                const rect = article.getBoundingClientRect();
                const total = article.offsetHeight - window.innerHeight;
                top = -rect.top;
                max = total;
            }
            if (max <= 0) return 100; // chương ngắn, không cần cuộn
            return Math.min(100, Math.max(0, (top / max) * 100));
        }

        let ticking = false;
        function update() {
            ticking = false;
            const p = getProgress();
            fill.style.strokeDashoffset = (100 - p).toFixed(2);
            bar.classList.toggle('reading-done', p >= 99.5);
            bar.setAttribute('aria-valuenow', Math.round(p));
        }
        function requestUpdate() {
            if (!ticking) {
                ticking = true;
                requestAnimationFrame(update);
            }
        }

        window.addEventListener('scroll', requestUpdate, { passive: true });
        window.addEventListener('resize', requestUpdate);
        if (scroller) scroller.addEventListener('scroll', requestUpdate, { passive: true });

        // Thanh đổi kích thước (vào/ra Focus, đổi cỡ chữ...) -> vẽ lại viền
        if ('ResizeObserver' in window) {
            new ResizeObserver(() => { fitRing(); requestUpdate(); }).observe(bar);
            // Nội dung chương tải xong / đổi ngôn ngữ làm thay đổi chiều cao
            const list = document.getElementById('paragraphList');
            if (list) new ResizeObserver(requestUpdate).observe(list);
        }
        // Vào/ra Focus: đổi class -> cập nhật sau khi layout chuyển xong
        new MutationObserver(() => {
            fitRing();
            requestUpdate();
            setTimeout(() => { fitRing(); requestUpdate(); }, 500);
        }).observe(bar, { attributes: true, attributeFilter: ['class'] });

        fitRing();
        update();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
