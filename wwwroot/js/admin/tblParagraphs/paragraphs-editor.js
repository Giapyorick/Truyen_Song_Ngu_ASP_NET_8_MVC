/* paragraphs-editor.js - Quản lý Reader / Fullscreen Editor theo chương, Side panel & Chèn câu sau */

let chapterEditorData = null;
let chapterEditorObserver = null;
let chapterEditorParagraphs = [];
let currentEditorParagraphIndex = 0;
let currentEditorLanguage = 'english';

let sideQuill = null;
let currentEditingIndex = -1;
let currentSideTabLang = 'english';
let modifiedParagraphsMap = new Map();

// 1. Khởi tạo Quill riêng cho Side Panel
function initSideQuillEditor() {
    if (sideQuill) return;

    sideQuill = new Quill('#sideQuillEditor', {
        theme: 'snow',
        modules: {
            toolbar: [
                ['bold', 'italic', 'underline', 'strike'],
                [{ 'color': [] }, { 'background': [] }],
                ['clean']
            ],
            keyboard: {
                bindings: {
                    handleEnter: { key: 'Enter', shiftKey: null, handler: () => false }
                }
            }
        }
    });

    if (sideQuill.root) {
        sideQuill.root.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.keyCode === 13) {
                e.preventDefault();
                return false;
            }
        }, true);
    }

    // Cập nhật text real-time vào bộ nhớ tạm
    sideQuill.on('text-change', function (delta, oldDelta, source) {
        if (source !== 'user' || currentEditingIndex === -1) return;

        const currentPara = chapterEditorParagraphs[currentEditingIndex];
        if (!currentPara) return;

        let cleanHtml = normalizeQuillHtml(sideQuill.root.innerHTML) || '';
        currentPara[currentSideTabLang] = cleanHtml;
        markParagraphAsModified(currentPara);

        if (currentSideTabLang.toLowerCase() === currentEditorLanguage.toLowerCase()) {
            renderChapterEditor();
            $(`.editor-reader-paragraph[data-paragraph-index="${currentEditingIndex}"]`).addClass('editing-live');
        }
    });

    $('#sidePanelBlockType').on('select2:select change', function () {
        const val = $(this).val();
        if (val !== null && val !== undefined) onSidePanelBlockTypeChange(val);
    });
}

// 2. Mở toàn màn hình Reader / Editor
function openChapterEditor(id) {
    $('#chapterEditorModal').removeClass('hidden');
    $('#chapterEditorParagraphList').html(`
        <div class="text-center text-gray-400 py-20">
            <i class="fa-solid fa-spinner fa-spin text-2xl mb-3"></i>
            <div>Loading chapter...</div>
        </div>
    `);

    closeChapterSidePanel();

    $.ajax({
        url: '/Admin/tblParagraphs/GetChapterForEditor/' + id,
        type: 'GET',
        success: function (data) {
            chapterEditorData = data;
            chapterEditorParagraphs = data.paragraphs || [];
            currentEditorParagraphIndex = 0;

            $('#editorStoryTitle').text(data.storyTitle || 'Unknown Story');
            $('#editorChapterTitle').text(data.chapterTitle || 'Unknown Chapter');
            $('#chapterParagraphCount').text(`${chapterEditorParagraphs.length} paragraphs`);

            buildChapterEditorLanguageTabs();
            renderChapterEditor();
            updateChapterEditorCounter();
        },
        error: function () {
            $('#chapterEditorParagraphList').html(`
                <div class="text-center text-red-500 py-20">
                    <i class="fa-solid fa-circle-exclamation text-2xl mb-3"></i>
                    <div>Cannot load chapter content.</div>
                </div>
            `);
        }
    });
}

// ==========================================================================
// HỘP THOẠI CẢNH BÁO 3 LỰA CHỌN: Stay | Discard & Exit | Save & Exit
// ==========================================================================
function unsavedChangesConfirm(count) {
    return new Promise((resolve) => {
        $('#unsavedModalOverlay').remove();

        const modalHtml = `
            <div id="unsavedModalOverlay" class="fixed inset-0 bg-black/60 backdrop-blur-sm z-[9999999] flex items-center justify-center p-4 animate-in fade-in duration-200">
                <div class="bg-white w-full max-w-md rounded-[2.5rem] p-8 shadow-2xl border border-slate-100 text-center space-y-5 transform scale-100">
                    <div class="w-16 h-16 mx-auto rounded-2xl bg-amber-50 flex items-center justify-center text-amber-500 text-2xl shadow-inner">
                        <i class="fa-solid fa-triangle-exclamation"></i>
                    </div>

                    <div>
                        <h4 class="text-xl font-black text-cus tracking-tight">Unsaved Changes</h4>
                        <p class="text-xs text-gray-500 mt-2 leading-relaxed font-medium">
                            You have <strong class="text-gray-800 font-bold">${count}</strong> modified paragraph(s) that are <strong>not saved yet</strong>.<br>
                            What would you like to do before leaving?
                        </p>
                    </div>

                    <div class="pt-2 flex flex-col gap-3 w-full">
                        <!-- Nút 1: Lưu và Thoát -->
                        <button id="btnSaveAndExit" type="button"
                                class="w-full !m-0 !py-3.5 btn-grad text-white font-extrabold text-xs uppercase tracking-wider rounded-xl shadow-lg hover:shadow-teal-500/25 active:scale-95 transition-all">
                            <i class="fa-solid fa-floppy-disk mr-1.5"></i> Save & Exit
                        </button>

                        <!-- Nút 2: Bỏ thay đổi & Thoát -->
                        <button id="btnDiscardAndExit" type="button"
                                class="w-full !m-0 !py-3.5 btn-grad-cancel_modal text-white font-extrabold text-xs uppercase tracking-wider rounded-xl active:scale-95 transition-all">
                            <i class="fa-solid fa-trash-can mr-1.5"></i> Discard & Exit
                        </button>

                        <!-- Nút 3: Ở lại tiếp tục chỉnh sửa -->
                        <button id="btnStayHere" type="button"
                                class="w-full !m-0 !py-3.5 btn-grad-stay text-white font-extrabold text-xs uppercase tracking-wider rounded-xl active:scale-95 transition-all">
                            Stay on this page
                        </button>
                    </div>
                </div>
            </div>`;

        $('body').append(modalHtml);

        function cleanup(choice) {
            $('#unsavedModalOverlay').fadeOut(200, function () {
                $(this).remove();
            });
            resolve(choice);
        }

        $('#btnSaveAndExit').on('click', () => cleanup('save'));
        $('#btnDiscardAndExit').on('click', () => cleanup('discard'));
        $('#btnStayHere').on('click', () => cleanup('stay'));
    });
}

// ==========================================================================
// HÀM LƯU TẤT CẢ DỮ LIỆU ĐANG SỬA (Tách riêng để tái sử dụng với async/await)
// ==========================================================================
async function saveChapterParagraphsEditor() {
    if (modifiedParagraphsMap.size === 0) return true;

    // Kiểm tra chặn nếu là tài khoản Viewer
    if (window.IS_VIEWER_MODE) {
        showToast("You have VIEWER (read-only) access. You are not permitted to save changes!", "error");
        return false;
    }

    const modifiedList = Array.from(modifiedParagraphsMap.values()).map(p => ({
        ParagraphId: p.paragraphId,
        ParagraphOrder: p.paragraphOrder,
        ChapterId: chapterEditorData.chapterId,
        English: p.english,
        Vietnamese: p.vietnamese,
        Chinese: p.chinese,
        Japanese: p.japanese,
        French: p.french,
        BlockType: p.blockType
    }));

    const $btn =$('#chapterEditorSaveButton').prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin"></i> Saving...');

    try {
        const res = await $.ajax({
            url: '/Admin/tblParagraphs/UpdateMultipleFromEditor',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(modifiedList)
        });

        if (res.success) {
            showToast('All changes saved successfully!', 'success');
            modifiedParagraphsMap.clear();
            $('#chapterModifiedInfo').addClass('hidden');
            $btn.prop('disabled', true).html('<i class="fa-solid fa-save"></i> <span>Save Changes</span>');
            if (typeof loadParagraphList === 'function') loadParagraphList(currentPage);
            return true;
        } else {
            showToast(res.message || 'Error occurred while saving!', 'error');
            $btn.prop('disabled', false).html('<i class="fa-solid fa-save"></i> <span>Save Changes</span>');
            return false;
        }
    } catch (err) {
        showToast('Cannot connect to the server!', 'error');
        $btn.prop('disabled', false).html('<i class="fa-solid fa-save"></i> <span>Save Changes</span>');
        return false;
    }
}

// Sự kiện click nút Save ở thanh tiêu đề editor
$(document).on('click', '#chapterEditorSaveButton', function () {
    saveChapterParagraphsEditor();
});

// ==========================================================================
// HÀM ĐÓNG CHAPTER EDITOR (Đã tích hợp 3 lựa chọn)
// ==========================================================================
async function closeChapterEditor() {
    // 1. Kiểm tra nếu có dữ liệu bị chỉnh sửa chưa lưu
    if (modifiedParagraphsMap && modifiedParagraphsMap.size > 0) {
        const count = modifiedParagraphsMap.size;
        const action = await unsavedChangesConfirm(count);

        if (action === 'stay') {
            // Lựa chọn "Stay on this page": giữ nguyên, không đóng editor
            return;
        }

        if (action === 'save') {
            // Lựa chọn "Save & Exit": gọi API lưu dữ liệu trước
            const saveSuccess = await saveChapterParagraphsEditor();
            // Nếu lưu thất bại do lỗi kết nối hoặc bị phân quyền chặn thì dừng lại
            if (!saveSuccess) return;
        }

        // Lựa chọn "Discard & Exit": bỏ qua lưu và chạy tiếp xuống dọn dẹp
    }

    // 2. Hủy observer scroll theo dõi câu
    if (chapterEditorObserver) {
        chapterEditorObserver.disconnect();
        chapterEditorObserver = null;
    }

    // 3. Đóng Side Panel & ẩn Modal Editor
    closeChapterSidePanel();
    $('#chapterEditorModal').addClass('hidden');

    // 4. Giải phóng bộ nhớ đệm
    chapterEditorData = null;
    chapterEditorParagraphs = [];
    currentEditorParagraphIndex = 0;
    modifiedParagraphsMap.clear();
    $('#chapterModifiedInfo').addClass('hidden');
    $('#chapterEditorSaveButton').prop('disabled', true).html('<i class="fa-solid fa-save"></i> <span>Save Changes</span>');
}

function hasLanguageContent(language) {
    return chapterEditorParagraphs.some(p => {
        const val = p[language];
        return val !== null && val !== undefined && val.trim() !== '';
    });
}

function buildChapterEditorLanguageTabs() {
    const languages = [
        { key: 'english', label: '🇺🇸 EN', fullName: 'English' },
        { key: 'vietnamese', label: '🇻🇳 VN', fullName: 'Vietnamese' },
        { key: 'chinese', label: '🇨🇳 ZH', fullName: 'Chinese' },
        { key: 'japanese', label: '🇯🇵 JP', fullName: 'Japanese' },
        { key: 'french', label: '🇫🇷 FR', fullName: 'French' }
    ];

    const $mainContainer = $('#chapterEditorLanguageTabs');
    const $sideContainer = $('#sidePanelLangTabs');

    if ($mainContainer.length) $mainContainer.empty();
    if ($sideContainer.length) $sideContainer.empty();

    let firstLanguage = null;

    languages.forEach(lang => {
        if (!hasLanguageContent(lang.key)) return;
        if (!firstLanguage) firstLanguage = lang.key;

        if ($mainContainer.length) {
            $mainContainer.append(`
                <button type="button" class="chapter-editor-language-tab" data-language="${lang.key}" onclick="changeChapterEditorLanguage('${lang.key}')">
                    ${lang.label}
                </button>
            `);
        }

        if ($sideContainer.length) {
            $sideContainer.append(`
                <button type="button" id="sideTab_${lang.key}" class="side-panel-lang-tab px-3 py-1.5 text-xs rounded-xl transition font-medium text-gray-500 hover:bg-gray-100" data-language="${lang.key}" onclick="changeChapterEditorLanguage('${lang.key}')">
                    ${lang.label}
                </button>
            `);
        }
    });

    if (!currentEditorLanguage && firstLanguage) currentEditorLanguage = firstLanguage;
    updateActiveEditorLanguageTab();
}

function changeChapterEditorLanguage(language) {
    if (!hasLanguageContent(language)) return;

    currentEditorLanguage = language;
    currentSideTabLang = language;

    updateActiveEditorLanguageTab();
    renderChapterEditor();

    if (currentEditingIndex !== -1 && sideQuill) {
        const paragraph = chapterEditorParagraphs[currentEditingIndex];
        if (paragraph) sideQuill.root.innerHTML = paragraph[currentEditorLanguage] || '';
        $('#sideCurrentLangLabel').text(currentEditorLanguage.toUpperCase());
        $(`.editor-reader-paragraph[data-paragraph-index="${currentEditingIndex}"]`).addClass('active editing-live');
    }
}

function updateActiveEditorLanguageTab() {
    $('.chapter-editor-language-tab').removeClass('active'); $(`.chapter-editor-language-tab[data-language="${currentEditorLanguage}"]`).addClass('active');

    $('.side-panel-lang-tab').removeClass('bg-teal-50 text-teal-700 font-bold border border-teal-200 shadow-sm').addClass('text-gray-500 font-medium'); $(`#sideTab_${currentEditorLanguage}`).addClass('bg-teal-50 text-teal-700 font-bold border border-teal-200 shadow-sm').removeClass('text-gray-500 font-medium');
    $('#sideCurrentLangLabel').text(currentEditorLanguage.toUpperCase());
}

function getEditorParagraphText(paragraph, language) {
    const val = paragraph[language];
    return (!val || val.trim() === '') ? null : val;
}

// Bóc tách cả thẻ ảnh đơn và thẻ dải thanh strip
function extractMediaInfo(text) {
    if (!text) return null;

    // A. Kiểm tra thẻ Multi-strip: [multistrip: url1 | url2 | url3 | caption: ...]
    const multiMatch = text.match(/\[multistrip\s*:\s*([^\]|]+)\s*\|\s*([^\]|]+)\s*\|\s*([^\]|]+)(?:\|\s*caption\s*:\s*([^\]]*))?\]/i);
    if (multiMatch) {
        return {
            type: 'multistrip',
            fullTag: multiMatch[0],
            urls: [multiMatch[1].trim(), multiMatch[2].trim(), multiMatch[3].trim()],
            count: 3,
            caption: multiMatch[4] ? multiMatch[4].trim() : ''
        };
    }

    // B. Kiểm tra thẻ Panorama Strip: [strip: url | count: 3 | caption: ...]
    const stripMatch = text.match(/\[strip\s*:\s*([^\]|]+)(?:\|\s*count\s*:\s*(\d+))?(?:\|\s*caption\s*:\s*([^\]]*))?\]/i);
    if (stripMatch) {
        return {
            type: 'strip',
            fullTag: stripMatch[0],
            url: stripMatch[1].trim(),
            count: parseInt(stripMatch[2] || 3),
            caption: stripMatch[3] ? stripMatch[3].trim() : ''
        };
    }

    // C. Kiểm tra thẻ ảnh đơn thông thường: [img: url | caption]
    const imgMatch = text.match(/\[img\s*:\s*([^\]|]+)(?:\|([^\]]*))?\]/i);
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
$(document).on('change', '#imgDisplayMode', function () {
    const val = $(this).val();
    if (val === 'strip') {
        $('#singleUploadArea').show();
        $('#stripCountWrapper').show();
        $('#multiUploadArea').hide();
    } else if (val === 'multi-strip') {
        $('#singleUploadArea').hide();
        $('#multiUploadArea').show();
    } else {
        $('#singleUploadArea').show();
        $('#stripCountWrapper').hide();
        $('#multiUploadArea').hide();
    }
});

// Xem trước từng ảnh trong 3 thanh riêng biệt
function previewMultiItem(input, index) {
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = function (e) {
            $(`#previewMulti_${index}`).attr('src', e.target.result).removeClass('hidden');
            $(`#iconMulti_${index}`).addClass('hidden');
        };
        reader.readAsDataURL(input.files[0]);
    }
}

// Render nội dung reader
function renderChapterEditor() {
    const $container = $('#chapterEditorParagraphList');
    if (!chapterEditorParagraphs || chapterEditorParagraphs.length === 0) {
        $container.html('<div class="text-center text-gray-400 py-20">No paragraphs found for this chapter.</div>');
        $('#chapterParagraphCounter').text('0 / 0');
        return;
    }

    let html = '<div class="chapter-editor-reader-inner">';
    let isBlockOpen = false;
    let previousWasDialogue = false;

    chapterEditorParagraphs.forEach(function (p, index) {
        let rawContent = getEditorParagraphText(p, currentEditorLanguage) || '';
        let blockType = p.blockType !== undefined && p.blockType !== null ? parseInt(p.blockType) : (index === 0 ? 0 : 1);
        const order = p.paragraphOrder;

        let mediaInfo = extractMediaInfo(rawContent);
        if (!mediaInfo) {
            const fallbackOrder = [p.english, p.vietnamese, p.chinese, p.japanese, p.french];
            for (let item of fallbackOrder) {
                const info = extractMediaInfo(item);
                if (info) { mediaInfo = info; break; }
            }
        }

        let mediaAfterHtml = '';
        // Bổ sung xử lý hiển thị thẻ multistrip trong renderChapterEditor
        if (mediaInfo && (mediaInfo.url || (mediaInfo.urls && mediaInfo.urls.length))) {
            if (mediaInfo.fullTag) rawContent = rawContent.replace(mediaInfo.fullTag, '').trim();

            if (mediaInfo.type === 'multistrip') {
                // Trường hợp 3 thanh với 3 ảnh khác nhau
                let panelsHtml = '';
                mediaInfo.urls.forEach(u => {
                    panelsHtml += `
                <div class="strip-panel-card" style="width: calc((100% - 24px) / 3); background-image: url('${u}'); background-size: cover; background-position: center;">
                </div>`;
                });

                mediaAfterHtml = `
            <div class="strip-gallery-wrapper relative my-6">
                <div class="strip-gallery-slider">
                    ${panelsHtml}
                </div>
                ${mediaInfo.caption ? `<div class="story-illustration-caption text-center mt-2 text-xs text-gray-500 font-semibold"><i class="fa-solid fa-camera mr-1"></i>${mediaInfo.caption}</div>` : ''}
            </div>`;

            } else if (mediaInfo.type === 'strip') {
                // Trường hợp 1 ảnh chia thành nhiều thanh panel
                const count = mediaInfo.count || 3;
                const sliderId = `stripSlider_${p.paragraphId}`;
                let panelsHtml = '';

                for (let idx = 0; idx < count; idx++) {
                    const posX = count > 1 ? (idx / (count - 1)) * 100 : 50;
                    panelsHtml += `
                <div class="strip-panel-card" style="width: calc((100% - 24px) / 3); background-image: url('${mediaInfo.url}'); background-size: ${count * 100}% 100%; background-position: ${posX}% center;">
                </div>`;
                }

                const showNav = count > 3;
                mediaAfterHtml = `
            <div class="strip-gallery-wrapper relative my-6">
                ${showNav ? `<button type="button" class="strip-nav-btn prev" onclick="scrollCustomStrip('${sliderId}', -1)"><i class="fa-solid fa-chevron-left"></i></button>` : ''}
                <div id="${sliderId}" class="strip-gallery-slider">
                    ${panelsHtml}
                </div>
                ${showNav ? `<button type="button" class="strip-nav-btn next" onclick="scrollCustomStrip('${sliderId}', 1)"><i class="fa-solid fa-chevron-right"></i></button>` : ''}
                ${mediaInfo.caption ? `<div class="story-illustration-caption text-center mt-2 text-xs text-gray-500 font-semibold"><i class="fa-solid fa-camera mr-1"></i>${mediaInfo.caption}</div>` : ''}
            </div>`;
            } else {
                // Trường hợp ảnh đơn tiêu chuẩn
                const captionHtml = mediaInfo.caption ? `<div class="story-illustration-caption"><i class="fa-solid fa-camera"></i> ${mediaInfo.caption}</div>` : '';
                mediaAfterHtml = `
            <div class="story-illustration-block">
                <img src="${mediaInfo.url}" alt="${mediaInfo.caption || 'Illustration #' + order}" onerror="this.src='/assets/image/placeholder.png';">
                ${captionHtml}
            </div>`;
            }
        }

        // Làm sạch toàn bộ các loại thẻ ảnh/strip khỏi text hiển thị
        rawContent = rawContent.replace(/\[(img|strip)[\s\S]*?\]/gi, '').trim();
        const content = stripBlockTags(rawContent);


        const shouldStartNewBlock = (index === 0) || (blockType === 0 || blockType === 2 || blockType === 4) || previousWasDialogue;

        if (shouldStartNewBlock) {
            if (isBlockOpen) html += '</div>';
            let blockClass = 'para-block-start';
            if (blockType === 2) blockClass = 'para-block-indent';
            else if (blockType === 4) blockClass = 'para-block-dialogue';
            else if (blockType === 1 && previousWasDialogue) blockClass = 'para-block-indent';

            html += `<div class="${blockClass}">`;
            isBlockOpen = true;
        }

        const isEditingThis = (currentEditingIndex === index);
        const editingClasses = isEditingThis ? ' active editing-live' : '';

        html += `<span class="editor-reader-paragraph${editingClasses}" data-paragraph-id="${p.paragraphId}" data-paragraph-index="${index}" data-paragraph-order="${order}" title="#${String(order).padStart(3, '0')} (BlockType: ${blockType})">`;

        if (!content) {
            if (!mediaAfterHtml) html += `<span class="italic text-gray-400">[No ${currentEditorLanguage} text]</span>`;
        } else {
            html += content;
        }
        html += `</span> `;

        previousWasDialogue = (blockType === 4);

        if (mediaAfterHtml) {
            if (isBlockOpen) { html += '</div>'; isBlockOpen = false; }
            html += mediaAfterHtml;
            previousWasDialogue = false;
        }
    });

    if (isBlockOpen) html += '</div>';
    html += '</div>';

    $container.html(html);
    updateChapterEditorCounter();
    initChapterEditorScrollObserver();
}

function stripBlockTags(str) {
    if (!str) return null;
    return str.replace(/<\/?p[^>]*>/gi, '').replace(/<br\s*\/?>/gi, ' ').trim();
}

function normalizeQuillHtml(html) {
    if (!html) return null;
    let clean = html.trim();
    if (clean === '' || clean === '<p><br></p>' || clean === '<p></p>') return null;
    if (clean.startsWith('<p>') && clean.endsWith('</p>')) clean = clean.slice(3, -4);
    return clean.replace(/<\/?p[^>]*>/gi, '').replace(/<br\s*\/?>/gi, ' ').trim();
}

function updateChapterEditorCounter() {
    const total = chapterEditorParagraphs.length;
    if (total === 0) {
        $('#chapterParagraphCounter').text('0 / 0');
        return;
    }
    const current = currentEditorParagraphIndex + 1;
    $('#chapterParagraphCounter').text(`${current} / ${total}`);

    const p = chapterEditorParagraphs[currentEditorParagraphIndex];
    if (p) $('#chapterCurrentParagraph').text(`#${String(p.paragraphOrder).padStart(3, '0')}`);
}

// Click chọn câu trên reader
$(document).off('click.chapterEditor', '.editor-reader-paragraph').on('click.chapterEditor', '.editor-reader-paragraph', function () {
    const index = parseInt($(this).attr('data-paragraph-index'));
    currentEditorParagraphIndex = index;

    $('.editor-reader-paragraph').removeClass('active'); $(this).addClass('active');

    updateChapterEditorCounter();
    openChapterSidePanel(index);
});

function openChapterSidePanel(index) {
    currentEditingIndex = index;
    const paragraph = chapterEditorParagraphs[index];
    if (!paragraph) return;

    initSideQuillEditor();

    $('#sideEditorFormWrapper').removeClass('hidden');
    $('#chapterEditorSidePanel').removeClass('hidden').addClass('open');
    $('#sidePanelParagraphNumber').text(`#${String(paragraph.paragraphOrder).padStart(3, '0')}`);

    const bType = paragraph.blockType !== undefined && paragraph.blockType !== null ? String(paragraph.blockType) : "1";
    $('#sidePanelBlockType').val(bType).trigger('change');

    updateActiveEditorLanguageTab();

    if (sideQuill) {
        sideQuill.root.innerHTML = paragraph[currentEditorLanguage] || '';
    }

    $('.editor-reader-paragraph').removeClass('editing-live active'); $(`.editor-reader-paragraph[data-paragraph-index="${index}"]`).addClass('active editing-live');
}

function closeChapterSidePanel() {
    $('#chapterEditorSidePanel').removeClass('open').addClass('hidden');
    $('#sideEditorFormWrapper').addClass('hidden');
    $('.editor-reader-paragraph').removeClass('editing-live active');
    currentEditingIndex = -1;
}

function onSidePanelBlockTypeChange(newVal) {
    if (currentEditingIndex === -1) return;
    const paragraph = chapterEditorParagraphs[currentEditingIndex];
    if (!paragraph) return;

    paragraph.blockType = parseInt(newVal);
    markParagraphAsModified(paragraph);
    renderChapterEditor();
    $(`.editor-reader-paragraph[data-paragraph-index="${currentEditingIndex}"]`).addClass('editing-live active');
}

function markParagraphAsModified(paragraph) {
    modifiedParagraphsMap.set(paragraph.paragraphId, paragraph);
    const count = modifiedParagraphsMap.size;
    $('#chapterEditorSaveButton').prop('disabled', false);
    $('#chapterModifiedInfo').removeClass('hidden');
    $('#chapterModifiedCount').text(`${count} modified`);
}

// Lưu tất cả thay đổi từ Side Panel
$(document).on('click', '#chapterEditorSaveButton', function () {
    if (modifiedParagraphsMap.size === 0) return;

    const modifiedList = Array.from(modifiedParagraphsMap.values()).map(p => ({
        ParagraphId: p.paragraphId,
        ParagraphOrder: p.paragraphOrder,
        ChapterId: chapterEditorData.chapterId,
        English: p.english,
        Vietnamese: p.vietnamese,
        Chinese: p.chinese,
        Japanese: p.japanese,
        French: p.french,
        BlockType: p.blockType
    }));

    const $btn = $(this).prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin"></i> Saving...');

    $.ajax({
        url: '/Admin/tblParagraphs/UpdateMultipleFromEditor',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(modifiedList),
        success: function (res) {
            if (res.success) {
                showToast('All changes saved successfully!', 'success');
                modifiedParagraphsMap.clear();
                $('#chapterModifiedInfo').addClass('hidden');
                $btn.prop('disabled', true).html('<i class="fa-solid fa-save"></i> <span>Save Changes</span>');
                if (typeof loadParagraphList === 'function') loadParagraphList(currentPage);
            } else {
                showToast(res.message || 'Error occurred while saving!', 'error');
                $btn.prop('disabled', false).html('<i class="fa-solid fa-save"></i> <span>Save Changes</span>');
            }
        },
        error: function () {
            showToast('Cannot connect to the server!', 'error');
            $btn.prop('disabled', false).html('<i class="fa-solid fa-save"></i> <span>Save Changes</span>');
        }
    });
});

function previousChapterParagraph() {
    if (currentEditorParagraphIndex <= 0) return;
    currentEditorParagraphIndex--;
    scrollToEditorParagraph(currentEditorParagraphIndex);
}

function nextChapterParagraph() {
    if (currentEditorParagraphIndex >= chapterEditorParagraphs.length - 1) return;
    currentEditorParagraphIndex++;
    scrollToEditorParagraph(currentEditorParagraphIndex);
}

function scrollToEditorParagraph(index) {
    const $reader = $('#chapterEditorParagraphList');
    const $p = $reader.find(`.editor-reader-paragraph[data-paragraph-index="${index}"]`);
    if ($p.length === 0) return;

    const target = $reader.scrollTop() + $p.offset().top - $reader.offset().top - 30;
    $reader.animate({ scrollTop: target }, 300);
    updateChapterEditorCounter();
}

function initChapterEditorScrollObserver() {
    const reader = document.getElementById('chapterEditorParagraphList');
    if (!reader) return;
    if (chapterEditorObserver) chapterEditorObserver.disconnect();

    chapterEditorObserver = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (!entry.isIntersecting) return;
            currentEditorParagraphIndex = parseInt(entry.target.dataset.paragraphIndex);
            updateChapterEditorCounter();
        });
    }, { root: reader, threshold: 0.5 });

    reader.querySelectorAll('.editor-reader-paragraph').forEach(p => chapterEditorObserver.observe(p));
}

// Xóa câu từ Side Panel
async function deleteFromCurrentEditorIndex() {
    if (currentEditingIndex === -1 || !chapterEditorParagraphs || !chapterEditorParagraphs[currentEditingIndex]) {
        showToast('Please select a paragraph to delete!', 'error');
        return;
    }

    const targetPara = chapterEditorParagraphs[currentEditingIndex];
    const orderNum = String(targetPara.paragraphOrder).padStart(3, '0');

    const confirmed = await safeConfirm(
        `Are you sure you want to delete paragraph <strong>#${orderNum}</strong>?<br><small class="text-red-500">The chapter will be automatically re-indexed from 0 to n-1.</small>`,
        "Delete Paragraph"
    );
    if (!confirmed) return;

    $.ajax({
        url: '/Admin/tblParagraphs/Delete',
        type: 'POST',
        data: { id: targetPara.paragraphId },
        success: function (res) {
            if (res.success) {
                showToast('Deleted paragraph and normalized orders!', 'success');
                const deletedIndex = currentEditingIndex;
                const currentChapId = chapterEditorData.chapterId;

                $.get('/Admin/tblParagraphs/GetChapterForEditor/' + currentChapId, function (data) {
                    chapterEditorData = data;
                    chapterEditorParagraphs = data.paragraphs || [];
                    $('#chapterParagraphCount').text(`${chapterEditorParagraphs.length} paragraphs`);
                    renderChapterEditor();

                    if (chapterEditorParagraphs.length === 0) {
                        closeChapterSidePanel();
                    } else {
                        let nextIndex = deletedIndex >= chapterEditorParagraphs.length ? chapterEditorParagraphs.length - 1 : deletedIndex;
                        openChapterSidePanel(nextIndex);
                        scrollToEditorParagraph(nextIndex);
                    }
                    if (typeof loadParagraphList === 'function') loadParagraphList(currentPage);
                });
            } else {
                showToast(res.message || 'Delete failed!', 'error');
            }
        },
        error: () => showToast('Server error while deleting paragraph!', 'error')
    });
}

// Chèn câu sau từ Side Panel
function insertFromCurrentEditorIndex() {
    if (currentEditingIndex === -1 || !chapterEditorParagraphs[currentEditingIndex]) {
        showToast('Please select a paragraph to insert after!', 'error');
        return;
    }
    const targetPara = chapterEditorParagraphs[currentEditingIndex];
    openInsertParagraphModal(chapterEditorData.chapterId, targetPara.paragraphOrder);
}

// Mở modal chèn câu sau
async function openInsertParagraphModal(chapterId, targetOrder) {
    if (!chapterId || chapterId <= 0) {
        showToast('Invalid Chapter selected!', 'error');
        return;
    }

    $('#insertChapterId').val(chapterId);
    $('#insertTargetOrder').val(targetOrder);
    $('#lblInsertTargetOrder').text(`#${targetOrder}`);
    $('#insertBlockType').val('1');

    const $modal = $('#insertParagraphModal');
    const $content = $('#insertModalContent');
    const $container = $('#insertDynamicLanguages');

    $container.html('<div class="text-center py-6 text-gray-400 text-xs"><i class="fa-solid fa-spinner fa-spin mr-1"></i> Loading language configurations...</div>');

    $modal.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('scale-95 opacity-0').addClass('scale-100 opacity-100'), 10);

    try {
        const res = await fetch(`/Admin/tblParagraphs/GetAvailableLanguages?chapterId=${chapterId}`);
        const data = await res.json();

        if (data.success && data.languages && data.languages.length > 0) {
            let html = '';
            data.languages.forEach(lang => {
                const code = (lang.code || '').toLowerCase().trim();
                const raw = (lang.rawCode || '').toLowerCase().trim();

                let key = 'en';

                if (code === 'vietnamese' || raw.includes('việt') || raw.includes('viet') || raw.includes('vi')) {
                    key = 'vi';
                } else if (code === 'chinese' || raw.includes('trung') || raw.includes('hoa') || raw.includes('zh') || raw.includes('cn')) {
                    key = 'zh';
                } else if (code === 'japanese' || raw.includes('nhật') || raw.includes('nhat') || raw.includes('ja') || raw.includes('jp')) {
                    key = 'ja';
                } else if (code === 'french' || raw.includes('pháp') || raw.includes('phap') || raw.includes('fr')) {
                    key = 'fr';
                } else if (code === 'english' || raw.includes('anh') || raw.includes('en') || raw.includes('us')) {
                    key = 'en';
                }

                const isRequired = (key === 'en' || key === 'vi');

                html += `
                    <div class="p-3 bg-teal-50/20 border border-teal-100 rounded-2xl space-y-1.5">
                        <div class="flex justify-between items-center">
                            <label class="text-xs font-bold text-gray-700 flex items-center gap-1.5">
                                <span class="px-2 py-0.5 rounded bg-teal-600 text-white text-[10px] font-extrabold uppercase">${key}</span>
                                ${lang.name}
                            </label>
                            ${isRequired ? '<span class="text-[10px] text-red-500 font-bold">* Required</span>' : '<span class="text-[10px] text-gray-400">Optional</span>'}
                        </div>
                        <textarea id="insert_input_${key}" data-lang-key="${key}" class="insert-lang-input w-full px-3 py-2 bg-white border border-gray-200 focus:border-teal-500 rounded-xl text-xs outline-none transition resize-none" rows="2" placeholder="Enter content for ${lang.name}..."></textarea>
                    </div>`;
            });
            $container.html(html);
        } else {
            $container.html('<div class="text-center py-4 text-amber-600 text-xs font-semibold">No configured languages found for this story.</div>');
        }
    } catch (e) {
        $container.html(`<div class="text-center py-4 text-red-500 text-xs">Cannot load languages: ${e.message}</div>`);
    }
}

function closeInsertParagraphModal() {
    $('#insertModalContent').removeClass('scale-100 opacity-100').addClass('scale-95 opacity-0');
    setTimeout(() => $('#insertParagraphModal').removeClass('flex').addClass('hidden'), 250);
}

// Gửi payload chèn câu và dồn STT
async function submitInsertParagraph() {
    const chapterId = parseInt($('#insertChapterId').val());
    const targetOrder = parseInt($('#insertTargetOrder').val());
    const blockType = parseInt($('#insertBlockType').val() || 1);

    const enVal = ($('#insert_input_en').length ? $('#insert_input_en').val() : '').trim();
    const viVal = ($('#insert_input_vi').length ? $('#insert_input_vi').val() : '').trim();
    const zhVal = ($('#insert_input_zh').length ? $('#insert_input_zh').val() : '').trim();
    const jaVal = ($('#insert_input_ja').length ? $('#insert_input_ja').val() : '').trim();
    const frVal = ($('#insert_input_fr').length ? $('#insert_input_fr').val() : '').trim();

    if (!enVal || !viVal) {
        showToast("English and Vietnamese fields are required!", "error");
        return;
    }

    const payload = {
        ChapterId: chapterId,
        TargetOrder: targetOrder,
        BlockType: blockType,
        English: enVal,
        Vietnamese: viVal,
        Chinese: zhVal || null,
        Japanese: jaVal || null,
        French: frVal || null
    };

    const $btn = $('#btnConfirmInsertParagraph').prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin"></i> Saving...');

    try {
        const res = await fetch('/Admin/tblParagraphs/InsertAfter', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        const data = await res.json();
        if (data.success) {
            showToast(data.message, 'success');
            closeInsertParagraphModal();
            if (typeof loadParagraphList === 'function') loadParagraphList(currentPage);
            if (!$('#chapterEditorModal').hasClass('hidden') && chapterEditorData) {
                openChapterEditor(chapterEditorData.chapterId);
            }
        } else {
            showToast(data.message || 'Error occurred while saving!', 'error');
        }
    } catch (err) {
        showToast('Connection error: ' + err.message, 'error');
    } finally {
        $btn.prop('disabled', false).html('<i class="fa-solid fa-floppy-disk"></i> Save & Re-index');
    }
}

let selectedImageFile = null;

$(document).on('change', '#fileStoryImageInput', function () {
    if (this.files && this.files.length > 0) {
        processSelectedImage(this.files[0]);
    }
});

$(document).on('dragenter dragover dragleave drop', '#dropZoneWrapper', function (e) {
    e.preventDefault();
    e.stopPropagation();
});

$(document).on('dragenter dragover', '#dropZoneWrapper', function () {
    $(this).addClass('border-amber-500 bg-amber-50/40').removeClass('border-gray-300');
});

$(document).on('dragleave drop', '#dropZoneWrapper', function () {
    $(this).removeClass('border-amber-500 bg-amber-50/40').addClass('border-gray-300');
});

$(document).on('drop', '#dropZoneWrapper', function (e) {
    const files = e.originalEvent.dataTransfer.files;
    if (files && files.length > 0) {
        processSelectedImage(files[0]);
    }
});

function processSelectedImage(file) {
    if (!file || !file.type.match('image.*')) {
        showToast('Please select or drop an image file only!', 'error');
        return;
    }

    selectedImageFile = file;

    const reader = new FileReader();
    reader.onload = function (e) {
        $('#imgPreviewTag').attr('src', e.target.result);
        $('#uploadPlaceholder').addClass('hidden');
        $('#imagePreviewBox').removeClass('hidden');
    };
    reader.readAsDataURL(file);
}

// Mở Modal đính kèm ảnh
$(document).on('change', '#imgDisplayMode', function () {
    if ($(this).val() === 'strip') {
        $('#stripCountWrapper').show();
    } else {
        $('#stripCountWrapper').hide();
    }
});

async function openAttachImageModal() {
    if (currentEditingIndex === -1 || !chapterEditorParagraphs[currentEditingIndex]) {
        showToast('Please select a paragraph before attaching an image!', 'error');
        return;
    }

    const currentPara = chapterEditorParagraphs[currentEditingIndex];
    let currentText = currentPara[currentSideTabLang] || '';

    // KIỂM TRA XEM ĐOẠN VĂN NÀY ĐÃ CHỨA ẢNH HAY CHƯA
    const existingMedia = extractMediaInfo(currentText);
    if (existingMedia) {
        const confirmMsg = `This paragraph already contains an attached illustration.<br><br>
            <span class="text-rose-500 font-semibold"><i class="fa-solid fa-triangle-exclamation"></i> Warning: Proceeding will permanently replace and remove the existing image!</span><br><br>
            Do you want to continue?`;

        const confirmed = typeof safeConfirm === 'function'
            ? await safeConfirm(confirmMsg, "Replace Illustration Warning")
            : confirm("This paragraph already has an image. Replacing it will remove the old one. Continue?");

        if (!confirmed) return;
    }

    // Reset các input về mặc định
    selectedImageFile = null;
    $('#fileStoryImageInput').val('');
    $('#imgCaptionInput').val('');
    $('#imgDisplayMode').val('single').trigger('change');
    $('#imgStripCount').val('3');
    $('#uploadPlaceholder').removeClass('hidden');
    $('#imagePreviewBox').addClass('hidden');
    $('#imgPreviewTag').attr('src', '');

    // Reset 3 ô ảnh của multi-strip
    for (let i = 1; i <= 3; i++) {
        $(`#fileMulti_${i}`).val('');
        $(`#previewMulti_${i}`).attr('src', '').addClass('hidden');
        $(`#iconMulti_${i}`).removeClass('hidden');
    }

    const $modal = $('#attachImageModal');
    const $content = $('#attachImageModalContent');
    $modal.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('scale-95 opacity-0').addClass('scale-100 opacity-100'), 10);
}

function closeAttachImageModal() {
    $('#attachImageModalContent').removeClass('scale-100 opacity-100').addClass('scale-95 opacity-0');
    setTimeout(() => $('#attachImageModal').removeClass('flex').addClass('hidden'), 250);
}

async function submitAttachImage() {
    const currentPara = chapterEditorParagraphs[currentEditingIndex];
    if (!currentPara) {
        showToast('Selected paragraph not found!', 'error');
        return;
    }

    const displayMode = $('#imgDisplayMode').val();
    const caption = ($('#imgCaptionInput').val() || '').trim();
    const $btn = $('#btnConfirmUploadImg').prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin"></i> Uploading...');

    try {
        let generatedTag = '';

        if (displayMode === 'multi-strip') {
            // Trường hợp 3 thanh 3 ảnh khác biệt
            const file1 = document.getElementById('fileMulti_1').files[0];
            const file2 = document.getElementById('fileMulti_2').files[0];
            const file3 = document.getElementById('fileMulti_3').files[0];

            if (!file1 || !file2 || !file3) {
                showToast('Please select all 3 images for the distinct panels!', 'error');
                $btn.prop('disabled', false).html('<i class="fa-solid fa-upload"></i> Upload & Attach');
                return;
            }

            // Upload tuần tự 3 ảnh lên Server
            async function uploadSingle(file) {
                const fd = new FormData();
                fd.append('file', file);
                const res = await fetch('/Admin/tblParagraphs/UploadIllustration', { method: 'POST', body: fd });
                const json = await res.json();
                if (!json.success) throw new Error(json.message || 'Error uploading one of the panel images');
                return json.url;
            }

            const [url1, url2, url3] = await Promise.all([uploadSingle(file1), uploadSingle(file2), uploadSingle(file3)]);
            generatedTag = caption
                ? `[multistrip: ${url1} | ${url2} | ${url3} | caption: ${caption}]`
                : `[multistrip: ${url1} | ${url2} | ${url3}]`;

        } else {
            // Trường hợp ảnh đơn hoặc Panorama 1 ảnh cắt thành nhiều thanh
            const inputElement = document.getElementById('fileStoryImageInput');
            const fileToUpload = (inputElement && inputElement.files.length > 0) ? inputElement.files[0] : selectedImageFile;

            if (!fileToUpload) {
                showToast('Please choose an image file to upload!', 'error');
                $btn.prop('disabled', false).html('<i class="fa-solid fa-upload"></i> Upload & Attach');
                return;
            }

            const formData = new FormData();
            formData.append('file', fileToUpload);

            const res = await fetch('/Admin/tblParagraphs/UploadIllustration', { method: 'POST', body: formData });
            const json = await res.json();
            if (!json.success) throw new Error(json.message || 'Upload failed');

            if (displayMode === 'strip') {
                const count = $('#imgStripCount').val() || '3';
                generatedTag = caption
                    ? `[strip: ${json.url} | count: ${count} | caption: ${caption}]`
                    : `[strip: ${json.url} | count: ${count}]`;
            } else {
                generatedTag = caption
                    ? `[img: ${json.url} | ${caption}]`
                    : `[img: ${json.url}]`;
            }
        }

        // Xóa sạch toàn bộ tag ảnh cũ nếu có trong chuỗi hiện tại
        let currentText = currentPara[currentSideTabLang] || '';
        currentText = currentText.replace(/\[(img\vert{}strip\vert{}multistrip)[\s\S]*?\]/gi, '').trim();
        currentPara[currentSideTabLang] = (currentText ? currentText + ' ' : '') + generatedTag;

        // Cập nhật Quill nếu đang mở đúng tab ngôn ngữ
        if (sideQuill && currentSideTabLang.toLowerCase() === currentEditorLanguage.toLowerCase()) {
            sideQuill.root.innerHTML = currentPara[currentSideTabLang];
        }

        markParagraphAsModified(currentPara);
        renderChapterEditor();
        $(`.editor-reader-paragraph[data-paragraph-index="${currentEditingIndex}"]`).addClass('editing-live active');

        showToast('Illustration attached successfully!', 'success');
        closeAttachImageModal();
    } catch (err) {
        showToast(err.message || 'Server error while attaching illustration!', 'error');
    } finally {
        $btn.prop('disabled', false).html('<i class="fa-solid fa-upload"></i> Upload & Attach');
    }
}
function scrollCustomStrip(sliderId, direction) {
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

async function handleMergeParagraph(direction) {
    if (currentEditingIndex === -1 || !chapterEditorParagraphs[currentEditingIndex]) {
        showToast('Please select a paragraph to merge!', 'error');
        return;
    }

    const currentPara = chapterEditorParagraphs[currentEditingIndex];
    const isPrev = direction === 'prev';
    const targetIdx = isPrev ? currentEditingIndex - 1 : currentEditingIndex + 1;

    if (targetIdx < 0 || targetIdx >= chapterEditorParagraphs.length) {
        showToast(isPrev ? 'This is the first paragraph, cannot merge with previous!' : 'This is the last paragraph, cannot merge with next!', 'warning');
        return;
    }

    const otherPara = chapterEditorParagraphs[targetIdx];

    function checkHasImg(p) {
        const textGroup = [p.english, p.vietnamese, p.chinese, p.japanese, p.french].join(' ');
        return /\[(img\vert{}strip)[\s\S]*?\]/i.test(textGroup);
    }

    const hasImg = checkHasImg(currentPara) || checkHasImg(otherPara);

    let confirmMsg = `Are you sure you want to merge paragraph <strong>#${String(currentPara.paragraphOrder).padStart(3, '0')}</strong> with the ${isPrev ? 'previous' : 'next'} paragraph <strong>#${String(otherPara.paragraphOrder).padStart(3, '0')}</strong>?`;

    if (hasImg) {
        confirmMsg += `<br><br><span class="text-red-500 font-semibold"><i class="fa-solid fa-triangle-exclamation"></i> WARNING: Illustration tag detected! If merged, existing image tags will be removed to prevent layout corruption.</span>`;
    }

    const confirmed = typeof safeConfirm === 'function'
        ? await safeConfirm(confirmMsg, "Confirm Paragraph Merge")
        : confirm(confirmMsg.replace(/<[^>]+>/g, ''));

    if (!confirmed) return;

    $.ajax({
        url: '/Admin/tblParagraphs/MergeParagraphs',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            TargetParagraphId: currentPara.paragraphId,
            Direction: direction,
            RemoveImages: true
        }),
        success: function (res) {
            if (res.success) {
                showToast(res.message, 'success');
                const currentChapId = chapterEditorData.chapterId;

                $.get('/Admin/tblParagraphs/GetChapterForEditor/' + currentChapId, function (data) {
                    chapterEditorData = data;
                    chapterEditorParagraphs = data.paragraphs || [];
                    $('#chapterParagraphCount').text(`${chapterEditorParagraphs.length} paragraphs`);
                    renderChapterEditor();

                    let newFocusIdx = isPrev ? targetIdx : currentEditingIndex;
                    if (newFocusIdx >= chapterEditorParagraphs.length) newFocusIdx = chapterEditorParagraphs.length - 1;

                    if (newFocusIdx >= 0) {
                        openChapterSidePanel(newFocusIdx);
                        scrollToEditorParagraph(newFocusIdx);
                    } else {
                        closeChapterSidePanel();
                    }

                    if (typeof loadParagraphList === 'function') loadParagraphList(currentPage);
                });
            } else {
                showToast(res.message || 'Error occurred while merging paragraphs!', 'error');
            }
        },
        error: function (xhr) {
            showToast('Server error while merging paragraphs!', 'error');
        }
    });
}

$(document).on('keydown', function (e) {
    if (e.key === 'Escape' || e.keyCode === 27) {
        if (!$('#chapterEditorModal').hasClass('hidden')) {
            e.preventDefault();
            closeChapterEditor();
        }
    }
});

window.addEventListener('beforeunload', function (e) {
    if (modifiedParagraphsMap && modifiedParagraphsMap.size > 0) {
        e.preventDefault();
        e.returnValue = 'You have unsaved changes in the editor!';
        return e.returnValue;
    }
});