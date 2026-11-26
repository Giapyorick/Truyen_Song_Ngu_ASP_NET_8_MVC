/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/stories/story-icons.js
 *  CHỨC NĂNG : Icon biến hình (morphicons) cho nút Thích & Theo dõi ở trang chi tiết truyện
 *              ♡ → ♥ (tô đặc) khi thích, bookmark → bookmark có dấu tích khi theo dõi
 *  DÙNG TẠI  : Views/Stories/Detail.cshtml (nạp dạng <script type="module">)
 *  PHỤ THUỘC : wwwroot/lib/morphicons/element.js (MIT, https://morphicons.com)
 *  GIAO TIẾP : window.storyIcons.setLike(bool) / setFollow(bool)
 *              – story-interaction.js gọi sau khi server trả kết quả
 * ----------------------------------------------------------------------------
 *  MỤC LỤC
 *    1. ĐƯỜNG VẼ ICON (lấy từ bộ Lucide, lưới 24x24)
 *    2. KHỞI TẠO <morph-icon> THEO TRẠNG THÁI BAN ĐẦU
 *    3. HÀM ĐỔI TRẠNG THÁI (có hiệu ứng nảy nhẹ)
 * ============================================================================
 */
import { defineMorphIcon } from "/lib/morphicons/element.js";

// ============================================================================
// 1. ĐƯỜNG VẼ ICON (lấy từ bộ Lucide, lưới 24x24)
// ============================================================================
const HEART_D = "M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z";
const BOOKMARK_D = "m19 21-7-4-7 4V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v16z";

const ICONS = {
    heart: [["path", { d: HEART_D }]],
    heartFilled: [["path", { d: HEART_D, fill: "currentColor" }]],
    bookmark: [["path", { d: BOOKMARK_D }]],
    bookmarkCheck: [["path", { d: BOOKMARK_D }], ["path", { d: "m9 10 2 2 4-4" }]]
};

// ============================================================================
// 2. KHỞI TẠO <morph-icon> THEO TRẠNG THÁI BAN ĐẦU
// ============================================================================
defineMorphIcon();

const likeEl = document.getElementById("iconLike");
const followEl = document.getElementById("iconFollow");

function initIcon(el, onIcon, offIcon) {
    if (!el) return;
    // Hiện ngay trạng thái hiện tại, không chạy hiệu ứng lúc tải trang
    el.set(el.dataset.active === "true" ? onIcon : offIcon);
}

initIcon(likeEl, ICONS.heartFilled, ICONS.heart);
initIcon(followEl, ICONS.bookmarkCheck, ICONS.bookmark);

// ============================================================================
// 3. HÀM ĐỔI TRẠNG THÁI (có hiệu ứng nảy nhẹ)
// ============================================================================
function toggleIcon(el, on, onIcon, offIcon) {
    if (!el) return;
    if ((el.dataset.active === "true") === !!on) return; // đã đúng trạng thái
    el.dataset.active = on ? "true" : "false";
    el.morphTo(on ? onIcon : offIcon, on ? "bouncy" : "snappy");

    // Nảy nhẹ khi bật (CSS .is-pop trong story-detail.css)
    if (on) {
        el.classList.remove("is-pop");
        void el.offsetWidth; // khởi động lại animation
        el.classList.add("is-pop");
    }
}

window.storyIcons = {
    setLike: (on) => toggleIcon(likeEl, on, ICONS.heartFilled, ICONS.heart),
    setFollow: (on) => toggleIcon(followEl, on, ICONS.bookmarkCheck, ICONS.bookmark)
};
