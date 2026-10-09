/**
 * Module Infinite Scroll hợp nhất cho Home, Categories và Rankings
 */
if (typeof window.InfiniteScroller === 'undefined') {
    window.InfiniteScroller = class InfiniteScroller {
        constructor(config) {
            this.grid = $(config.gridSelector || '#mainStoryGrid');
            this.trigger = document.querySelector(config.triggerSelector || '#infiniteScrollTrigger');
            this.spinner = $(config.spinnerSelector || '#scrollSpinner');
            this.noMore = $(config.noMoreSelector || '#noMoreData');

            this.endpoint = config.endpoint || '/Stories/GetStoriesInfinite';
            this.pageSize = config.pageSize || 8;
            this.params = Object.assign({}, config.params || {});

            this.isLoading = false;
            this.hasMore = true;
            this.loadedStoryIds = new Set();
            this.currentPage = 0;
            this.currentXhr = null; // Quản lý hủy request cũ nếu chuyển tab nhanh

            // Quét các card đã có sẵn từ Server (nếu có)
            this.scanExistingCards();
            const initialCount = this.grid.find('.story-card').length;
            if (initialCount > 0) {
                this.currentPage = 1;
                if (initialCount < this.pageSize) {
                    this.stopScrolling();
                }
            } else {
                this.loadNext();
            }

            this.initObserver();
        }

        scanExistingCards() {
            this.grid.find('.story-card').each((_, el) => {
                const sid = $(el).attr('data-story-id');
                if (sid) this.loadedStoryIds.add(String(sid));
            });
        }

        initObserver() {
            if (!this.trigger) return;
            if (this.observer) this.observer.disconnect();

            this.observer = new IntersectionObserver((entries) => {
                if (entries[0].isIntersecting && !this.isLoading && this.hasMore && this.currentPage >= 1) {
                    this.loadNext();
                }
            }, {
                rootMargin: '150px'
            });

            this.observer.observe(this.trigger);
        }

        loadNext(isReset = false) {
            if (this.isLoading || !this.hasMore) return;

            this.isLoading = true;
            const targetPage = this.currentPage + 1;
            this.spinner.removeClass('hidden');

            const queryData = Object.assign({}, this.params, {
                page: targetPage,
                pageSize: this.pageSize
            });

            if (typeof queryData.search === 'undefined') {
                queryData.search = '';
            }

            this.currentXhr = $.ajax({
                url: this.endpoint,
                type: 'GET',
                data: queryData,
                success: (html) => {
                    this.currentPage = targetPage;

                    // Nếu là lượt reset sau tìm kiếm thì lúc này mới làm sạch lưới cũ
                    if (isReset) {
                        this.grid.empty();
                    }

                    if (!html || !html.trim()) {
                        this.stopScrolling();
                        return;
                    }

                    const $incoming = $('<div>').html(html);
                    const $cards = $incoming.find('.story-card');

                    if ($cards.length === 0) {
                        this.stopScrolling();
                        return;
                    }

                    const elementsToAdd = [];
                    $cards.each((_, el) => {
                        const sid = $(el).attr('data-story-id');
                        if (!sid || !this.loadedStoryIds.has(String(sid))) {
                            if (sid) this.loadedStoryIds.add(String(sid));
                            elementsToAdd.push(el);
                        }
                    });

                    if (elementsToAdd.length > 0) {
                        this.grid.append(elementsToAdd);
                    }

                    if ($cards.length < this.pageSize) {
                        this.stopScrolling();
                    }
                },
                error: (xhr) => {
                    if (xhr.statusText !== 'abort') {
                        this.spinner.addClass('hidden');
                    }
                },
                complete: () => {
                    this.spinner.addClass('hidden');
                    this.grid.removeClass('opacity-50'); // Khôi phục lại độ rõ
                    setTimeout(() => { this.isLoading = false; }, 100);
                }
            });
        }

        stopScrolling() {
            this.hasMore = false;
            if (this.trigger && this.observer) this.observer.unobserve(this.trigger);
            this.spinner.addClass('hidden');
            this.noMore.removeClass('hidden');
        }

        reset(newParams) {
            // 1. Hủy ngay request AJAX cũ nếu đang chạy dở để tránh giật kết quả cũ chèn vào mới
            if (this.currentXhr && this.currentXhr.readyState !== 4) {
                this.currentXhr.abort();
            }

            this.params = Object.assign({}, this.params, newParams);
            this.currentPage = 0;
            this.hasMore = true;
            this.isLoading = false;
            this.loadedStoryIds.clear();

            this.noMore.addClass('hidden');
            this.spinner.removeClass('hidden');

            // 2. KHÔNG DÙNG this.grid.empty() NGAY (tránh giật trắng màn hình)
            // Thay vào đó, chỉ làm mờ nhẹ lưới để báo cho người dùng biết đang tải
            this.grid.addClass('opacity-50 transition-opacity duration-200');

            if (this.trigger && this.observer) {
                this.observer.disconnect();
                this.observer.observe(this.trigger);
            }

            // Gọi tải trang mới
            this.loadNext(true); // Truyền cờ isReset = true
        }
    };
}

// Bắt sự kiện ô tìm kiếm chung
$(document).ready(function () {$(document).on('input keyup', 'input[type="search"], input[name="search"], #txtSearch, #searchInput, #topSearchInput, #liveSearchInput', function () {
        const val = $(this).val();
        onSearchStory(val);
    });
});

// KHẮC PHỤC LỖI: Gắn biến vào window thay vì dùng let toàn cục
window.globalSearchDebounceTimer = window.globalSearchDebounceTimer || null;

function onSearchStory(keyword) {
    if (!keyword) keyword = '';
    const cleanSearch = keyword.trim().normalize('NFC');

    // Xóa bộ hẹn giờ trước đó
    clearTimeout(window.globalSearchDebounceTimer);

    // Chờ 800ms sau khi người dùng dừng gõ hẳn thì mới kích hoạt tìm kiếm
    window.globalSearchDebounceTimer = setTimeout(() => {
        const activeScroller = window.storyScroller || window.rankScroller || window.mainScroller;

        // Cập nhật tiêu đề mục kết quả nếu có
        const $title =$('.section-title');
        if ($title.length > 0) {
            if (cleanSearch.length > 0) {
                $title.html(`Kết quả tìm kiếm cho: <span class="text-teal-600">"${cleanSearch}"</span>`);
            } else {
                $title.text('Truyện mới cập nhật');
            }
        }

        if (activeScroller) {
            activeScroller.reset({ search: cleanSearch });
        }
    }, 800); // SỬA LẠI: Thay 10000 thành 800 (800ms = 0.8 giây)
}