/**
 * Module xử lý Infinite Scroll chống trùng lặp dữ liệu
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

            // 1. Quét các story đã được nạp sẵn từ Server-side (nếu có)
            this.scanExistingCards();

            // Nếu trong grid đã có sẵn card từ view Server -> bắt đầu tính từ Page 1 (lần cuộn tới sẽ tải Page 2)
            // Nếu grid đang rỗng -> bắt đầu từ 0 và nạp ngay Page 1
            const initialCount = this.grid.find('.story-card').length;
            if (initialCount > 0) {
                this.currentPage = 1;
                // Nếu số lượng ban đầu ít hơn pageSize nghĩa là đã hết dữ liệu
                if (initialCount < this.pageSize) {
                    this.hasMore = false;
                    this.noMore.removeClass('hidden');
                }
            } else {
                this.currentPage = 0;
                this.loadNext();
            }

            this.initObserver();
        }

        scanExistingCards() {
            this.grid.find('.story-card').each((_, el) => {
                const sid = $(el).attr('data-story-id');
                if (sid) {
                    this.loadedStoryIds.add(String(sid));
                }
            });
        }

        initObserver() {
            if (!this.trigger) return;

            if (this.observer) {
                this.observer.disconnect();
            }

            this.observer = new IntersectionObserver((entries) => {
                if (entries[0].isIntersecting && !this.isLoading && this.hasMore && this.currentPage >= 1) {
                    this.loadNext();
                }
            }, {
                rootMargin: '200px'
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

            // ==================== DEBUG CHI TIẾT AI ĐANG GỌI ====================
            const callerName = this.params.categoryId ? `[THỂ LOẠI ID = ${this.params.categoryId}]` 
                             : (this.params.type ? `[XẾP HẠNG TYPE = ${this.params.type}]` : `[TRANG CHỦ DEFAULT]`);

            console.group(`%c${callerName} ĐANG GỌI TẢI Page = ${targetPage}`, 'color: #d946ef; font-weight: bold; font-size: 13px;');
            console.log('📌 Endpoint:', this.endpoint);
            console.log('📌 Query Data gửi đi:', queryData);
            console.trace('📌 Dấu vết gọi từ file/dòng nào:');
            console.groupEnd();

            $.ajax({
                url: this.endpoint,
                type: 'GET',
                data: queryData,
                success: (html) => {
                    this.currentPage = targetPage;

                    if (!html || !html.trim()) {
                        this.hasMore = false;
                        if (this.trigger && this.observer) this.observer.unobserve(this.trigger);
                        this.spinner.addClass('hidden');
                        this.noMore.removeClass('hidden');
                        this.isLoading = false;
                        console.groupEnd();
                        return;
                    }

                    const $dom =$('<div>').html(html);
                    const $incomingCards =$dom.find('.story-card');

                    if ($incomingCards.length === 0) {
                        this.hasMore = false;
                        if (this.trigger && this.observer) this.observer.unobserve(this.trigger);
                        this.spinner.addClass('hidden');
                        this.noMore.removeClass('hidden');
                        this.isLoading = false;
                        console.groupEnd();
                        return;
                    }

                    const elementsToAdd = [];
                    $incomingCards.each((_, el) => {
                        const sid = $(el).attr('data-story-id');
                        if (sid) {
                            if (!this.loadedStoryIds.has(String(sid))) {
                                this.loadedStoryIds.add(String(sid));
                                elementsToAdd.push(el);
                            }
                        } else {
                            elementsToAdd.push(el);
                        }
                    });

                    if (elementsToAdd.length > 0) {
                        const $newCards =$(elementsToAdd).css({ opacity: 0, transform: 'translateY(15px)' });
                        this.grid.append($newCards);

                        $newCards.animate(
                            { opacity: 1 },
                            {
                                duration: 250,
                                step: function (now, fx) {
                                    if (fx.prop === "opacity") {
                                        const translateY = (1 - now) * 15;
                                        $(this).css('transform', `translateY(${translateY}px)`);
                                    }
                                }
                            }
                        );
                    }

                    // Nếu số lượng card trả về ít hơn pageSize -> Đã hết dữ liệu
                    if ($incomingCards.length < this.pageSize) {
                        this.hasMore = false;
                        if (this.trigger && this.observer) this.observer.unobserve(this.trigger);
                        this.noMore.removeClass('hidden');
                    }

                    this.spinner.addClass('hidden');
                    this.isLoading = false;
                    console.log(`%c[TOTAL LOADED] Đang hiển thị: ${this.loadedStoryIds.size} truyện duy nhất`, 'color: #10b981; font-weight: bold;');
                    console.groupEnd();
                },
                error: (xhr) => {
                    console.error('[INFINITE SCROLL ERROR]', xhr.status, xhr.responseText);
                    this.spinner.addClass('hidden');
                    this.isLoading = false;
                    console.groupEnd();
                }
            });
        }

        reset(newParams) {
            if (newParams && typeof newParams.search === 'undefined') {
                newParams.search = '';
            }
            this.params = Object.assign({}, this.params, newParams);

            this.currentPage = 0;
            this.hasMore = true;
            this.isLoading = false;
            this.loadedStoryIds.clear();

            this.noMore.addClass('hidden');
            this.spinner.removeClass('hidden');
            this.grid.empty(); // Xóa sạch dữ liệu cũ trước khi nạp trang mới

            if (this.trigger && this.observer) {
                this.observer.disconnect();
                this.observer.observe(this.trigger);
            }

            this.loadNext();
        }
    };
}
$(document).ready(function () {
    // Chỉ bắt sự kiện ô tìm kiếm, KHÔNG tự động new InfiniteScroller ở đây nữa
    $(document).on('input keyup', 'input[type="search"], input[name="search"], #txtSearch, #searchInput, #topSearchInput', function () {
        const val = $(this).val();
        onSearchStory(val);
    });
});

function decodeHtml(html) {
    if (!html) return '';
    const txt = document.createElement("textarea");
    txt.innerHTML = html;
    return txt.value;
}

function onSearchStory(keyword) {
    if (!keyword) keyword = '';
    const cleanSearch = keyword.trim().normalize('NFC');
    const activeScroller = window.mainScroller || window.storyScroller || window.rankScroller;
    if (activeScroller) {
        activeScroller.reset({ search: cleanSearch });
    }
}