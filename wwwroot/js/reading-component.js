/* reading-component.js - Quản lý Reader song ngữ, chế độ Focus, trích xuất ảnh và Dải thanh Strip Gallery */

class ReadingViewer {
    constructor(options) {
        this.chapterId = options.chapterId;
        this.currentFontSize = 18;
        this.currentTheme = 'light';
        this.isFocusMode = false;
        this.isAllTranslated = false;
        this.chapterList = [];
        this.langMap = { 'en': '🇺🇸 EN', 'vi': '🇻🇳 VN', 'zh': '🇨🇳 ZH', 'ja': '🇯🇵 JP', 'fr': '🇫🇷 FR' };
        this.currentSrcLang = 'en';
        this.currentTargetLang = 'vi';
        this.rawParagraphData = [];
    }

    init() {
        this.bindEvents();
        this.loadChapterList();
        this.loadParagraphs();
    }

    // Tải danh sách các chương của bộ truyện
    loadChapterList() {
        const self = this;
        $.ajax({
            url: '/Paragraphs/GetListByChapter',
            type: 'GET',
            data: { chapterId: self.chapterId },
            success: function (response) {
                self.chapterList = response.chapters || response;
                if (response.storyTitle) {
                    $('#txtStoryTitle').text(response.storyTitle);
                }
                self.renderChapterSelect();
                self.updateChapterTitleText();
            },
            error: function () {
                console.error('Cannot load chapter list');
            }
        });
    }

    // Hiển thị danh sách chương vào thẻ Select
    renderChapterSelect() {
        let html = '';
        const isViMode = document.cookie.includes('c=vi-VN') || document.cookie.includes('uic=vi-VN');
        const defaultLabel = isViMode ? 'Chương' : 'Chapter';
        const chapterLabel = $('#mainWrapper').data('reader-chapter') || defaultLabel;

        this.chapterList.forEach(c => {
            const selected = c.chapterId == this.chapterId ? 'selected' : '';
            html += `<option value="${c.chapterId}" ${selected}>${chapterLabel} ${c.chapterNumber}</option>`;
        });
        $('#chapterSelect').html(html);
    }

    // Cập nhật tiêu đề chương đang hiển thị
    updateChapterTitleText() {
        const current = this.chapterList.find(c => c.chapterId == this.chapterId);
        if (current) {
            // Nếu current.title đã chứa sẵn "Chapter 1:" hoặc "Chương 1:" từ API, hiển thị trực tiếp
            if (current.title) {
                $('#txtChapterTitle').text(current.title);
            } else {
                const isViMode = document.cookie.includes('c=vi-VN') || document.cookie.includes('uic=vi-VN');
                const defaultLabel = isViMode ? 'Chương' : 'Chapter';
                const chapterLabel = $('#mainWrapper').data('reader-chapter') || defaultLabel;
                $('#txtChapterTitle').text(`${chapterLabel} ${current.chapterNumber}`);
            }
        }
    }

    // Điều hướng chuyển chương (Đầu, Trước, Sau, Cuối)
    goToChapter(action) {
        if (!this.chapterList || this.chapterList.length === 0) return;
        const currentIndex = this.chapterList.findIndex(c => c.chapterId == this.chapterId);
        let targetIndex = -1;

        if (action === 'first') targetIndex = 0;
        else if (action === 'prev') targetIndex = currentIndex - 1;
        else if (action === 'next') targetIndex = currentIndex + 1;
        else if (action === 'last') targetIndex = this.chapterList.length - 1;

        if (targetIndex >= 0 && targetIndex < this.chapterList.length) {
            this.switchChapter(this.chapterList[targetIndex].chapterId);
        }
    }

    // Chuyển đổi sang chương mới và tải lại nội dung
    switchChapter(newChapterId) {
        this.chapterId = newChapterId;
        if (typeof currentChapterId !== 'undefined') {
            currentChapterId = newChapterId;
        }
        $('#chapterSelect').val(this.chapterId);
        this.updateChapterTitleText();

        window.history.pushState({}, '', `?chapterId=${this.chapterId}`);
        this.loadParagraphs();

        $.post('/Paragraphs/UpdateProgress', { chapterId: this.chapterId }, function (res) {
            if (res && res.success) {
                console.log("Updated reading progress:", newChapterId);
            }
        });

        window.scrollTo({ top: 0, behavior: 'smooth' });
        if (this.isFocusMode) {
            $('#focusModeOverlay > div').scrollTop(0);
        }
    }

    // Tải danh sách đoạn văn của chương hiện tại
    loadParagraphs() {
        const self = this;
        $.ajax({
            url: '/Paragraphs/GetByChapter',
            type: 'GET',
            data: { chapterId: self.chapterId },
            success: function (data) {
                self.rawParagraphData = data || [];
                self.detectAvailableLanguages(self.rawParagraphData);
                self.renderParagraphs();

                if (self.isFocusMode) {
                    const headerHtml = $('#storyChapterHeader').prop('outerHTML');
                    $('#focusContentContainer').html(headerHtml + $('#paragraphList').html());
                }
            },
            error: function () {
                console.error('Cannot load chapter data');
            }
        });
    }

    // Tự động nhận diện các ngôn ngữ có dữ liệu trong chương
    detectAvailableLanguages(data) {
        if (!data || data.length === 0) {
            $('#languageSelectorGroup').addClass('hidden').removeClass('flex');
            return;
        }

        const hasContent = (str) => str && str.replace(/<[^>]*>/g, '').trim() !== '';
        const availableLangs = [];

        if (data.some(x => hasContent(x.english))) availableLangs.push('en');
        if (data.some(x => hasContent(x.vietnamese))) availableLangs.push('vi');
        if (data.some(x => hasContent(x.chinese))) availableLangs.push('zh');
        if (data.some(x => hasContent(x.japanese))) availableLangs.push('ja');
        if (data.some(x => hasContent(x.french))) availableLangs.push('fr');

        if (availableLangs.length <= 1) {
            $('#languageSelectorGroup').addClass('hidden').removeClass('flex');
            this.currentSrcLang = availableLangs[0] || 'en';
            return;
        }

        $('#languageSelectorGroup').removeClass('hidden').addClass('flex');

        if (!availableLangs.includes(this.currentSrcLang)) this.currentSrcLang = availableLangs[0];
        if (!availableLangs.includes(this.currentTargetLang) || this.currentTargetLang === this.currentSrcLang) {
            this.currentTargetLang = availableLangs.find(l => l !== this.currentSrcLang) || availableLangs[1];
        }

        let srcOptions = '';
        let targetOptions = '';
        availableLangs.forEach(code => {
            const label = this.langMap[code] || code.toUpperCase();
            srcOptions += `<option value="${code}" ${code === this.currentSrcLang ? 'selected' : ''}>${label}</option>`;
            targetOptions += `<option value="${code}" ${code === this.currentTargetLang ? 'selected' : ''}>${label}</option>`;
        });

        $('#srcLangSelect').html(srcOptions);
        $('#targetLangSelect').html(targetOptions);
    }

    // Dọn dẹp các thẻ p và br
    stripBlockTags(str) {
        if (!str) return '';
        return str.replace(/<\/?p[^>]*>/gi, '').replace(/<br\s*\/?>/gi, ' ').trim();
    }

    // Bóc tách toàn bộ 3 loại thẻ ảnh: Ảnh đơn, Panorama Strip và Multi-strip (3 ảnh độc lập)
    extractMediaInfo(text) {
        if (!text) return null;
        const plainText = text.replace(/<[^>]+>/g, ' ');

        // 1. Kiểm tra thẻ Multi-strip: [multistrip: url1 | url2 | url3 | caption: ...]
        const multiMatch = plainText.match(/\[multistrip\s*:\s*([^\]|]+)\s*\|\s*([^\]|]+)\s*\|\s*([^\]|]+)(?:\|\s*caption\s*:\s*([^\]]*))?\]/i);
        if (multiMatch) {
            return {
                type: 'multistrip',
                fullTag: multiMatch[0],
                urls: [multiMatch[1].trim(), multiMatch[2].trim(), multiMatch[3].trim()],
                count: 3,
                caption: multiMatch[4] ? multiMatch[4].trim() : ''
            };
        }

        // 2. Kiểm tra thẻ Panorama Strip: [strip: url | count: 3 | caption: ...]
        const stripMatch = plainText.match(/\[strip\s*:\s*([^\]|]+)(?:\|\s*count\s*:\s*(\d+))?(?:\|\s*caption\s*:\s*([^\]]*))?\]/i);
        if (stripMatch) {
            return {
                type: 'strip',
                fullTag: stripMatch[0],
                url: stripMatch[1].trim(),
                count: parseInt(stripMatch[2] || 3),
                caption: stripMatch[3] ? stripMatch[3].trim() : ''
            };
        }

        // 3. Kiểm tra thẻ ảnh đơn: [img: url | caption]
        const imgMatch = plainText.match(/\[img\s*:\s*([^\]|]+)(?:\|([^\]]*))?\]/i);
        if (imgMatch) {
            return {
                type: 'single',
                fullTag: imgMatch[0],
                url: imgMatch[1].trim(),
                count: 1,
                caption: imgMatch[2] ? imgMatch[2].trim() : ''
            };
        }

        return null;
    }

    // Escape chuỗi an toàn
    escapeHtml(text) {
        if (!text) return '';
        return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#039;");
    }

    // Trích xuất văn bản theo ngôn ngữ
    getParagraphTextByLang(pObj, langCode) {
        switch (langCode) {
            case 'en': return pObj.english || '';
            case 'vi': return pObj.vietnamese || '';
            case 'zh': return pObj.chinese || '';
            case 'ja': return pObj.japanese || '';
            case 'fr': return pObj.french || '';
            default: return pObj.english || '';
        }
    }

    // Render toàn bộ nội dung đọc truyện
    renderParagraphs() {
        const $container =$('#paragraphList');

        // 1. Chuẩn hóa mã 2 ký tự: 'vi' hoặc 'en'
        const isViMode = document.cookie.includes('c=vi-VN') ||
            document.cookie.includes('uic=vi-VN') ||
            $('html').attr('lang')?.toLowerCase().startsWith('vi');

        // Đồng bộ với this.currentSrcLang và this.currentTargetLang
        if (!this.currentSrcLang || !this.currentTargetLang) {
            this.currentSrcLang = isViMode ? 'vi' : 'en';
            this.currentTargetLang = isViMode ? 'en' : 'vi';
        }

        const activeSrcLang = this.currentSrcLang;
        const activeTargetLang = this.currentTargetLang;

        // 2. Thông báo khi chương rỗng
        if (!this.rawParagraphData || this.rawParagraphData.length === 0) {
            const noContentText = window.READER_LANG?.NoContent ||
                (isViMode ? 'Nội dung chương đang được cập nhật...' : 'This chapter has no content.');
            $container.html(`<div class="text-center text-gray-400 py-20 font-serif text-sm">${noContentText}</div>`);
            return;
        }

        let html = '';
        let isBlockOpen = false;
        let previousWasDialogue = false;

        this.rawParagraphData.forEach((p, index) => {
            // Lấy nội dung theo đúng mã 2 ký tự ('en', 'vi', ...)
            let rawSrc = this.getParagraphTextByLang(p, activeSrcLang) || '';
            let rawTarget = this.getParagraphTextByLang(p, activeTargetLang) || '';

            let blockType = 1;
            if (p.blockType !== undefined && p.blockType !== null) {
                blockType = parseInt(p.blockType);
            } else if (p.BlockType !== undefined && p.BlockType !== null) {
                blockType = parseInt(p.BlockType);
            } else if (index === 0) {
                blockType = 0;
            }

            const order = (p.paragraphOrder !== undefined && p.paragraphOrder !== null)
                ? p.paragraphOrder
                : ((p.ParagraphOrder !== undefined && p.ParagraphOrder !== null) ? p.ParagraphOrder : index);

            // Bóc tách Media từ ngôn ngữ nguồn hoặc fallback
            let srcMedia = this.extractMediaInfo(rawSrc);
            if (!srcMedia) {
                const fallbackOrder = isViMode
                    ? [p.vietnamese, p.english, p.chinese, p.japanese, p.french]
                    : [p.english, p.vietnamese, p.chinese, p.japanese, p.french];

                for (let item of fallbackOrder) {
                    const info = this.extractMediaInfo(item);
                    if (info) {
                        srcMedia = info;
                        break;
                    }
                }
            }

            let targetMedia = this.extractMediaInfo(rawTarget);
            let srcCaption = srcMedia ? srcMedia.caption : '';
            let targetCaption = targetMedia && targetMedia.caption ? targetMedia.caption : srcCaption;

            let mediaAfterHtml = '';

            // Nếu đoạn văn có chứa ảnh minh họa hoặc dải strip
            if (srcMedia && (srcMedia.url || (srcMedia.urls && srcMedia.urls.length))) {
                const sliderId = `readerStripSlider_${order}`;

                if (srcMedia.type === 'multistrip') {
                    let panelsHtml = '';
                    srcMedia.urls.forEach(u => {
                        panelsHtml += `
                        <div class="strip-panel-card shrink-0 h-[500px] rounded-2xl shadow-lg bg-cover bg-center cursor-pointer transition-transform duration-300 hover:scale-[1.02]"
                             style="width: calc((100% - 24px) / 3); background-image: url('${u}');">
                        </div>`;
                    });

                    const captionHtml = srcCaption
                        ? `<div class="story-illustration-caption text-center mt-2 text-xs text-gray-500 font-semibold" id="caption-${order}"><i class="fa-solid fa-camera mr-1"></i> <span>${srcCaption}</span></div>`
                        : `<div class="story-illustration-caption text-center mt-2 text-xs text-gray-500 font-semibold hidden" id="caption-${order}"><i class="fa-solid fa-camera mr-1"></i> <span></span></div>`;

                    mediaAfterHtml = `
                    <div class="strip-gallery-wrapper relative my-8 w-full">
                        <div id="${sliderId}" class="strip-gallery-slider flex items-center gap-3 overflow-x-auto scroll-smooth no-scrollbar py-2" style="height: 520px;">
                            ${panelsHtml}
                        </div>
                        ${captionHtml}
                    </div>`;

                } else if (srcMedia.type === 'strip') {
                    const count = srcMedia.count || 3;
                    let panelsHtml = '';

                    for (let idx = 0; idx < count; idx++) {
                        const posX = count > 1 ? (idx / (count - 1)) * 100 : 50;
                        panelsHtml += `
                        <div class="strip-panel-card shrink-0 h-[500px] rounded-2xl shadow-lg bg-no-repeat cursor-pointer transition-transform duration-300 hover:scale-[1.02]"
                             style="width: calc((100% - 24px) / 3); background-image: url('${srcMedia.url}'); background-size: ${count * 100}% 100%; background-position: ${posX}% center;">
                        </div>`;
                    }

                    const showNav = count > 3;
                    const captionHtml = srcCaption
                        ? `<div class="story-illustration-caption text-center mt-2 text-xs text-gray-500 font-semibold" id="caption-${order}"><i class="fa-solid fa-camera mr-1"></i> <span>${srcCaption}</span></div>`
                        : `<div class="story-illustration-caption text-center mt-2 text-xs text-gray-500 font-semibold hidden" id="caption-${order}"><i class="fa-solid fa-camera mr-1"></i> <span></span></div>`;

                    mediaAfterHtml = `
                    <div class="strip-gallery-wrapper relative my-8 w-full">
                        ${showNav ? `<button type="button" class="strip-nav-btn prev absolute left-3 top-1/2 -translate-y-1/2 z-20 w-10 h-10 rounded-full bg-black/60 text-white flex items-center justify-center hover:bg-black/90 shadow-lg" onclick="window.readingViewer.scrollReaderStrip('${sliderId}', -1)"><i class="fa-solid fa-chevron-left"></i></button>` : ''}
                        <div id="${sliderId}" class="strip-gallery-slider flex items-center gap-3 overflow-x-auto scroll-smooth no-scrollbar py-2" style="height: 520px;">
                            ${panelsHtml}
                        </div>
                        ${showNav ? `<button type="button" class="strip-nav-btn next absolute right-3 top-1/2 -translate-y-1/2 z-20 w-10 h-10 rounded-full bg-black/60 text-white flex items-center justify-center hover:bg-black/90 shadow-lg" onclick="window.readingViewer.scrollReaderStrip('${sliderId}', 1)"><i class="fa-solid fa-chevron-right"></i></button>` : ''}
                        ${captionHtml}
                    </div>`;

                } else {
                    const captionHtml = srcCaption
                        ? `<div class="story-illustration-caption" id="caption-${order}"><i class="fa-solid fa-camera"></i> <span>${srcCaption}</span></div>`
                        : `<div class="story-illustration-caption hidden" id="caption-${order}"><i class="fa-solid fa-camera"></i> <span></span></div>`;

                    mediaAfterHtml = `
                    <div class="story-illustration-block">
                        <img src="${srcMedia.url}" alt="${srcCaption || 'Illustration #' + order}" onerror="this.src='/assets/image/placeholder.png';">
                        ${captionHtml}
                    </div>`;
                }

                if (srcMedia.fullTag) {
                    rawSrc = rawSrc.replace(srcMedia.fullTag, '').trim();
                }
            }

            rawSrc = rawSrc.replace(/\[(img | strip | multistrip)[\s\S]*?\]/gi, '')
                .replace(/<[^>]*>\[(img | strip | multistrip)[\s\S]*?\]<\/[^>]*>/gi, '')
                .trim();

            rawTarget = rawTarget.replace(/\[(img | strip | multistrip)[\s\S]*?\]/gi, '')
                .replace(/<[^>]*>\[(img | strip | multistrip)[\s\S]*?\]<\/[^>]*>/gi, '')
                .trim();

            let cleanSrc = this.stripBlockTags(rawSrc);
            let cleanTarget = this.stripBlockTags(rawTarget);

            // Đồng bộ thẻ định dạng
            const formattingTags = ['strong', 'b', 'em', 'i', 'u', 's', 'strike'];
            formattingTags.forEach(tag => {
                const openRegex = new RegExp(`^<${tag}[^>]*>`, 'i');
                const closeRegex = new RegExp(`</${tag}>$`, 'i');

                if (openRegex.test(cleanSrc.trim()) && closeRegex.test(cleanSrc.trim())) {
                    if (!new RegExp(`<${tag}[^>]*>`, 'i').test(cleanTarget)) {
                        cleanTarget = `<${tag}>${cleanTarget}</${tag}>`;
                    }
                }
            });

            // Mở khối đoạn văn theo BlockType
            const shouldStartNewBlock = (index === 0) ||
                (blockType === 0 || blockType === 2 || blockType === 4) ||
                previousWasDialogue;

            if (shouldStartNewBlock) {
                if (isBlockOpen) {
                    html += '</div>';
                }

                let blockClass = 'para-block-start';
                if (blockType === 2) blockClass = 'para-block-indent';
                else if (blockType === 4) blockClass = 'para-block-dialogue';
                else if (blockType === 1 && previousWasDialogue) blockClass = 'para-block-indent';

                html += `<div class="${blockClass}">`;
                isBlockOpen = true;
            }

            // 3. In câu văn bản kèm tooltip song ngữ
            if (cleanSrc) {
                const tipText = isViMode
                    ? `Nhấp đúp chuột để xem câu đối chiếu #${order}`
                    : `Double-click to translate sentence #${order}`;

                html += `
                <span class="text-segment"
                      data-order="${order}"
                      data-lang="${activeSrcLang}"
                      data-src="${this.escapeHtml(cleanSrc)}"
                      data-target="${this.escapeHtml(cleanTarget)}"
                      data-src-caption="${this.escapeHtml(srcCaption)}"
                      data-target-caption="${this.escapeHtml(targetCaption)}"
                      title="${tipText}">
                    ${cleanSrc}
                </span> `;
            }

            previousWasDialogue = (blockType === 4);

            if (mediaAfterHtml) {
                if (isBlockOpen) {
                    html += '</div>';
                    isBlockOpen = false;
                }
                html += mediaAfterHtml;
                previousWasDialogue = false;
            }
        });

        if (isBlockOpen) {
            html += '</div>';
        }

        $container.html(html);
    }

    // Cuộn dải thanh Strip
    scrollReaderStrip(sliderId, direction) {
        const slider = document.getElementById(sliderId);
        if (!slider) return;

        const firstCard = slider.querySelector('.strip-panel-card');
        if (!firstCard) return;

        const scrollAmount = firstCard.offsetWidth + 12;
        slider.scrollBy({
            left: direction * scrollAmount,
            behavior: 'smooth'
        });
    }

    // Chuyển đổi ngôn ngữ của một câu khi nhấp đúp
      toggleLanguage($el, targetLang = null) {
        const currentLang = $el.attr('data-lang') || this.currentSrcLang;
        // Nếu không chỉ định targetLang, hoán đổi qua lại giữa Source và Target
        const nextLang = targetLang || (currentLang === this.currentSrcLang ? this.currentTargetLang : this.currentSrcLang);

        if (currentLang === nextLang) return;

        const isGoingToTarget = (nextLang === this.currentTargetLang);
        const newContent = isGoingToTarget ? $el.attr('data-target') :$el.attr('data-src');

        if (newContent !== undefined && newContent !== null && newContent !== '') {
            $el.html(newContent);$el.attr('data-lang', nextLang);
            $el.data('lang', nextLang);$el.toggleClass('text-teal-700 font-medium', isGoingToTarget);
        }

        const order = $el.attr('data-order');
        const $caption =$(`#caption-${order}`);
        if ($caption.length > 0) {
            const nextCaption = isGoingToTarget 
                ? $el.attr('data-target-caption') 
                : $el.attr('data-src-caption');
            if (nextCaption) {
                $caption.find('span').text(nextCaption);$caption.removeClass('hidden');
            }
        }
    }
    // Đăng ký các sự kiện tương tác
    bindEvents() {
        const self = this;

        $('#btnChapterFirst').on('click', () => self.goToChapter('first'));
        $('#btnChapterPrev').on('click', () => self.goToChapter('prev'));
        $('#btnChapterNext').on('click', () => self.goToChapter('next'));
        $('#btnChapterLast').on('click', () => self.goToChapter('last'));
        $('#chapterSelect').on('change', function () {
            self.switchChapter($(this).val());
        });

        $('#srcLangSelect, #targetLangSelect').on('change', function () {
            const newSrc = $('#srcLangSelect').val();
            const newTarget = $('#targetLangSelect').val();

            if (newSrc === newTarget) {
                showToast($('#mainWrapper').data('reader-translation-error') || 'Translation language must be different from the source language!', 'error');
                $('#srcLangSelect').val(self.currentSrcLang);
                $('#targetLangSelect').val(self.currentTargetLang);
                return;
            }

            self.currentSrcLang = newSrc;
            self.currentTargetLang = newTarget;
            self.isAllTranslated = false;
            $('#btnTranslateAll').removeClass('active').find('.btn-text').text($('#mainWrapper').data('reader-translate-all') || 'Translate all');

            self.renderParagraphs();

            if (self.isFocusMode) {
                const headerHtml = $('#storyChapterHeader').prop('outerHTML');
                $('#focusContentContainer').html(headerHtml + $('#paragraphList').html());
            }
        });

        $(document).off('dblclick', '.text-segment').on('dblclick', '.text-segment', function (e) {
            e.preventDefault();

            if (window.getSelection) {
                window.getSelection().removeAllRanges();
            }

            const $el = $(this);

            if (self.isFocusMode) {
                const order = $el.attr('data-order');
                const $original = $(`#paragraphList .text-segment[data-order="${order}"]`);
                self.toggleLanguage($el);
                self.toggleLanguage($original);
            } else {
                self.toggleLanguage($el);
            }
        });

        $('#btnTranslateAll').off('click').on('click', function () {
            self.isAllTranslated = !self.isAllTranslated;
            const $btn = $(this); $btn.toggleClass('active', self.isAllTranslated);

            $('#paragraphList .text-segment').each(function () {
                const $el = $(this);
                const currentLang = $el.data('lang');
                if (self.isAllTranslated && currentLang === self.currentSrcLang) {
                    self.toggleLanguage($el, self.currentTargetLang);
                } else if (!self.isAllTranslated && currentLang === self.currentTargetLang) {
                    self.toggleLanguage($el, self.currentSrcLang);
                }
            });

            if (self.isFocusMode) {
                const headerHtml = $('#storyChapterHeader').prop('outerHTML');
                $('#focusContentContainer').html(headerHtml + $('#paragraphList').html());
            }

            const srcLabel = self.langMap[self.currentSrcLang] || self.currentSrcLang.toUpperCase();
            const translateAllLabel = $('#mainWrapper').data('reader-translate-all') || 'Translate all';
            const showLabel = $('#mainWrapper').data('reader-show') || 'Show';
            $btn.find('.btn-text').text(self.isAllTranslated ? `${showLabel} ${srcLabel}` : translateAllLabel);
        });

        $('#btnFontDecrease').on('click', () => self.changeFontSize(-2));
        $('#btnFontIncrease').on('click', () => self.changeFontSize(2));

        $('#themeSelectorGroup button').on('click', function () {
            const theme = $(this).data('theme');
            self.changeTheme(theme);
        });

        $('#btnFocusMode').on('click', () => self.toggleFocusMode());
        $(document).on('keydown', (e) => {
            if (e.key === "Escape" && self.isFocusMode) {
                self.toggleFocusMode();
            }
        });
    }

    changeFontSize(step) {
        this.currentFontSize += step;
        if (this.currentFontSize < 14) this.currentFontSize = 14;
        if (this.currentFontSize > 32) this.currentFontSize = 32;

        $('#paragraphList').css('font-size', this.currentFontSize + 'px');
        $('#focusContentContainer').css('font-size', this.currentFontSize + 'px');
        $('#fontSizeDisplay').text(this.currentFontSize + 'px');
    }

    changeTheme(themeName) {
        this.currentTheme = themeName;
        const $wrapper = $('#mainWrapper');
        const $overlay = $('#focusModeOverlay');

        $wrapper.removeClass('theme-light theme-sepia theme-dark').addClass('theme-' + themeName); $overlay.removeClass('theme-light theme-sepia theme-dark').addClass('theme-' + themeName);
    }

    toggleFocusMode() {
        this.isFocusMode = !this.isFocusMode;
        const $overlay = $('#focusModeOverlay');
        const $container = $('#focusContentContainer');
        const $btn = $('#btnFocusMode');
        const $fixedBar = $('#fixedBottomBar');
        const $actionControls = $('#rightActionControls');

        if (this.isFocusMode) {
            this.changeTheme('light');

            const headerHtml = $('#storyChapterHeader').prop('outerHTML');
            $container.html(headerHtml + $('#paragraphList').html()).css('font-size', this.currentFontSize + 'px');

            $fixedBar.addClass('focus-layout-active'); $actionControls.addClass('focus-layout-active');

            $overlay.removeClass('hidden');
            requestAnimationFrame(() => {
                $overlay.addClass('block opacity-100');
            });

            $('body').addClass('overflow-hidden');
            $btn.addClass('bg-amber-500 text-white').find('.btn-text').text($('#mainWrapper').data('reader-exit-focus') || 'Exit Focus'); $btn.find('i').removeClass('fa-eye-slash').addClass('fa-eye');
        } else {
            $fixedBar.removeClass('focus-layout-active'); $actionControls.removeClass('focus-layout-active');

            $overlay.removeClass('opacity-100');
            setTimeout(() => {
                $overlay.removeClass('block').addClass('hidden'); $container.empty();
            }, 300);

            $('body').removeClass('overflow-hidden');
            $btn.removeClass('bg-amber-500 text-white').find('.btn-text').text($('#mainWrapper').data('reader-focus') || 'Focus Mode'); $btn.find('i').removeClass('fa-eye').addClass('fa-eye-slash');
        }
    }
}

// Khởi tạo đối tượng toàn cục
$(document).ready(function () {
    if (typeof currentChapterId !== 'undefined') {
        window.readingViewer = new ReadingViewer({ chapterId: currentChapterId });
        window.readingViewer.init();
    }
});