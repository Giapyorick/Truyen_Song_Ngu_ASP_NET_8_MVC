/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/common/feed-ads.js
 *  CHỨC NĂNG : Chèn 1 hàng quảng cáo ngang (Google AdSense) sau mỗi N hàng truyện
 *              trong lưới #mainStoryGrid (mặc định N = 4, đọc từ #adConfig).
 *  CÁCH LÀM  : Đếm số cột thật của lưới (4 desktop / 3 tablet / 2 điện thoại) nên
 *              "4 hàng" luôn đúng trên mọi màn hình. Tự chạy lại khi cuộn vô hạn
 *              nạp thêm truyện hoặc khi tìm kiếm thay toàn bộ lưới.
 *  CẤU HÌNH  : appsettings.json > "GoogleAdSense"; <div id="adConfig"> ở _Layout.cshtml
 *  DÙNG TẠI  : Views/Shared/_Layout.cshtml (nạp sau infinite-scroll.js)
 * ============================================================================
 */
(function () {
    function init() {
        var cfgEl = document.getElementById('adConfig');
        var grid = document.getElementById('mainStoryGrid');
        if (!cfgEl || !grid) return;

        var cfg = {
            enabled: cfgEl.dataset.enabled === 'true' && !!cfgEl.dataset.slot,
            client: cfgEl.dataset.client,
            slot: cfgEl.dataset.slot,
            rows: Math.max(1, parseInt(cfgEl.dataset.rows, 10) || 4),
            label: cfgEl.dataset.label || 'Advertisement'
        };

        // Số cột hiện tại của lưới truyện
        function columnCount() {
            var cols = getComputedStyle(grid).gridTemplateColumns.split(' ').filter(Boolean).length;
            return cols > 0 ? cols : 4;
        }

        function createAd() {
            var wrap = document.createElement('div');
            wrap.className = 'ad-slot ad-slot--feed';

            var label = document.createElement('span');
            label.className = 'ad-label';
            label.textContent = cfg.label;
            wrap.appendChild(label);

            if (cfg.enabled) {
                var ins = document.createElement('ins');
                ins.className = 'adsbygoogle';
                ins.style.display = 'block';
                ins.setAttribute('data-ad-client', cfg.client);
                ins.setAttribute('data-ad-slot', cfg.slot);
                ins.setAttribute('data-ad-format', 'horizontal');
                ins.setAttribute('data-full-width-responsive', 'true');
                wrap.appendChild(ins);
            } else {
                // Máy dev (chưa có mã AdSense): khung giữ chỗ để xem bố cục
                var ph = document.createElement('div');
                ph.className = 'ad-placeholder';
                ph.textContent = 'In-feed ad · 1 hàng / ' + cfg.rows + ' hàng truyện';
                wrap.appendChild(ph);
            }
            return wrap;
        }

        var busy = false;
        function placeAds() {
            if (busy) return;
            busy = true;

            var every = columnCount() * cfg.rows;          // số truyện giữa 2 quảng cáo
            var cards = grid.querySelectorAll(':scope > .story-card');

            for (var i = every - 1; i < cards.length; i += every) {
                var card = cards[i];
                var next = card.nextElementSibling;
                if (next && next.classList.contains('ad-slot--feed')) continue;   // đã có quảng cáo
                if (i === cards.length - 1 && !document.getElementById('noMoreData')?.classList.contains('hidden')) {
                    continue;   // hết truyện: không để quảng cáo đứng cuối danh sách
                }

                var ad = createAd();
                card.after(ad);
                if (cfg.enabled) {
                    try { (window.adsbygoogle = window.adsbygoogle || []).push({}); } catch (e) { /* bị chặn quảng cáo */ }
                }
            }
            busy = false;
        }

        // Lưới thay đổi (nạp thêm / tìm kiếm) -> chèn lại cho đúng
        new MutationObserver(function () {
            requestAnimationFrame(placeAds);
        }).observe(grid, { childList: true });

        placeAds();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
