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
                console.error('Không thể tải danh sách chương');
            }
        });
    }

    renderChapterSelect() {
        let html = '';
        this.chapterList.forEach(c => {
            const selected = c.chapterId == this.chapterId ? 'selected' : '';
            html += `<option value="${c.chapterId}" ${selected}>Chương ${c.chapterNumber}</option>`;
        });
        $('#chapterSelect').html(html);
    }

    updateChapterTitleText() {
        const current = this.chapterList.find(c => c.chapterId == this.chapterId);
        if (current) {
            $('#txtChapterTitle').text(`Chương ${current.chapterNumber}: ${current.title || ''}`);
        }
    }

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
                console.log("Đã cập nhật tiến trình đọc:", newChapterId);
            }
        });

        window.scrollTo({ top: 0, behavior: 'smooth' });
        if (this.isFocusMode) {
            $('#focusModeOverlay > div').scrollTop(0);
        }
    }

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
                console.error('Không thể nạp dữ liệu chương');
            }
        });
    }

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

    stripBlockTags(str) {
        if (!str) return '';
        return str.replace(/<\/?p[^>]*>/gi, '').replace(/<br\s*\/?>/gi, ' ').trim();
    }

    extractImgInfo(text) {
        if (!text) return null;
        const plainText = text.replace(/<[^>]+>/g, ' ');
        const match = plainText.match(/\[img\s*:\s*([^\]|]+)(?:\|([^\]]*))?\]/i);
        if (!match) return null;

        return {
            fullTag: match[0],
            url: match[1].trim(),
            caption: match[2] ? match[2].trim() : ''
        };
    }

    escapeHtml(text) {
        if (!text) return '';
        return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#039;");
    }

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

    renderParagraphs() {
        const $container = $('#paragraphList');

        if (!this.rawParagraphData || this.rawParagraphData.length === 0) {
            $container.html('<div class="text-center text-gray-400 py-20">Chapter này chưa có nội dung.</div>');
            return;
        }

        let html = '';
        let isBlockOpen = false;
        let previousWasDialogue = false;

        this.rawParagraphData.forEach((p, index) => {
            let rawSrc = this.getParagraphTextByLang(p, this.currentSrcLang) || '';
            let rawTarget = this.getParagraphTextByLang(p, this.currentTargetLang) || '';

            let blockType = 1;
            if (p.blockType !== undefined && p.blockType !== null) {
                blockType = parseInt(p.blockType);
            } else if (p.BlockType !== undefined && p.BlockType !== null) {
                blockType = parseInt(p.BlockType);
            } else if (index === 0) {
                blockType = 0;
            }

            const order = p.paragraphOrder || p.ParagraphOrder || (index + 1);

            let currentImgInfo = this.extractImgInfo(rawSrc);
            let finalUrl = '';
            let srcCaption = '';

            if (currentImgInfo) {
                finalUrl = currentImgInfo.url;
                srcCaption = currentImgInfo.caption;
            } else {
                const fallbackOrder = [p.english, p.vietnamese, p.chinese, p.japanese, p.french];
                for (let item of fallbackOrder) {
                    const info = this.extractImgInfo(item);
                    if (info) {
                        finalUrl = info.url;
                        srcCaption = info.caption;
                        break;
                    }
                }
            }

            if (finalUrl && !srcCaption) {
                const vnInfo = this.extractImgInfo(p.vietnamese);
                const enInfo = this.extractImgInfo(p.english);
                if (vnInfo && vnInfo.caption) srcCaption = vnInfo.caption;
                else if (enInfo && enInfo.caption) srcCaption = enInfo.caption;
            }

            let targetImgInfo = this.extractImgInfo(rawTarget);
            let targetCaption = targetImgInfo && targetImgInfo.caption ? targetImgInfo.caption : srcCaption;
            if (!targetCaption && p.vietnamese) {
                const vnFallback = this.extractImgInfo(p.vietnamese);
                if (vnFallback && vnFallback.caption) targetCaption = vnFallback.caption;
            }

            let imgAfterHtml = '';

            if (finalUrl) {
                const captionHtml = srcCaption
                    ? `<div class="story-illustration-caption" id="caption-${order}"><i class="fa-solid fa-camera"></i> <span>${srcCaption}</span></div>`
                    : `<div class="story-illustration-caption hidden" id="caption-${order}"><i class="fa-solid fa-camera"></i> <span></span></div>`;

                // Luôn gom ảnh vào imgAfterHtml để render sau đoạn
                imgAfterHtml = `
                    <div class="story-illustration-block">
                        <img src="${finalUrl}" alt="${srcCaption || 'Illustration #' + order}" onerror="this.src='/assets/image/placeholder.png';">
                        ${captionHtml}
                    </div>
                `;

                // Xóa sạch mã [img:...] khỏi nội dung văn bản dù nằm ở vị trí nào
                rawSrc = rawSrc.replace(/\[img[\s\S]*?\]/gi, '').replace(/<[^>]*>\[img[\s\S]*?\]<\/[^>]*>/gi, '').trim();
            }

            rawTarget = rawTarget.replace(/\[img[\s\S]*?\]/gi, '').replace(/<[^>]*>\[img[\s\S]*?\]<\/[^>]*>/gi, '').trim();

            let cleanSrc = this.stripBlockTags(rawSrc);
            let cleanTarget = this.stripBlockTags(rawTarget);

            // Tự động sao chép các thẻ lồng nhau (strong, b, em, i, u, s...) sang câu dịch
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

            // Logic mở khối chữ theo BlockType (0, 2, 4)
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

            // In nội dung chữ
            if (cleanSrc) {
                html += `<span class="text-segment"
                              data-order="${order}"
                              data-lang="${this.currentSrcLang}"
                              data-src="${this.escapeHtml(cleanSrc)}"
                              data-target="${this.escapeHtml(cleanTarget)}"
                              data-src-caption="${this.escapeHtml(srcCaption)}"
                              data-target-caption="${this.escapeHtml(targetCaption)}"
                              title="Nháy đúp để dịch câu #${order}">
                            ${cleanSrc}
                         </span> `;
            }

            previousWasDialogue = (blockType === 4);

            // Luôn luôn xử lý ảnh sau khối chữ
            if (imgAfterHtml) {
                // Đóng khối chữ trước khi chèn ảnh để ảnh không bị dính thụt lề 1cm
                if (isBlockOpen) {
                    html += '</div>';
                    isBlockOpen = false;
                }
                html += imgAfterHtml;
                previousWasDialogue = false; // Reset cờ để câu tiếp theo không bị nối sai dòng thoại
            }
        });

        if (isBlockOpen) {
            html += '</div>';
        }

        $container.html(html);
    }

    toggleLanguage($el, targetLang = null) {
        const currentLang = $el.data('lang');
        const nextLang = targetLang || (currentLang === this.currentSrcLang ? this.currentTargetLang : this.currentSrcLang);
        if (currentLang === nextLang) return;

        let newContent = nextLang === this.currentTargetLang ? $el.attr('data-target') : $el.attr('data-src');

        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = newContent || '';

        $el.html(tempDiv.innerHTML);
        $el.data('lang', nextLang);

        const order = $el.attr('data-order');
        const $caption = $(`#caption-${order}`);
        if ($caption.length > 0) {
            const nextCaption = nextLang === this.currentTargetLang ? $el.attr('data-target-caption') : $el.attr('data-src-caption');
            if (nextCaption) {
                $caption.find('span').text(nextCaption);
                $caption.removeClass('hidden');
            }
        }
    }

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
                alert("Ngôn ngữ dịch phải khác ngôn ngữ hiển thị ban đầu!");
                $('#srcLangSelect').val(self.currentSrcLang);
                $('#targetLangSelect').val(self.currentTargetLang);
                return;
            }

            self.currentSrcLang = newSrc;
            self.currentTargetLang = newTarget;
            self.isAllTranslated = false;
            $('#btnTranslateAll').removeClass('active').find('.btn-text').text('Translate all');

            self.renderParagraphs();

            if (self.isFocusMode) {
                const headerHtml = $('#storyChapterHeader').prop('outerHTML');
                $('#focusContentContainer').html(headerHtml + $('#paragraphList').html());
            }
        });

        $(document).off('dblclick', '.text-segment').on('dblclick', '.text-segment', function () {
            const $el = $(this);
            const order = $el.attr('data-order');

            const $original = $(`#paragraphList .text-segment[data-order="${order}"]`);
            self.toggleLanguage($original);

            if (self.isFocusMode) {
                const $focus = $(`#focusContentContainer .text-segment[data-order="${order}"]`);
                if ($focus.length > 0) {
                    $focus.html($original.html());
                    $focus.data('lang', $original.data('lang'));
                    $focus.toggleClass('is-translated', $original.hasClass('is-translated'));
                }
            }
        });

        $('#btnTranslateAll').off('click').on('click', function () {
            self.isAllTranslated = !self.isAllTranslated;
            const $btn = $(this);
            $btn.toggleClass('active', self.isAllTranslated);

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
            $btn.find('.btn-text').text(self.isAllTranslated ? `Show ${srcLabel}` : 'Translate all');
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

        $wrapper.removeClass('theme-light theme-sepia theme-dark').addClass('theme-' + themeName);
        $overlay.removeClass('theme-light theme-sepia theme-dark').addClass('theme-' + themeName);
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

            // Kích hoạt animation trượt của thanh điều hướng
            $fixedBar.addClass('focus-layout-active');
            $actionControls.addClass('focus-layout-active');

            // Mở lớp nền Focus Mode mượt mà
            $overlay.removeClass('hidden');
            requestAnimationFrame(() => {
                $overlay.addClass('block opacity-100');
            });

            $('body').addClass('overflow-hidden');
            $btn.addClass('bg-amber-500 text-white').find('.btn-text').text('Exit Focus');
            $btn.find('i').removeClass('fa-eye-slash').addClass('fa-eye');
        } else {
            // Trượt thanh điều hướng trở về vị trí cũ
            $fixedBar.removeClass('focus-layout-active');
            $actionControls.removeClass('focus-layout-active');

            $overlay.removeClass('opacity-100');
            setTimeout(() => {
                $overlay.removeClass('block').addClass('hidden');
                $container.empty();
            }, 300);

            $('body').removeClass('overflow-hidden');
            $btn.removeClass('bg-amber-500 text-white').find('.btn-text').text('Focus Mode');
            $btn.find('i').removeClass('fa-eye').addClass('fa-eye-slash');
        }
    }
}