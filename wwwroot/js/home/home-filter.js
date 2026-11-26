/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/home-filter.js
 *  CHỨC NĂNG : Bộ lọc trang chủ: chọn thể loại nổi bật + sắp xếp
 *              "Mới nhất" (type=latest) / "Số thứ tự" theo mã #YYMMNNN
 *              (type=code, bấm lần nữa -> code_desc). Nhớ lựa chọn trên URL
 *              (?cat=..&sort=..) để F5 / chia sẻ link vẫn giữ bộ lọc.
 *  DÙNG TẠI  : Views/Home/Index.cshtml (HTML ở Components/StoriesList/StoriesList.cshtml)
 *  GIAO DIỆN : nút dùng class .cat-pill / .active-cat của trang Thể loại (categories.css)
 *  PHỤ THUỘC : jQuery, window.mainScroller (InfiniteScroller – infinite-scroll.js)
 * ============================================================================
 */
$(function () {
    const $box = $('#homeFilter');
    if (!$box.length) return;

    const state = { categoryId: '', type: 'latest' };

    // ---------- Đồng bộ giao diện ----------
    function paintCats() {
        $box.find('.hf-chip').each(function () {
            const on = String($(this).data('category') ?? '') === String(state.categoryId);
            $(this).toggleClass('active-cat', on).attr('aria-selected', on ? 'true' : 'false');
        });
    }

    function paintSort() {
        const isCode = state.type === 'code' || state.type === 'code_desc';
        $box.find('.hf-sort-btn').each(function () {
            const key = $(this).data('sort');
            const on = key === 'code' ? isCode : state.type === key;
            $(this).toggleClass('active-cat', on).attr('aria-pressed', on ? 'true' : 'false');
        });
        $box.find('.hf-dir')
            .toggleClass('fa-arrow-up-1-9', state.type !== 'code_desc')
            .toggleClass('fa-arrow-down-9-1', state.type === 'code_desc');
    }

    function syncUrl() {
        try {
            const url = new URL(window.location.href);
            state.categoryId ? url.searchParams.set('cat', state.categoryId) : url.searchParams.delete('cat');
            state.type !== 'latest' ? url.searchParams.set('sort', state.type) : url.searchParams.delete('sort');
            window.history.replaceState({}, '', url.pathname + url.search + url.hash);
        } catch (e) { /* trình duyệt cũ: bỏ qua */ }
    }

    // ---------- Tải lại danh sách ----------
    function apply(scroll) {
        paintCats();
        paintSort();
        syncUrl();

        const scroller = window.mainScroller;
        if (scroller && typeof scroller.reset === 'function') {
            scroller.reset({ categoryId: state.categoryId, type: state.type });
        }

        // Đưa bộ lọc lên đầu màn hình nếu người dùng đã cuộn xuống sâu
        if (scroll) {
            const top = $box.offset().top - 90;
            if (window.scrollY > top + 40) window.scrollTo({ top, behavior: 'smooth' });
        }
    }

    // ---------- Sự kiện ----------
    $box.on('click', '.hf-chip', function () {
        const id = String($(this).data('category') ?? '');
        if (id === String(state.categoryId)) return;
        state.categoryId = id;
        apply(true);

        // Cuộn chip đang chọn vào giữa thanh (trên điện thoại thanh cuộn ngang)
        this.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
    });

    $box.on('click', '.hf-sort-btn', function () {
        const key = $(this).data('sort');
        let next = key;
        if (key === 'code') {
            // Bấm "Số thứ tự" lần 2 -> đảo chiều tăng / giảm
            next = state.type === 'code' ? 'code_desc' : (state.type === 'code_desc' ? 'code' : 'code');
        }
        if (next === state.type) return;
        state.type = next;
        apply(true);
    });

    // ---------- Khôi phục bộ lọc từ URL (?cat=3&sort=code) ----------
    try {
        const qs = new URLSearchParams(window.location.search);
        const cat = qs.get('cat') || '';
        const sort = qs.get('sort') || 'latest';
        const catExists = !cat || $box.find(`.hf-chip[data-category="${CSS.escape(cat)}"]`).length > 0;
        if (catExists) state.categoryId = cat;
        if (['latest', 'code', 'code_desc'].includes(sort)) state.type = sort;
    } catch (e) { }

    if (state.categoryId || state.type !== 'latest') {
        // mainScroller được tạo trong cùng $(document).ready ở Index.cshtml -> chờ 1 nhịp
        setTimeout(() => apply(false), 0);
    } else {
        paintCats();
        paintSort();
    }
});
