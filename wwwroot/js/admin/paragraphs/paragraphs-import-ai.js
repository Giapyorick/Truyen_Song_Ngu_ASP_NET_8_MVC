/**
 * ============================================================================
 *  FILE      : wwwroot/js/admin/paragraphs/paragraphs-import-ai.js
 *  CHỨC NĂNG : Quản lý Câu/Đoạn – Import file theo từng ngôn ngữ, AI căn chỉnh & dịch tự động (Hangfire job)
 *  DÙNG TẠI  : Areas/Admin/Views/tblParagraphs/Paragraphs.cshtml (HTML ở _ParagraphImportAiModalsPartial.cshtml)
 *  PHỤ THUỘC : jQuery, window.ADMIN_LANG (khai báo trong view), showToast() (_LayoutAdmin.cshtml), window.adminLocalizer, Select2, Swal (SweetAlert2 – LƯU Ý: hiện CHƯA được nạp ở view, xem showAiFormatPrompt)
 *  THỨ TỰ    : Nạp SAU paragraphs-crud.js
 * ----------------------------------------------------------------------------
 *  MỤC LỤC
 *    1. BIẾN TOÀN CỤC & DANH SÁCH NGÔN NGỮ HỖ TRỢ
 *    2. KHỞI TẠO SELECT2 & ĐÓNG / MỞ MODAL IMPORT
 *    3. CHỌN TRUYỆN → CHƯƠNG (đọc ngôn ngữ đã có trong CSDL)
 *    4. RESET FORM
 *    5. SỰ KIỆN TRÊN TRANG (chọn truyện/chương, chọn file theo ngôn ngữ, kéo thả)
 *    6. KIỂM TRA THẺ NGÔN NGỮ & BẬT/TẮT AI DỊCH TỰ ĐỘNG
 *    7. THEO DÕI TIẾN TRÌNH JOB (Hangfire)
 *    8. PHÂN TÍCH FILE & IMPORT BẰNG AI
 * ============================================================================
 */

// ============================================================================
// 1. BIẾN TOÀN CỤC & DANH SÁCH NGÔN NGỮ HỖ TRỢ
// ============================================================================
var L = window.ADMIN_LANG || {};
var AL = window.adminLocalizer || {};

let activeTargetLanguages = ['vietnamese'];
let selectedAiLanguages = new Set();
let excelProvidedLanguages = new Set(); // Ngôn ngữ đã có cột trong file Excel tiếng Anh nhiều cột (không cần file / AI)
let inspectEnXhr = null;
let aiModels = [];                    // Model AI cho ô #aiModelSelect (GetAiModels)
const AI_MODEL_STORE_KEY = 'admin.import.aiModel';
let isInternalReset = false; // Flag chặn vòng lặp sự kiện của Select2

const LANGUAGE_DEFINITIONS = [
    { key: 'vietnamese', name: 'Vietnamese 🇻🇳', flag: '🇻🇳', fixed: true },
    { key: 'chinese', name: 'Chinese 🇨🇳', flag: '🇨🇳', fixed: false },
    { key: 'japanese', name: 'Japanese 🇯🇵', flag: '🇯🇵', fixed: false },
    { key: 'french', name: 'French 🇫🇷', flag: '🇫🇷', fixed: false }
];

// ============================================================================
// 2. KHỞI TẠO SELECT2 & ĐÓNG / MỞ MODAL IMPORT
// ============================================================================
function getImportCulture() {
    const match = document.cookie.match(new RegExp('(^| )\\.AspNetCore\\.Culture=([^;]+)'));
    if (match) {
        const cultureValue = decodeURIComponent(match[2]);
        if (cultureValue.includes('en')) return 'en-US';
        if (cultureValue.includes('vi')) return 'vi-VN';
    }

    const htmlLang = document.documentElement.lang;
    if (htmlLang) return htmlLang;

    if (typeof getCurrentCulture === 'function') return getCurrentCulture().trim();

    return 'vi-VN';
}

function initImportSelect2() {
    console.log("👉 [DEBUG] Khởi tạo Select2 cho Modal Import...");
    const $modal = $('#importParagraphModal');
    const isVi = getImportCulture().toLowerCase().startsWith('vi');
    const storyPlaceholder = L.SelectStoryPlaceholder || L.SelectStory || AL.selectStory || (isVi ? '-- Chọn truyện --' : '-- Select story --');
    const chapterPlaceholder = L.SelectChapterPlaceholder || L.SelectChapter || AL.selectChapter || (isVi ? '-- Chọn chương --' : '-- Select chapter --');

    if (!$('#storySelect').hasClass("select2-hidden-accessible")) {
        $('#storySelect').select2({
            dropdownParent: $modal,
            width: '100%',
            placeholder: storyPlaceholder,
            allowClear: false,
            minimumResultsForSearch: Infinity
        });
    }

    if (!$('#chapterSelect').hasClass("select2-hidden-accessible")) {
        $('#chapterSelect').select2({
            dropdownParent: $modal,
            width: '100%',
            placeholder: chapterPlaceholder,
            allowClear: false,
            minimumResultsForSearch: Infinity
        });
    }
}

function openParagraphModal() {
    console.log("👉 [DEBUG] Mở modal Import Paragraphs");
    const $modal = $('#importParagraphModal');
    const $content = $('#importContent');

    if ($modal.length === 0) {
        console.error("❌ Không tìm thấy modal #importParagraphModal trong DOM!");
        return;
    }

    $modal.removeClass('hidden').addClass('flex');
    setTimeout(() => {
        $content.removeClass('scale-95 opacity-0').addClass('scale-100 opacity-100');
    }, 20);

    initImportSelect2();
    loadModalStories();
}

function closeParagraphModal() {
    console.log("👉 [DEBUG] Đóng modal Import Paragraphs");
    const $modal = $('#importParagraphModal');
    const $content = $('#importContent');

    $content.removeClass('scale-100 opacity-100').addClass('scale-95 opacity-0');
    setTimeout(() => {
        $modal.removeClass('flex').addClass('hidden');
        resetImportForm();
    }, 250);
}

// ============================================================================
// 3. CHỌN TRUYỆN → CHƯƠNG (đọc ngôn ngữ đã có trong CSDL)
// ============================================================================
// 3.1 Nạp danh sách truyện vào select của Modal
function loadModalStories() {
    console.log("👉 [DEBUG] Đang nạp danh sách truyện...");
    const $storySelect = $('#storySelect');
    const $chapterSelect = $('#chapterSelect');
    const currentCulture = getImportCulture();
    const isVi = currentCulture.toLowerCase().startsWith('vi');
    const storyPlaceholder = L.SelectStoryPlaceholder || L.SelectStory || AL.selectStory || (isVi ? '-- Chọn truyện --' : '-- Select story --');
    const chapterPlaceholder = L.SelectChapterPlaceholder || L.SelectChapter || AL.selectChapter || (isVi ? '-- Chọn chương --' : '-- Select chapter --');

    isInternalReset = true;
    $storySelect.empty().append(`<option value="">${storyPlaceholder}</option>`);
    $chapterSelect.empty().append(`<option value="">${chapterPlaceholder}</option>`);
    $('#languageCardsGrid .dynamic-lang-card').remove();

    $.get('/Admin/tblParagraphs/GetStoriesForSelect', { culture: currentCulture }, function (data) {
        console.log("✅ [DEBUG] Đã tải danh sách truyện:", data);
        if (data && data.length > 0) {
            data.forEach(x => {
                $storySelect.append(new Option(x.name, x.id, false, false));
            });
        }
        $storySelect.val('').trigger('change.select2');
        $chapterSelect.val('').trigger('change.select2');
        isInternalReset = false;
    }).fail(function (err) {
        isInternalReset = false;
        console.error("❌ [DEBUG] Lỗi tải truyện:", err);
        showToast(L.ErrConnectServer || 'Cannot load stories for modal!', 'error');
    });
}

// 3.2 Xử lý khi chọn một truyện -> Nạp chương tương ứng & Đọc đúng ngôn ngữ từ CSDL
let currentChapterXhr = null;
let isHandlingStoryChange = false;

function handleStoryChange(storyId) {
    console.log("👉 [DEBUG] Xử lý chọn StoryId:", storyId);
    if (isHandlingStoryChange) return;
    isHandlingStoryChange = true;

    const $chapterSelect = $('#chapterSelect');
    const $gridContainer = $('#languageCardsGrid');
    const currentCulture = getImportCulture();
    const isVi = currentCulture.toLowerCase().startsWith('vi');
    const chapterPlaceholder = L.SelectChapterPlaceholder || L.SelectChapter || AL.selectChapter || (isVi ? '-- Chọn chương --' : '-- Select chapter --');
    const browseText = AL.browse || L.Browse || 'Browse';
    const chooseOrDropText = AL.chooseOrDropFile || L.ChooseOrDropFile || 'Choose or drop file...';

    $chapterSelect.empty().append(`<option value="">${chapterPlaceholder}</option>`);
    $gridContainer.find('.dynamic-lang-card').remove();

    activeTargetLanguages = ['vietnamese'];
    selectedAiLanguages.clear();

    const $vnBtn = $('#btnAi_vietnamese');
    $vnBtn.data('active', false)
        .removeClass('ring-2 ring-purple-500 from-purple-600 to-indigo-600 shadow-md')
        .addClass('from-[#50C9C3] to-[#96DEDA]');
    $vnBtn.find('.ai-label').text(L.AiTranslateFromEn || 'AI Translate from English');

    if (!storyId || storyId <= 0 || storyId === "" || storyId === "all") {
        $chapterSelect.val('').trigger('change.select2');
        isHandlingStoryChange = false;
        return;
    }

    if (currentChapterXhr && currentChapterXhr.readyState !== 4) {
        currentChapterXhr.abort();
    }

    // Tải danh sách chương
    currentChapterXhr = $.get('/Admin/tblParagraphs/GetChaptersForSelect', { id: storyId, culture: currentCulture }, function (data) {
        console.log("✅ [DEBUG] Đã tải danh sách chương:", data);
        $chapterSelect.empty().append(`<option value="">${chapterPlaceholder}</option>`);
        if (data && Array.isArray(data) && data.length > 0) {
            data.forEach(x => {
                $chapterSelect.append(new Option(x.name, x.id, false, false));
            });
        }
        $chapterSelect.trigger('change.select2');
    });

    // Tải ngôn ngữ theo CSDL của truyện
    $.get('/Admin/tblParagraphs/GetLanguagesByStory', { storyId: storyId }, function (res) {
        console.log("✅ [DEBUG] Ngôn ngữ theo truyện:", res);
        if (res && res.success && res.languages) {
            const hasEnFile = ($('#fileEnglish')[0].files && $('#fileEnglish')[0].files.length > 0) || $('#dropZoneEN').attr('data-selected-filename');

            res.languages.forEach(langItem => {
                const langCode = langItem.code;

                if (langCode === 'english' || langCode === 'vietnamese') return;

                if (!activeTargetLanguages.includes(langCode)) {
                    activeTargetLanguages.push(langCode);
                }

                const def = LANGUAGE_DEFINITIONS.find(d => d.key === langCode) || {
                    key: langCode,
                    name: langItem.name || langCode.toUpperCase()
                };

                const cardHtml = `
                    <div id="card_lang_${def.key}" class="dynamic-lang-card p-4 border border-teal-100 bg-teal-50/10 rounded-2xl space-y-3 hover:border-teal-200 transition-all flex flex-col justify-between h-full min-h-[160px]">
                        <div class="space-y-3">
                            <div class="flex items-center justify-between">
                                <span class="text-sm font-bold text-cus flex items-center gap-2">
                                    ${def.name}
                                </span>
                                <a href="/assets/template/Story_${capitalizeFirstLetter(def.key)}_Template.xlsx" class="text-[11px] text-teal-600 hover:underline font-medium"><i class="fas fa-file-excel"></i> Template</a>
                            </div>
                            <div class="target-dropzone relative border-2 border-dashed border-teal-200 rounded-xl p-3 flex items-center justify-between bg-white hover:border-[#50C9C3] transition-all cursor-pointer group" data-lang-key="${def.key}">
                                <input type="file" id="file_${def.key}" data-lang-key="${def.key}" accept=".xlsx, .txt" class="target-lang-file absolute inset-0 opacity-0 cursor-pointer z-10">
                                <div class="flex items-center gap-3">
                                    <i class="fas fa-cloud-upload-alt text-teal-400 text-lg group-hover:scale-110 transition-transform"></i>
                                    <span id="fileStatus_${def.key}" class="text-xs font-medium text-gray-600 truncate max-w-[150px]">${chooseOrDropText}</span>
                                </div>
                                <span class="text-[10px] bg-teal-50 text-teal-700 px-2.5 py-1 rounded-md font-semibold border border-teal-100">${browseText}</span>
                            </div>
                        </div>
                        <button type="button" id="btnAi_${def.key}" onclick="toggleAutoAiTranslate('${def.key}', this)" ${hasEnFile ? '' : 'disabled'}
                                class="ai-btn w-full py-2 px-3 rounded-xl text-xs font-semibold shadow-sm transition-all flex items-center justify-center gap-1.5 from-[#50C9C3] to-[#96DEDA] text-white bg-gradient-to-r ${hasEnFile ? '' : 'opacity-40 cursor-not-allowed'} mt-3" 
                                data-active="false">
                            <i class="fas fa-wand-magic-sparkles text-amber-300"></i> <span class="ai-label">${L.AiTranslateFromEn || 'AI Translate from English'}</span>
                        </button>
                    </div>`;

                $gridContainer.append(cardHtml);
            });
            refreshExcelProvidedCards();
        }
        isHandlingStoryChange = false;
    }).fail(function () {
        isHandlingStoryChange = false;
    });
}

// ============================================================================
// 4. RESET FORM
// ============================================================================
// 4.3 Reset form
function resetImportForm() {
    console.log("👉 [DEBUG] Reset Import Form");
    isInternalReset = true;
    const isVi = getImportCulture().toLowerCase().startsWith('vi');
    const chapterPlaceholder = L.SelectChapterPlaceholder || L.SelectChapter || AL.selectChapter || (isVi ? '-- Chọn chương --' : '-- Select chapter --');
    const chooseOrDropText = AL.chooseOrDropFile || L.ChooseOrDropFile || 'Choose or drop file...';
    const uploadEnSourceText = L.UploadEnSourceFile || 'Upload English source file...';

    $('#storySelect').val('').trigger('change.select2');
    $('#chapterSelect').empty().append(`<option value="">${chapterPlaceholder}</option>`).val('').trigger('change.select2');

    $('#fileEnglish, #file_vietnamese, .target-lang-file').val('');
    $('#dropZoneEN, .target-dropzone').removeAttr('data-selected-filename');
    $('.target-dropzone, #dropZoneEN, .dynamic-lang-card, #card_lang_vietnamese').removeClass('ring-2 ring-red-500 bg-red-50/30');

    $('#fileStatusEN').text(uploadEnSourceText);
    $('#fileStatus_vietnamese').text(chooseOrDropText);
    $('#languageCardsGrid .dynamic-lang-card').remove();

    $('.ai-btn').prop('disabled', true).addClass('opacity-40 cursor-not-allowed')
        .removeClass('ring-2 ring-purple-500 from-purple-600 to-indigo-600 shadow-md')
        .addClass('from-[#50C9C3] to-[#96DEDA]')
        .data('active', false).find('.ai-label').text(L.AiTranslateFromEn || 'AI Translate from English');

    activeTargetLanguages = ['vietnamese'];
    selectedAiLanguages.clear();
    excelProvidedLanguages.clear();
    $('.excel-provided-badge').remove();
    isInternalReset = false;
}

function capitalizeFirstLetter(string) {
    if (!string) return '';
    return string.charAt(0).toUpperCase() + string.slice(1);
}

// ============================================================================
// 5. SỰ KIỆN TRÊN TRANG (chọn truyện/chương, chọn file theo ngôn ngữ, kéo thả)
// ============================================================================
// Đăng ký các sự kiện khi trang sẵn sàng
$(document).ready(function () {

    loadAiModels();
    $(document).on('change', '#aiModelSelect', function () {
        try { localStorage.setItem(AI_MODEL_STORE_KEY, $(this).val() || ''); } catch (e) { }
        updateAiModelNote();
    });

    $(document).on('change', '#storySelect', function () {
        if (!isInternalReset) {
            const selectedVal = $(this).val();
            console.log("👉 [DEBUG] storySelect Change:", selectedVal);
            if (selectedVal && selectedVal !== "" && selectedVal !== "all") {
                handleStoryChange(selectedVal);
            }
        }
    });

    $(document).on('change', '#chapterSelect', function () {
        console.log("👉 [DEBUG] chapterSelect Change giá trị hiện tại:", $(this).val());
    });

    // Khi chọn file Tiếng Anh Local
    $('#fileEnglish').on('change', function () {
        const file = this.files && this.files.length > 0 ? this.files[0] : null;
        console.log("👉 [DEBUG] Chọn file English Local:", file ? file.name : 'null');
        const uploadEnSourceText = L.UploadEnSourceFile || 'Upload English source file...';

        if (file) {
            $('#fileStatusEN').text(file.name);
            $('#dropZoneEN').removeAttr('data-selected-filename');
            $('.ai-btn').prop('disabled', false).removeClass('opacity-40 cursor-not-allowed');
            $('#dropZoneEN').removeClass('ring-2 ring-red-500');
        } else {
            $('#fileStatusEN').text(uploadEnSourceText);
            $('.ai-btn').prop('disabled', true).addClass('opacity-40 cursor-not-allowed');
            selectedAiLanguages.clear();
        }
        inspectEnglishSource();
    });

    // Khi chọn file ngôn ngữ phụ Local
    $(document).on('change', '.target-lang-file', function () {
        const file = this.files && this.files[0];
        const langKey = $(this).data('lang-key');
        console.log(`👉 [DEBUG] Chọn file Local cho ngôn ngữ [${langKey}]:`, file ? file.name : 'null');
        const $status = $(`#fileStatus_${langKey}`);
        const chooseOrDropText = AL.chooseOrDropFile || L.ChooseOrDropFile || 'Choose or drop file...';

        if (file) {
            $status.text(file.name);
            $(`#card_lang_${langKey} .target-dropzone`).removeAttr('data-selected-filename');
        } else {
            $status.text(chooseOrDropText);
        }
        validateLanguageCard(langKey);
    });

    // Drag & Drop Kho lưu trữ cho ô Tiếng Anh
    const dropZoneEN = document.getElementById('dropZoneEN');
    if (dropZoneEN) {
        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
            dropZoneEN.addEventListener(eventName, e => {
                e.preventDefault();
                e.stopPropagation();
            }, false);
        });

        dropZoneEN.addEventListener('drop', e => {
            e.preventDefault();
            e.stopPropagation();

            const savedStory = $('#storySelect').val();
            const savedChapter = $('#chapterSelect').val();
            const fromStorageText = L.FromStorageText || '(From storage)';

            console.log("👉 [DEBUG Drop EN] Trước khi thả file -> StoryId:", savedStory, "| ChapId:", savedChapter);

            const dt = e.dataTransfer;
            if (dt.files && dt.files.length > 0) {
                console.log("👉 [DEBUG Drop EN] Thả file từ máy tính:", dt.files[0].name);
                document.getElementById('fileEnglish').files = dt.files;
                $('#dropZoneEN').removeAttr('data-selected-filename');
                $('#fileStatusEN').text(dt.files[0].name);
            } else {
                const fileName = dt.getData('text/plain') || dt.getData('fileName');
                console.log("👉 [DEBUG Drop EN] Thả file từ FileManager:", fileName);
                if (fileName) {
                    $('#fileEnglish').val('');
                    $('#dropZoneEN').attr('data-selected-filename', fileName);
                    $('#fileStatusEN').html(`📄 ${decodeURIComponent(fileName)} ${fromStorageText}`);
                }
            }

            if (savedStory) $('#storySelect').val(savedStory).trigger('change.select2');
            if (savedChapter) $('#chapterSelect').val(savedChapter).trigger('change.select2');

            $('.ai-btn').prop('disabled', false).removeClass('opacity-40 cursor-not-allowed');
            $('#dropZoneEN').removeClass('ring-2 ring-red-500');
            inspectEnglishSource();
        });
    }

    // Drag & Drop động cho ô ngôn ngữ phụ từ FileManager
    $(document).on('dragover dragenter', '.target-dropzone', function (e) {
        e.preventDefault();
        e.stopPropagation();
    });

    $(document).on('drop', '.target-dropzone', function (e) {
        e.preventDefault();
        e.stopPropagation();

        const savedStory = $('#storySelect').val();
        const savedChapter = $('#chapterSelect').val();
        const fromStorageText = L.FromStorageText || '(From storage)';

        const dt = e.originalEvent.dataTransfer;
        const langKey = $(this).data('lang-key');
        const fileInput = document.getElementById(`file_${langKey}`);

        if (dt.files && dt.files.length > 0) {
            console.log(`👉 [DEBUG Drop ${langKey}] Thả file local:`, dt.files[0].name);
            fileInput.files = dt.files;
            $(this).removeAttr('data-selected-filename');
            $(`#fileStatus_${langKey}`).text(dt.files[0].name);
        } else {
            const fileName = dt.getData('text/plain') || dt.getData('fileName');
            console.log(`👉 [DEBUG Drop ${langKey}] Thả file FileManager:`, fileName);
            if (fileName) {
                $(`#file_${langKey}`).val('');
                $(this).attr('data-selected-filename', fileName);
                $(`#fileStatus_${langKey}`).html(`📄 ${decodeURIComponent(fileName)} ${fromStorageText}`);
            }
        }

        if (savedStory) $('#storySelect').val(savedStory).trigger('change.select2');
        if (savedChapter) $('#chapterSelect').val(savedChapter).trigger('change.select2');

        validateLanguageCard(langKey);
    });

    // Nút xác nhận Import
    $('#btnImport').off('click').on('click', function () {
        const storyId = $('#storySelect').val();
        const chapId = $('#chapterSelect').val();

        console.log("🚀 [DEBUG Confirm Import] Bắt đầu kiểm tra dữ liệu gửi đi:");

        // 1. Kiểm tra Chọn Truyện & Chương
        if (!storyId || !chapId || storyId === "" || chapId === "") {
            showToast(L.ReqSelectStoryAndChapter || 'Please select both Story and Chapter!', 'error');
            return;
        }

        // 2. Kiểm tra File Tiếng Anh Nguồn
        const inputEN = document.getElementById('fileEnglish');
        const fileEN = inputEN && inputEN.files ? inputEN.files[0] : null;
        const serverFileNameEN = $('#dropZoneEN').attr('data-selected-filename') || '';

        if (!fileEN && !serverFileNameEN) {
            showToast(L.ReqEnglishSourceFile || 'Please upload an English file as the source language!', 'error');
            $('#dropZoneEN').addClass('ring-2 ring-red-500');
            return;
        } else {
            $('#dropZoneEN').removeClass('ring-2 ring-red-500');
        }

        // 3. KIỂM TRA ĐỦ CÁC NGÔN NGỮ PHỤ (AI dịch / file / cột trong Excel tiếng Anh)
        let missingLanguages = [];

        activeTargetLanguages.forEach(langKey => {
            const $card = $(`#card_lang_${langKey}`);
            if (!isLanguageCardSatisfied(langKey)) {
                missingLanguages.push(capitalizeFirstLetter(langKey));
                $card.addClass('ring-2 ring-red-500 bg-red-50/30');
            } else {
                $card.removeClass('ring-2 ring-red-500 bg-red-50/30');
            }
        });

        if (missingLanguages.length > 0) {
            const reqMsg = (L.ReqAiOrFileTargetMsg || 'Please upload file or select AI Translate for missing languages: {0}').replace('{0}', missingLanguages.join(', '));
            showToast(reqMsg, 'error');
            return;
        }

        const formData = buildImportFormData(chapId);
        const isUsingAi = selectedAiLanguages.size > 0;
        const targetUrl = isUsingAi ? '/Admin/tblParagraphs/EnqueueImportAi' : '/Admin/tblParagraphs/ImportFromFilesDirect';

        const processingText = isUsingAi ? (L.EnqueuingStatus || 'Enqueuing AI...') : (L.Processing || 'Importing files...');
        const $btn = $(this).prop('disabled', true).html(`<i class="fas fa-spinner fa-spin mr-2"></i> ${processingText}`);

        $.ajax({
            url: targetUrl,
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (res) {
                if (res.success) {
                    showToast(res.message || 'Import successful!', 'success');
                    closeParagraphModal();

                    if (isUsingAi && res.jobId) {
                        pollHangfireJob(res.jobId, storyId, chapId);
                    } else {
                        if (typeof loadParagraphList === 'function') {
                            loadParagraphList(1);
                        } else {
                            location.reload();
                        }
                    }
                } else {
                    if (res.isMismatched || (res.message && (res.message.includes('không đồng nhất') || res.message.includes('mismatch') || res.message.includes('lệch')))) {
                        showAiFormatPrompt(res.message);
                    } else {
                        showToast(res.message || 'Failed to import!', 'error');
                    }
                }
            },
            error: function (xhr) {
                const msg = xhr.responseJSON ? xhr.responseJSON.message : (L.ErrConnectServer || 'Server connection error!');
                showToast(msg, 'error');
            },
            complete: () => $btn.prop('disabled', false).html(L.ConfirmImport || 'Confirm Import')
        });
    });
});

// ============================================================================
// 6. KIỂM TRA THẺ NGÔN NGỮ & BẬT/TẮT AI DỊCH TỰ ĐỘNG
// ============================================================================
function hasLanguageFile(langKey) {
    const fileInput = document.getElementById(`file_${langKey}`);
    const hasLocalFile = !!(fileInput && fileInput.files && fileInput.files.length > 0);
    const hasServerFile = !!$(`#card_lang_${langKey}`).find('.target-dropzone').attr('data-selected-filename');
    return hasLocalFile || hasServerFile;
}

// Ngôn ngữ đủ điều kiện import: bật AI dịch, có file riêng, hoặc có sẵn cột trong file Excel tiếng Anh
function isLanguageCardSatisfied(langKey) {
    return selectedAiLanguages.has(langKey) || hasLanguageFile(langKey) || excelProvidedLanguages.has(langKey);
}

function validateLanguageCard(langKey) {
    if (isLanguageCardSatisfied(langKey)) {
        $(`#card_lang_${langKey}`).removeClass('ring-2 ring-red-500 bg-red-50/30');
    }
}

/**
 * Gom dữ liệu form import – dùng chung cho Import thường, Import AI, AI căn chỉnh và Phân tích file.
 * Gửi đủ file tải lên LẪN file chọn từ kho (serverFileName*) cho mọi ngôn ngữ không bật AI.
 */
function buildImportFormData(chapId) {
    const formData = new FormData();
    if (chapId) formData.append('chapterId', chapId);

    const inputEN = document.getElementById('fileEnglish');
    const fileEN = inputEN && inputEN.files ? inputEN.files[0] : null;
    const serverFileNameEN = $('#dropZoneEN').attr('data-selected-filename') || '';
    if (fileEN) {
        formData.append('fileEnglish', fileEN);
    } else if (serverFileNameEN) {
        formData.append('serverFileNameEnglish', decodeURIComponent(serverFileNameEN));
    }

    if (selectedAiLanguages.size > 0) {
        formData.append('targetLanguages', Array.from(selectedAiLanguages).map(l => capitalizeFirstLetter(l)).join(','));
    }

    // Model AI đã chọn (dùng cho AI dịch và AI căn chỉnh câu lệch)
    const aiModel = $('#aiModelSelect').val();
    if (aiModel) formData.append('aiModel', aiModel);

    ['vietnamese', 'chinese', 'japanese', 'french'].forEach(langKey => {
        if (selectedAiLanguages.has(langKey)) return;
        const fileInput = document.getElementById(`file_${langKey}`);
        const serverFileName = $(`#card_lang_${langKey}`).find('.target-dropzone').attr('data-selected-filename') || '';

        if (fileInput && fileInput.files && fileInput.files.length > 0) {
            formData.append(`file${capitalizeFirstLetter(langKey)}`, fileInput.files[0]);
        } else if (serverFileName) {
            formData.append(`serverFileName${capitalizeFirstLetter(langKey)}`, decodeURIComponent(serverFileName));
        }
    });
    return formData;
}

/**
 * Nạp danh sách model AI vào ô #aiModelSelect. Model chưa có API key bị khóa.
 * Nhớ lựa chọn gần nhất (localStorage).
 */
function loadAiModels() {
    const $sel = $('#aiModelSelect');
    if (!$sel.length) return;

    $.get('/Admin/tblParagraphs/GetAiModels', function (res) {
        if (!res || !res.success) return;
        aiModels = res.models || [];
        const noKey = L.AiModelNoKey || 'chưa có API key';
        const groups = { groq: 'Groq', claude: 'Claude (Anthropic)' };

        $sel.html(Object.keys(groups).map(p => {
            const items = aiModels.filter(m => m.provider === p);
            if (!items.length) return '';
            return `<optgroup label="${groups[p]}">` + items.map(m =>
                `<option value="${m.id}" ${m.configured ? '' : 'disabled'}>${m.name}${m.configured ? '' : ' – ' + noKey}</option>`
            ).join('') + '</optgroup>';
        }).join(''));

        let saved = '';
        try { saved = localStorage.getItem(AI_MODEL_STORE_KEY) || ''; } catch (e) { }
        const usable = (id) => aiModels.some(m => m.id === id && m.configured);
        $sel.val(usable(saved) ? saved : (usable(res.defaultModel) ? res.defaultModel : (aiModels.find(m => m.configured) || {}).id || ''));
        updateAiModelNote();
    });
}

function updateAiModelNote() {
    const m = aiModels.find(x => x.id === $('#aiModelSelect').val());
    $('#aiModelNote').text(m ? m.note : (L.AiModelNone || 'Chưa có model AI nào được cấu hình API key.'));
}

function hasEnglishSource() {
    const inputEN = document.getElementById('fileEnglish');
    return !!((inputEN && inputEN.files && inputEN.files.length > 0) || $('#dropZoneEN').attr('data-selected-filename'));
}

/**
 * Đọc nhanh file tiếng Anh: nếu là Excel nhiều cột (English | Vietnamese | Chinese...) thì
 * đánh dấu các ngôn ngữ có cột là "Có sẵn trong file Excel" – không bắt chọn file / AI cho các ngôn ngữ đó.
 */
function inspectEnglishSource() {
    excelProvidedLanguages.clear();
    refreshExcelProvidedCards();
    if (inspectEnXhr) inspectEnXhr.abort();
    if (!hasEnglishSource()) return;

    const formData = new FormData();
    const inputEN = document.getElementById('fileEnglish');
    const serverFileNameEN = $('#dropZoneEN').attr('data-selected-filename') || '';
    if (inputEN && inputEN.files && inputEN.files.length > 0) {
        formData.append('fileEnglish', inputEN.files[0]);
    } else {
        formData.append('serverFileNameEnglish', decodeURIComponent(serverFileNameEN));
    }

    inspectEnXhr = $.ajax({
        url: '/Admin/tblParagraphs/InspectImportFile',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            if (!res || !res.success) {
                if (res && res.message) showToast(res.message, 'error');
                return;
            }
            if (res.isTable) {
                (res.languages || []).forEach(l => {
                    const key = String(l).toLowerCase();
                    if (key !== 'english') excelProvidedLanguages.add(key);
                });
                refreshExcelProvidedCards();
                const langs = Array.from(excelProvidedLanguages).map(capitalizeFirstLetter).join(', ');
                const msg = (L.ExcelMultiColumnDetected || 'Excel nhiều cột: {0} dòng, có sẵn {1}').replace('{0}', res.rows).replace('{1}', langs || '—');
                showToast(msg, 'info');
            }
        },
        complete: function () { inspectEnXhr = null; }
    });
}

function refreshExcelProvidedCards() {
    $('.excel-provided-badge').remove();
    excelProvidedLanguages.forEach(langKey => {
        const $card = $(`#card_lang_${langKey}`);
        if (!$card.length) return;
        const label = L.AvailableInExcel || 'Có sẵn trong file Excel';
        $card.find('.target-dropzone').first().before(
            `<div class="excel-provided-badge text-[11px] font-semibold text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-lg px-2.5 py-1.5 flex items-center gap-1.5"><i class="fas fa-table"></i> ${label}</div>`);
        validateLanguageCard(langKey);
    });
}

function toggleAutoAiTranslate(targetLang, btn) {
    const $btn = $(btn);
    const isActive = $btn.data('active') === true;

    if (isActive) {
        selectedAiLanguages.delete(targetLang);
        $btn.data('active', false)
            .removeClass('ring-2 ring-purple-500 from-purple-600 to-indigo-600 shadow-md')
            .addClass('from-[#50C9C3] to-[#96DEDA]');
        $btn.find('.ai-label').text(L.AiTranslateFromEn || 'AI Translate from English');
        const disabledMsg = (L.AiDisabledMsg || 'AI translation disabled for {0}').replace('{0}', capitalizeFirstLetter(targetLang));
        showToast(disabledMsg, 'info');
    } else {
        selectedAiLanguages.add(targetLang);
        $btn.data('active', true)
            .removeClass('from-[#50C9C3] to-[#96DEDA]')
            .addClass('ring-2 ring-purple-500 from-purple-600 to-indigo-600 shadow-md');
        $btn.find('.ai-label').html(L.AiTranslatedActive || '✨ Translated by AI');
        const enabledMsg = (L.AiEnabledMsg || 'AI translation enabled for {0}').replace('{0}', capitalizeFirstLetter(targetLang));
        showToast(enabledMsg, 'success');
    }

    validateLanguageCard(targetLang);
}

// ============================================================================
// 7. THEO DÕI TIẾN TRÌNH JOB (Hangfire)
// ============================================================================
function pollHangfireJob(jobId, storyId, chapId) {
    const $overlay = $('#aiProcessingOverlay');
    const $msg = $('#aiProcessingMessage');

    $overlay.removeClass('hidden');
    setTimeout(() => $overlay.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 50);

    const preventUnload = function (e) {
        e.preventDefault();
        return '';
    };
    window.addEventListener('beforeunload', preventUnload);

    function hideProcessingOverlay() {
        window.removeEventListener('beforeunload', preventUnload);
        $overlay.removeClass('translate-y-0 opacity-100').addClass('translate-y-0 opacity-0');
        setTimeout(() => $overlay.addClass('hidden'), 300);
    }

    const intervalId = setInterval(function () {
        $.get('/Admin/tblParagraphs/CheckJobStatus', { jobId: jobId }, function (statusRes) {
            if (statusRes.completed) {
                clearInterval(intervalId);
                hideProcessingOverlay();

                if (statusRes.state === 'Failed') {
                    const failMsg = L.ImportJobFailedMsg || 'Background job failed! Check Hangfire logs.';
                    // Thông báo lỗi cụ thể từ job (hết quota AI, AI trả sai định dạng...) – chưa có câu nào được lưu
                    showToast(statusRes.message ? `${failMsg} ${statusRes.message}` : failMsg, 'error');
                } else {
                    showToast(L.ImportJobSuccessMsg || 'Translated and imported whole chapter successfully!', 'success');
                    $('#filterStory').val(storyId).trigger('change.select2');

                    const currentCulture = getImportCulture();
                    $.get('/Admin/tblParagraphs/GetChaptersForSelect', { id: storyId, culture: currentCulture }, function (data) {
                        const allChapText = L.FilterAllChapters || 'Chapter';
                        const $chapter = $('#filterChapter').html(`<option value="all">${allChapText}</option>`);
                        data.forEach(c => $chapter.append(`<option value="${c.id}">${c.name}</option>`));
                        $chapter.val(chapId).trigger('change.select2');
                        if (typeof currentPage !== 'undefined') currentPage = 1;
                        if (typeof loadParagraphList === 'function') loadParagraphList(1);
                    });
                }
            } else if (statusRes.state === 'Processing') {
                $msg.text(L.ImportJobProcessingMsg || 'AI is translating paragraphs in batches... Synchronizing data.');
            }
        }).fail(function () {
            clearInterval(intervalId);
            hideProcessingOverlay();
            showToast(L.ErrConnectServer || 'Lost connection while polling background job!', 'error');
        });
    }, 2500);
}

// ============================================================================
// 8. PHÂN TÍCH FILE & IMPORT BẰNG AI
// ============================================================================
function triggerFileAnalysis() {
    if (!hasEnglishSource()) return;
    const formData = buildImportFormData(null);

    const analyzingText = L.AnalyzingSentenceStructure || 'Analyzing sentence structure across files...';
    $('#analysisStatus').html(`<i class="fas fa-spinner fa-spin"></i> ${analyzingText}`);

    $.ajax({
        url: '/Admin/tblParagraphs/AnalyzeImportFiles',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            if (res.success) {
                if (res.isMismatched) {
                    showAiFormatPrompt(res.mismatchedDetails);
                } else {
                    showToast(res.message, 'success');
                    $('#btnImport').prop('disabled', false);
                }
            }
        }
    });
}

function showAiFormatPrompt(details) {
    const swalTitle = L.MismatchedSentenceTitle || '⚠️ Sentence count mismatch!';
    const swalMsg = L.MismatchedAskAiMsg || 'Would you like AI to automatically align and re-segment translations to match English 1-to-1?';
    const confirmBtn = L.BtnConfirmAiFormat || '✨ AI Align & Import';
    const cancelBtn = L.BtnManualEditFile || 'I will edit files manually';

    if (typeof Swal !== 'undefined') {
        Swal.fire({
            title: swalTitle,
            html: `<p class="text-sm text-gray-600 mb-2">${details}</p>
                   <p class="text-xs font-semibold text-teal-600">${swalMsg}</p>`,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: confirmBtn,
            cancelButtonText: cancelBtn,
            confirmButtonColor: '#0d9488'
        }).then((result) => {
            if (result.isConfirmed) {
                executeAiAlignAndImport();
            }
        });
    } else {
        if (confirm(`${swalTitle}\n\n${details}\n\n${swalMsg}`)) {
            executeAiAlignAndImport();
        }
    }
}

function executeAiAlignAndImport() {
    const storyId = $('#storySelect').val();
    const chapId = $('#chapterSelect').val();

    if (!storyId || !chapId || !hasEnglishSource()) {
        showToast(L.ReqSelectStoryAndChapter || 'Please select Story, Chapter and English source file!', 'error');
        return;
    }

    // Gửi lại ĐẦY ĐỦ dữ liệu như nút Import (file kho lưu trữ + ngôn ngữ AI dịch)
    const formData = buildImportFormData(chapId);

    const sendingJobText = L.EnqueuingStatus || 'Enqueuing Hangfire task...';
    const $btn = $('#btnImport').prop('disabled', true)
        .html(`<i class="fas fa-magic fa-spin mr-2"></i> ${sendingJobText}`);

    $.ajax({
        url: '/Admin/tblParagraphs/AiAutoAlignAndImport',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            if (res.success && res.jobId) {
                showToast(L.SentTaskBackgroundSuccess || 'Sent background task successfully!', 'info');
                closeParagraphModal();
                pollHangfireJob(res.jobId, storyId, chapId);
            } else if (res.success) {
                // Không còn gì cần AI (các file đã khớp) -> server đã lưu luôn
                showToast(res.message || 'Import successful!', 'success');
                closeParagraphModal();
                if (typeof loadParagraphList === 'function') loadParagraphList(1); else location.reload();
            } else {
                showToast(res.message || 'Failed to submit AI task!', 'error');
            }
        },
        error: function (xhr) {
            const msg = xhr.responseJSON ? xhr.responseJSON.message : (L.ErrConnectServer || 'Server connection error!');
            showToast(msg, 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).html(L.ConfirmImport || 'Confirm Import');
        }
    });
}
