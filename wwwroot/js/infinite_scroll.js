/**
 * Module xử lý Infinite Scroll đồng bộ từ Page 1
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
            this.params = config.params || {};

            // Bắt đầu từ 0 để lượt load đầu tiên tải đúng Page = 1
            this.currentPage = 0;
            this.isLoading = false;
            this.hasMore = true;

            this.loadedStoryIds = new Set();

            this.initObserver();

            // Tải trang 1 ngay khi vừa vào trang
            this.loadNext();
        }

        initObserver() {
            if (!this.trigger) return;

            this.observer = new IntersectionObserver((entries) => {
                if (entries[0].isIntersecting && !this.isLoading && this.hasMore && this.currentPage > 0) {
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
            this.currentPage++;

            this.spinner.removeClass('hidden');

            const queryData = Object.assign({}, this.params, {
                page: this.currentPage,
                pageSize: this.pageSize
            });

            console.group(`%c[AJAX REQUEST] Đang tải Page = ${this.currentPage}`, 'color: #8b5cf6; font-weight: bold;');

            $.ajax({
                url: this.endpoint,
                type: 'GET',
                data: queryData,
                success: (html) => {
                    setTimeout(() => {
                        if (!html || !html.trim()) {
                            this.hasMore = false;
                            if (this.trigger) this.observer.unobserve(this.trigger);
                            this.spinner.addClass('hidden');
                            this.noMore.removeClass('hidden');
                            this.isLoading = false;
                            console.groupEnd();
                            return;
                        }

                        const $dom = $('<div>').html(html);
                        const $incomingCards = $dom.find('.story-card');

                        if ($incomingCards.length === 0) {
                            this.hasMore = false;
                            if (this.trigger) this.observer.unobserve(this.trigger);
                            this.spinner.addClass('hidden');
                            this.noMore.removeClass('hidden');
                            this.isLoading = false;
                            console.groupEnd();
                            return;
                        }

                        const elementsToAdd = [];
                        $incomingCards.each((_, el) => {
                            const storyId = $(el).attr('data-story-id');
                            if (storyId) {
                                if (!this.loadedStoryIds.has(String(storyId))) {
                                    this.loadedStoryIds.add(String(storyId));
                                    elementsToAdd.push(el);
                                }
                            } else {
                                elementsToAdd.push(el);
                            }
                        });

                        if (elementsToAdd.length > 0) {
                            const $newCards = $(elementsToAdd).css({ opacity: 0, transform: 'translateY(15px)' });
                            this.grid.append($newCards);

                            $newCards.animate(
                                { opacity: 1 },
                                {
                                    duration: 300,
                                    step: function (now, fx) {
                                        if (fx.prop === "opacity") {
                                            const translateY = (1 - now) * 15;
                                            $(this).css('transform', `translateY(${translateY}px)`);
                                        }
                                    }
                                }
                            );
                        }

                        // Nếu số lượng card nhận về ít hơn pageSize -> Đây là trang cuối cùng
                        if ($incomingCards.length < this.pageSize) {
                            this.hasMore = false;
                            if (this.trigger) this.observer.unobserve(this.trigger);
                            this.noMore.removeClass('hidden');
                        }

                        this.spinner.addClass('hidden');
                        this.isLoading = false;
                        console.log(`%c[TỔNG SỐ HIỆN TẠI] Đang hiển thị: ${this.loadedStoryIds.size} truyện`, 'color: #059669; font-weight: bold;');
                        console.groupEnd();
                    }, 250);
                },
                error: (xhr) => {
                    console.error('[AJAX ERROR]', xhr.status);
                    this.spinner.addClass('hidden');
                    this.isLoading = false;
                    console.groupEnd();
                }
            });
        }

        reset(newParams) {
            this.params = Object.assign(this.params, newParams);
            this.currentPage = 0;
            this.hasMore = true;
            this.isLoading = false;
            this.loadedStoryIds.clear();

            this.noMore.addClass('hidden');
            this.spinner.removeClass('hidden');
            this.grid.empty();

            if (this.trigger) {
                this.observer.disconnect();
                this.observer.observe(this.trigger);
            }

            this.loadNext();
        }
    };
}