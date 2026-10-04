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

        loadNext() {
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

                    // Nếu số lượng truyện trả về ít hơn pageSize -> Đã hết
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
            // Hủy request AJAX cũ nếu đang chạy dở
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
            this.grid.empty();

            if (this.trigger && this.observer) {
                this.observer.disconnect();
                this.observer.observe(this.trigger);
            }

            this.loadNext();
        }
    };
}

// Bắt sự kiện ô tìm kiếm chung
$(document).ready(function () {
    $(document).on('input keyup', 'input[type="search"], input[name="search"], #txtSearch, #searchInput, #topSearchInput, #liveSearchInput', function () {
        const val = $(this).val();
        onSearchStory(val);
    });
});

function onSearchStory(keyword) {
    if (!keyword) keyword = '';
    const cleanSearch = keyword.trim().normalize('NFC');
    // Ưu tiên scroller của trang đang mở
    const activeScroller = window.storyScroller || window.rankScroller || window.mainScroller;
    if (activeScroller) {
        activeScroller.reset({ search: cleanSearch });
    }
}