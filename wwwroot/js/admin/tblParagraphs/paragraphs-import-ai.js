/* paragraphs-import-ai.js - Quản lý Import AI theo thứ tự chuẩn: English, Vietnamese, Chinese, Japanese, French */

var L = window.ADMIN_LANG || {};

let activeTargetLanguages = ['vietnamese'];
let selectedAiLanguages = new Set();
let isInternalReset = false; // Flag chặn vòng lặp sự kiện của Select2

const LANGUAGE_DEFINITIONS = [
    { key: 'vietnamese', name: 'Vietnamese 🇻🇳', flag: '🇻🇳', fixed: true },
    { key: 'chinese', name: 'Chinese 🇨🇳', flag: '🇨🇳', fixed: false },
    { key: 'japanese', name: 'Japanese 🇯🇵', flag: '🇯🇵', fixed: false },
    { key: 'french', name: 'French 🇫🇷', flag: '🇫🇷', fixed: false }
];

function initImportSelect2() {
    const $modal =$('#importParagraphModal');

    // Chỉ khởi tạo Select2 nếu chưa từng được khởi tạo
    if (!$('#storySelect').hasClass("select2-hidden-accessible")) {
        $('#storySelect').select2({
            dropdownParent: $modal,
            width: '100%',
            placeholder: '-- Select story --',
            allowClear: false,
            minimumResultsForSearch: Infinity
        });
    }

    if (!$('#chapterSelect').hasClass("select2-hidden-accessible")) {
        $('#chapterSelect').select2({
            dropdownParent: $modal,
            width: '100%',
            placeholder: '-- Select chapter --',
            allowClear: false,
            minimumResultsForSearch: Infinity
        });
    }
}

function openParagraphModal() {
    const $modal =$('#importParagraphModal');
    const $content =$('#importContent');
    
    if ($modal.length === 0) {
        console.error("Không tìm thấy modal #importParagraphModal trong DOM!");
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
    const $modal =$('#importParagraphModal');
    const $content =$('#importContent');

    $content.removeClass('scale-100 opacity-100').addClass('scale-95 opacity-0');
    setTimeout(() => {
        $modal.removeClass('flex').addClass('hidden');
        resetImportForm();
    }, 250);
}

// 1. Nạp danh sách truyện vào select của Modal (Chỉ nạp khi mở modal)
function loadModalStories() {
    const $storySelect =$('#storySelect');
    const $chapterSelect =$('#chapterSelect');

    isInternalReset = true; // Bật cờ để không kích hoạt handleStoryChange khi reset dữ liệu
    $storySelect.empty().append('<option value="">-- Select story --</option>');
    $chapterSelect.empty().append('<option value="">-- Select chapter --</option>');
    $('#dynamicExtraLanguagesContainer').empty();

    $.get('/Admin/tblParagraphs/GetStoriesForSelect', function (data) {
        if (data && data.length > 0) {
            data.forEach(x => {
                $storySelect.append(new Option(x.name, x.id, false, false));
            });
        }
        $storySelect.val('').trigger('change.select2');$chapterSelect.val('').trigger('change.select2');
        isInternalReset = false; // Tắt cờ sau khi hoàn tất nạp danh sách
    }).fail(function () {
        isInternalReset = false;
        showToast(L.ErrConnectServer || 'Cannot load stories for modal!', 'error');
    });
}

// 2. Xử lý khi chọn một truyện: KHÔNG ĐƯỢC chạm vào $storySelect ở đây
function handleStoryChange(storyId) {
    const $chapterSelect =$('#chapterSelect');
    const $extraContainer =$('#dynamicExtraLanguagesContainer');

    $chapterSelect.empty().append('<option value="">-- Select chapter --</option>');
    $extraContainer.empty();

    activeTargetLanguages = ['vietnamese'];
    selectedAiLanguages.clear();

    const $vnBtn =$('#btnAi_vietnamese');
    $vnBtn.data('active', false)
        .removeClass('ring-2 ring-purple-500 from-purple-600 to-indigo-600 shadow-md')
        .addClass('from-[#50C9C3] to-[#96DEDA]');
    $vnBtn.find('.ai-label').text(L.AiTranslateFromEn || 'AI Translate from English');

    if (!storyId || storyId <= 0 || storyId === "") {
        $chapterSelect.val('').trigger('change.select2');
        return;
    }

    // A. Nạp danh sách chương
    $.get('/Admin/tblParagraphs/GetChaptersForSelect', { id: storyId }, function (data) {
        if (data && data.length > 0) {
            data.forEach(x => {
                $chapterSelect.append(new Option(x.name, x.id, false, false));
            });
        }
        $chapterSelect.val('').trigger('change.select2');
    });

    // B. Nạp danh sách ngôn ngữ tương ứng với truyện
    $.get('/Admin/tblParagraphs/GetLanguagesByStory', { storyId: storyId }, function (res) {
        if (!res) return;

        let rawLangString = "";
        if (typeof res === 'string') {
            rawLangString = res;
        } else if (res.lang) {
            rawLangString = res.lang;
        } else if (res.languages) {
            rawLangString = Array.isArray(res.languages) 
                ? res.languages.map(x => (typeof x === 'string' ? x : (x.code || x.name || ''))).join(',')
                : String(res.languages);
        } else if (Array.isArray(res)) {
            rawLangString = res.map(x => (typeof x === 'string' ? x : (x.code || x.name || ''))).join(',');
        }

        const normalizedStr = rawLangString.toLowerCase();
        const hasEnFile = $('#fileEnglish')[0].files && $('#fileEnglish')[0].files.length > 0;
        const extraDefs = LANGUAGE_DEFINITIONS.filter(d => !d.fixed);

        extraDefs.forEach(def => {
            let isMatch = false;

            if (def.key === 'chinese') {
                isMatch = normalizedStr.includes('trung') || normalizedStr.includes('zh') || normalizedStr.includes('chinese');
            } else if (def.key === 'japanese') {
                isMatch = normalizedStr.includes('nhật') || normalizedStr.includes('ja') || normalizedStr.includes('japanese');
            } else if (def.key === 'french') {
                isMatch = normalizedStr.includes('pháp') || normalizedStr.includes('fr') || normalizedStr.includes('french');
            }

            if (isMatch) {
                activeTargetLanguages.push(def.key);

                const cardHtml = `
                    <div id="card_lang_${def.key}" class="p-4 border border-teal-100 bg-teal-50/10 rounded-2xl space-y-3 hover:border-teal-200 transition-all flex flex-col justify-between">
                        <div class="space-y-3">
                            <div class="flex items-center justify-between">
                                <span class="text-sm font-bold text-cus flex items-center gap-2">
                                    ${def.name} <span class="text-[10px] text-red-500 font-extrabold">* Required</span>
                                </span>
                                <a href="/assets/template/Story_${capitalizeFirstLetter(def.key)}_Template.xlsx" class="text-[11px] text-teal-600 hover:underline font-medium"><i class="fas fa-file-excel"></i> Template</a>
                            </div>
                            <div class="relative border-2 border-dashed border-teal-200 rounded-xl p-3 flex items-center justify-between bg-white hover:border-[#50C9C3] transition-all cursor-pointer group">
                                <input type="file" id="file_${def.key}" data-lang-key="${def.key}" accept=".xlsx, .xls, .txt" class="target-lang-file absolute inset-0 opacity-0 cursor-pointer z-10">
                                <div class="flex items-center gap-3">
                                    <i class="fas fa-cloud-upload-alt text-teal-400 text-lg group-hover:scale-110 transition-transform"></i>
                                    <span id="fileStatus_${def.key}" class="text-xs font-medium text-gray-600 truncate max-w-[150px]">Choose or drop file...</span>
                                </div>
                                <span class="text-[10px] bg-teal-50 text-teal-700 px-2.5 py-1 rounded-md font-semibold border border-teal-100">Browse</span>
                            </div>
                        </div>
                        <button type="button" id="btnAi_${def.key}" onclick="toggleAutoAiTranslate('${def.key}', this)" ${hasEnFile ? '' : 'disabled'}
                                class="ai-btn w-full py-2 px-3 rounded-xl text-xs font-semibold shadow-sm transition-all flex items-center justify-center gap-1.5 from-[#50C9C3] to-[#96DEDA] text-white bg-gradient-to-r ${hasEnFile ? '' : 'opacity-40 cursor-not-allowed'}" 
                                data-active="false">
                            <i class="fas fa-wand-magic-sparkles text-amber-300"></i> <span class="ai-label">${L.AiTranslateFromEn || 'AI Translate from English'}</span>
                        </button>
                    </div>`;

                $extraContainer.append(cardHtml);
            }
        });
    });
}

// 3. Reset form
function resetImportForm() {
    isInternalReset = true;
    $('#storySelect').val('').trigger('change.select2');
    $('#chapterSelect').empty().append('<option value="">-- Select chapter --</option>').val('').trigger('change.select2');
    $('#fileEnglish, #file_vietnamese').val('');
    $('#fileStatusEN').text('Upload English source file...');
    $('#fileStatus_vietnamese').text('Choose or drop file...');
    $('#dynamicExtraLanguagesContainer').empty();

    $('.ai-btn').prop('disabled', true).addClass('opacity-40 cursor-not-allowed')
        .removeClass('ring-2 ring-purple-500 from-purple-600 to-indigo-600 shadow-md')
        .addClass('from-[#50C9C3] to-[#96DEDA]')
        .data('active', false).find('.ai-label').text(L.AiTranslateFromEn || 'AI Translate from English');

    activeTargetLanguages = ['vietnamese'];
    selectedAiLanguages.clear();
    isInternalReset = false;
}

function capitalizeFirstLetter(string) {
    if (!string) return '';
    return string.charAt(0).toUpperCase() + string.slice(1);
}

// Đăng ký các sự kiện 1 lần duy nhất trong $(document).ready
$(document).ready(function () {
    // Chỉ kích hoạt xử lý khi người dùng thực sự chọn từ giao diện Select2
    $(document).on('select2:select', '#storySelect', function (e) {
        if (!isInternalReset) {
            const selectedVal = e.params.data.id;
            handleStoryChange(selectedVal);
        }
    });

    $('#fileEnglish').on('change', function () {
        const file = this.files && this.files.length > 0 ? this.files[0] : null;
        if (file) {
            $('#fileStatusEN').text(file.name);
            $('.ai-btn').prop('disabled', false).removeClass('opacity-40 cursor-not-allowed');
        } else {
            $('#fileStatusEN').text('Upload English source file...');
            $('.ai-btn').prop('disabled', true).addClass('opacity-40 cursor-not-allowed');
            selectedAiLanguages.clear();
            $('.ai-btn').data('active', false)
                .removeClass('ring-2 ring-purple-500 from-purple-600 to-indigo-600 shadow-md')
                .addClass('from-[#50C9C3] to-[#96DEDA]');
            $('.ai-btn .ai-label').text(L.AiTranslateFromEn || 'AI Translate from English');
        }
    });

    $(document).on('change', '.target-lang-file', function () {
        const file = this.files && this.files[0];
        const langKey = $(this).data('lang-key');
        const $status =$(`#fileStatus_${langKey}`);
        const $aiBtn =$(`#btnAi_${langKey}`);

        if (file) {
            $status.text(file.name);
            if ($aiBtn.data('active') === true) {
                toggleAutoAiTranslate(langKey, $aiBtn[0]);             }         } else {$status.text(`Choose or drop file...`);
        }
        validateLanguageCard(langKey);
    });

    $('#btnImport').off('click').on('click', function () {
        const storyId = $('#storySelect').val();
        const chapId = $('#chapterSelect').val();
        const inputEN = document.getElementById('fileEnglish');
        const fileEN = inputEN && inputEN.files ? inputEN.files[0] : null;

        if (!storyId || !chapId) {
            showToast(L.ReqSelectStoryAndChapter || 'Please select both Story and Chapter!', 'error');
            return;
        }
        if (!fileEN) {
            showToast(L.ReqEnglishSourceFile || 'Please upload an English file as the source language!', 'error');
            return;
        }

        let hasError = false;
        let missingLangs = [];

        activeTargetLanguages.forEach(langKey => {
            const isAiSelected = selectedAiLanguages.has(langKey);
            const fileInput = document.getElementById(`file_${langKey}`);
            const hasFile = fileInput && fileInput.files && fileInput.files.length > 0;

            if (!isAiSelected && !hasFile) {
                hasError = true;
                const def = LANGUAGE_DEFINITIONS.find(d => d.key === langKey);
                missingLangs.push(def ? def.name : langKey);
                $(`#card_lang_${langKey}`).addClass('ring-2 ring-red-500 bg-red-50/30');
            } else {
                $(`#card_lang_${langKey}`).removeClass('ring-2 ring-red-500 bg-red-50/30');
            }
        });

        if (hasError) {
            const missingTemplate = L.ReqAiOrFileTarget || 'Please enable AI Translation or upload a file for: {0}';
            showToast(missingTemplate.replace('{0}', missingLangs.join(', ')), 'error');
            return;
        }

        const formData = new FormData();
        formData.append('chapterId', chapId);
        formData.append('fileEnglish', fileEN);

        const targetLangs = Array.from(selectedAiLanguages).map(l => capitalizeFirstLetter(l)).join(',');
        formData.append('targetLanguages', targetLangs);

        activeTargetLanguages.forEach(langKey => {
            const fileInput = document.getElementById(`file_${langKey}`);
            if (fileInput && fileInput.files && fileInput.files.length > 0) {
                formData.append(`file${capitalizeFirstLetter(langKey)}`, fileInput.files[0]);
            }
        });

        const enqueuingText = L.EnqueuingStatus || 'Enqueuing...';
        const $btn =$(this).prop('disabled', true).html(`<i class="fas fa-spinner fa-spin mr-2"></i> ${enqueuingText}`);

        $.ajax({
            url: '/Admin/tblParagraphs/EnqueueImportAi',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (res) {
                if (res.success) {
                    showToast(res.message || 'Queued background process successfully!', 'info');
                    closeParagraphModal();
                    if (res.jobId) pollHangfireJob(res.jobId, storyId, chapId);
                    else setTimeout(() => loadParagraphList(1), 3000);
                } else {
                    showToast(res.message || 'Failed to submit request!', 'error');
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

function validateLanguageCard(langKey) {
    const isAi = selectedAiLanguages.has(langKey);
    const fileInput = document.getElementById(`file_${langKey}`);
    const hasFile = fileInput && fileInput.files && fileInput.files.length > 0;

    if (isAi || hasFile) {
        $(`#card_lang_${langKey}`).removeClass('ring-2 ring-red-500 bg-red-50/30');
    }
}

function toggleAutoAiTranslate(targetLang, btn) {
    const $btn =$(btn);
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

function pollHangfireJob(jobId, storyId, chapId) {
    const $overlay =$('#aiProcessingOverlay');
    const $msg =$('#aiProcessingMessage');

    $overlay.removeClass('hidden');
    setTimeout(() => $overlay.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 50);

    const preventUnload = function (e) {
        e.preventDefault();
        e.returnValue = 'Background translation is running. Are you sure you want to leave?';
        return e.returnValue;
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
                    showToast(L.ImportJobFailedMsg || 'Background job failed! Check Hangfire logs.', 'error');
                } else {
                    showToast(L.ImportJobSuccessMsg || 'Translated and imported whole chapter successfully!', 'success');
                    $('#filterStory').val(storyId).trigger('change.select2');

                    $.get('/Admin/tblParagraphs/GetChaptersForSelect', { id: storyId }, function (data) {
                        const allChapText = L.FilterAllChapters || 'Chapter';
                        const $chapter =$('#filterChapter').html(`<option value="all">${allChapText}</option>`);
                        data.forEach(c => $chapter.append(`<option value="${c.id}">${c.name}</option>`));
                        $chapter.val(chapId).trigger('change.select2');
                        currentPage = 1;
                        loadParagraphList(1);
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