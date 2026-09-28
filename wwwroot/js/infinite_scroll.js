/**
 * Module xử lý Infinite Scroll có Delay 2s và Fade-In mượt mà
 */
class InfiniteScroller {
    constructor(config) {
        this.grid = $(config.gridSelector || '#mainStoryGrid');
        this.trigger = document.querySelector(config.triggerSelector || '#infiniteScrollTrigger');
        this.spinner = $(config.spinnerSelector || '#scrollSpinner');
        this.noMore = $(config.noMoreSelector || '#noMoreData');
        
        this.endpoint = config.endpoint || '/Stories/GetStoriesInfinite';
        this.pageSize = config.pageSize || 8;
        this.params = config.params || {};
        
        this.currentPage = 1;
        this.isLoading = false;
        this.hasMore = true;

        this.initObserver();
    }

    initObserver() {
        if (!this.trigger) return;

        this.observer = new IntersectionObserver((entries) => {
            if (entries[0].isIntersecting && !this.isLoading && this.hasMore) {
                this.loadNext();
            }
        }, {
            // Giảm margin xuống 50px hoặc 0px để người dùng thấy rõ phần spinner loading dưới đáy
            rootMargin: '50px'
        });

        this.observer.observe(this.trigger);
    }

    loadNext() {
        this.isLoading = true;
        this.currentPage++;
        
        // Hiện spinner quay quay
        this.spinner.removeClass('hidden');

        const queryData = Object.assign({}, this.params, {
            page: this.currentPage,
            pageSize: this.pageSize
        });

        $.ajax({
            url: this.endpoint,
            type: 'GET',
            data: queryData,
            success: (html) => {
                // ĐỢI ĐÚNG 2 GIÂY (2000ms) để người dùng thấy rõ hiệu ứng đang tải
                setTimeout(() => {
                    if (!html || html.trim() === '') {
                        this.hasMore = false;
                        if (this.trigger) this.observer.unobserve(this.trigger);
                        this.spinner.addClass('hidden');
                        this.noMore.removeClass('hidden');
                        this.isLoading = false;
                        return;
                    }

                    // Bọc các thẻ truyện mới vào một wrapper tạm để làm hiệu ứng fade-in từ từ
                    const $newCards =$(html).css({ opacity: 0, transform: 'translateY(20px)' });
                    
                    // Nối vào danh sách
                    this.grid.append($newCards);

                    // Hiệu ứng hiện từ từ (Fade In + trượt nhẹ lên)
                    $newCards.animate(
                        { opacity: 1 }, 
                        {
                            duration: 500,
                            step: function (now, fx) {
                                if (fx.prop === "opacity") {
                                    const translateY = (1 - now) * 20;
                                    $(this).css('transform', `translateY(${translateY}px)`);
                                }
                            }
                        }
                    );

                    this.spinner.addClass('hidden');
                    this.isLoading = false;
                }, 2000);
            },
            error: (xhr) => {
                console.error("Error loading infinite scroll:", xhr.status);
                this.spinner.addClass('hidden');
                this.isLoading = false;
            }
        });
    }

    reset(newParams) {
        this.params = Object.assign(this.params, newParams);
        this.currentPage = 1;
        this.hasMore = true;
        this.isLoading = true;

        this.noMore.addClass('hidden');
        this.spinner.removeClass('hidden');
        this.grid.empty();

        if (this.trigger) {
            this.observer.disconnect();
            this.observer.observe(this.trigger);
        }

        const queryData = Object.assign({}, this.params, {
            page: 1,
            pageSize: this.pageSize
        });

        $.ajax({
            url: this.endpoint,
            type: 'GET',
            data: queryData,
            success: (html) => {
                if (html && html.trim() !== '') {
                    const $cards =$(html).css({ opacity: 0 });
                    this.grid.html($cards);$cards.animate({ opacity: 1 }, 400);
                } else {
                    this.grid.html(`
                        <div class="col-span-full bg-white rounded-3xl p-16 text-center border border-gray-100 space-y-3">
                            <div class="w-16 h-16 mx-auto rounded-full bg-teal-50 text-teal-400 flex items-center justify-center text-2xl">
                                <i class="fas fa-folder-open"></i>
                            </div>
                            <h3 class="font-bold text-gray-700 text-lg">No stories found</h3>
                            <p class="text-xs text-gray-400 max-w-sm mx-auto">
                                This category currently has no stories available. Please select another category!
                            </p>
                        </div>
                    `);
                    this.hasMore = false;
                    if (this.trigger) this.observer.unobserve(this.trigger);
                }
            },
            complete: () => {
                this.isLoading = false;
                this.spinner.addClass('hidden');
            }
        });
    }
}