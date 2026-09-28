/* paragraphs-import-ai.js - Quản lý Import AI theo thứ tự chuẩn: English, Vietnamese, Chinese, Japanese, French */

// Mặc định luôn có Tiếng Việt nằm trong danh sách ngôn ngữ đích cần xử lý
let activeTargetLanguages = ['vietnamese'];

// QUAN TRỌNG: Mặc định ĐỂ RỖNG, không tick chọn AI cho bất kỳ ngôn ngữ nào
let selectedAiLanguages = new Set();

// Danh mục định nghĩa và thứ tự chuẩn bắt buộc
const LANGUAGE_DEFINITIONS = [
    { key: 'vietnamese', name: 'Vietnamese 🇻🇳', flag: '🇻🇳', fixed: true },
    { key: 'chinese', name: 'Chinese 🇨🇳', flag: '🇨🇳', fixed: false },
    { key: 'japanese', name: 'Japanese 🇯🇵', flag: '🇯🇵', fixed: false },
    { key: 'french', name: 'French 🇫🇷', flag: '🇫🇷', fixed: false }
];

$(document).ready(function () {
    // 1. Khi chọn file Tiếng Anh (Nguồn)
    $('#fileEnglish').on('change', function () {
        const file = this.files && this.files.length > 0 ? this.files[0] : null;
        if (file) {
            $('#fileStatusEN').text(file.name);
            $('.ai-btn').prop('disabled', false).removeClass('opacity-40 cursor-not-allowed');
        } else {
            $('#fileStatusEN').text('Upload English source file...');
            $('.ai-btn').prop('disabled', true).addClass('opacity-40 cursor-not-allowed');
            // Reset trạng thái các nút AI khi bỏ file nguồn
            selectedAiLanguages.clear();
            $('.ai-btn').data('active', false)
                .removeClass('ring-2 ring-purple-500 from-purple-600 to-indigo-600 shadow-md')
                .addClass('from-[#50C9C3] to-[#96DEDA]');
            $('.ai-btn .ai-label').text('AI Translate from English');
        }
    });

    // 2. Lắng nghe upload file cho các ngôn ngữ đích
    $(document).on('change', '.target-lang-file', function () {
        const file = this.files && this.files[0];
        const langKey = $(this).data('lang-key');
        const $status = $(`#fileStatus_${langKey}`);
        const $aiBtn = $(`#btnAi_${langKey}`);

        if (file) {
            $status.text(file.name);
            // Nếu đã tải file lên, tự động tắt nút AI để tránh trùng lặp
            if ($aiBtn.data('active') === true) {
                toggleAutoAiTranslate(langKey, $aiBtn[0]);
            }
        } else {
            $status.text(`Choose or drop file...`);
        }
        validateLanguageCard(langKey);
    });

    // 3. Nút xác nhận Import
    $('#btnImport').off('click').on('click', function () {
        const storyId = $('#storySelect').val();
        const chapId = $('#chapterSelect').val();
        const inputEN = document.getElementById('fileEnglish');
        const fileEN = inputEN && inputEN.files ? inputEN.files[0] : null;

        if (!storyId || !chapId) {
            showToast('Please select both Story and Chapter!', 'error');
            return;
        }
        if (!fileEN) {
            showToast('Please upload an English file as the source language!', 'error');
            return;
        }

        // ========================================================
        // KIỂM TRA BẮT BUỘC: MỖI NGÔN NGỮ ĐÍCH PHẢI CÓ FILE HOẶC AI DỊCH
        // ========================================================
        let hasError = false;
        let missingLangs = [];

        activeTargetLanguages.forEach(langKey => {
            const isAiSelected = selectedAiLanguages.has(langKey);
            const fileInput = document.getElementById(`file_${langKey}`);
            const hasFile = fileInput && fileInput.files && fileInput.files.length > 0;

            // Nếu người dùng không tick AI và cũng không tải file lên
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
            showToast(`Please enable AI Translation or upload a file for: ${missingLangs.join(', ')}`, 'error');
            return;
        }

        // Tạo payload
        const formData = new FormData();
        formData.append('chapterId', chapId);
        formData.append('fileEnglish', fileEN);

        // Danh sách ngôn ngữ cho AI dịch
        const targetLangs = Array.from(selectedAiLanguages).map(l => capitalizeFirstLetter(l)).join(',');
        formData.append('targetLanguages', targetLangs);

        // Đính kèm các file upload thủ công
        activeTargetLanguages.forEach(langKey => {
            const fileInput = document.getElementById(`file_${langKey}`);
            if (fileInput && fileInput.files && fileInput.files.length > 0) {
                formData.append(`file${capitalizeFirstLetter(langKey)}`, fileInput.files[0]);
            }
        });

        const $btn = $(this).prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-2"></i> Enqueuing...');

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
                const msg = xhr.responseJSON ? xhr.responseJSON.message : 'Server connection error!';
                showToast(msg, 'error');
            },
            complete: () => $btn.prop('disabled', false).html('Confirm Import')
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

// Bật/tắt nút AI dịch
function toggleAutoAiTranslate(targetLang, btn) {
    const $btn = $(btn);
    const isActive = $btn.data('active') === true;

    if (isActive) {
        selectedAiLanguages.delete(targetLang);
        $btn.data('active', false)
            .removeClass('ring-2 ring-purple-500 from-purple-600 to-indigo-600 shadow-md')
            .addClass('from-[#50C9C3] to-[#96DEDA]');
        $btn.find('.ai-label').text('AI Translate from English');
        showToast(`AI translation disabled for ${capitalizeFirstLetter(targetLang)}`, 'info');
    } else {
        selectedAiLanguages.add(targetLang);
        $btn.data('active', true)
            .removeClass('from-[#50C9C3] to-[#96DEDA]')
            .addClass('ring-2 ring-purple-500 from-purple-600 to-indigo-600 shadow-md');
        $btn.find('.ai-label').html('✨ Translated by AI');
        showToast(`AI translation enabled for ${capitalizeFirstLetter(targetLang)}`, 'success');
    }

    validateLanguageCard(targetLang);
}

function openParagraphModal() {
    $('#importParagraphModal').removeClass('hidden').addClass('flex');
    setTimeout(() => {
        $('#importContent').removeClass('scale-95 opacity-0').addClass('scale-100 opacity-100');
    }, 10);
    loadModalStories();
}

function closeParagraphModal() {
    $('#importContent').removeClass('scale-100 opacity-100').addClass('scale-95 opacity-0');
    setTimeout(() => {
        $('#importParagraphModal').removeClass('flex').addClass('hidden');
        resetImportForm();
    }, 250);
}

function loadModalStories() {
    $('#storySelect').html('<option value="">-- Select story --</option>');
    $('#chapterSelect').html('<option value="">-- Select chapter --</option>');
    $('#dynamicExtraLanguagesContainer').empty();

    $.get('/Admin/tblParagraphs/GetStoriesForSelect', function (data) {
        data.forEach(x => $('#storySelect').append(`<option value="${x.id}">${x.name}</option>`));
        if ($('#storySelect').hasClass("select2-hidden-accessible")) {
            $('#storySelect').trigger('change.select2');
        }
    });
}

// Bắt sự kiện thay đổi truyện qua cả 2 kênh: change và select2:select
$(document).on('change select2:select', '#storySelect', function () {
    handleStoryChange($(this).val());
});

function handleStoryChange(storyId) {
    $('#chapterSelect').html('<option value="">-- Select chapter --</option>');
    $('#dynamicExtraLanguagesContainer').empty();

    // Mặc định ngôn ngữ đích luôn có Tiếng Việt
    activeTargetLanguages = ['vietnamese'];
    // Mặc định xóa sạch, không tick AI
    selectedAiLanguages.clear();

    // Reset nút AI Tiếng Việt về trạng thái chưa kích hoạt
    const $vnBtn = $('#btnAi_vietnamese');
    $vnBtn.data('active', false)
        .removeClass('ring-2 ring-purple-500 from-purple-600 to-indigo-600 shadow-md')
        .addClass('from-[#50C9C3] to-[#96DEDA]');
    $vnBtn.find('.ai-label').text('AI Translate from English');

    if (!storyId || storyId <= 0) return;

    // 1. Tải danh sách Chapters
    $.get('/Admin/tblParagraphs/GetChaptersForSelect', { id: storyId }, function (data) {
        data.forEach(x => $('#chapterSelect').append(`<option value="${x.id}">${x.name}</option>`));
        if ($('#chapterSelect').hasClass("select2-hidden-accessible")) {
            $('#chapterSelect').trigger('change.select2');
        }
    });

    // 2. Lấy danh sách ngôn ngữ của Story từ DB
    $.get('/Admin/tblParagraphs/GetLanguagesByStory', { storyId: storyId }, function (res) {
        if (!res || !res.success || !res.languages) return;

        // Chuẩn hóa danh sách mã ngôn ngữ về chữ thường
        const supportedCodes = res.languages.map(l => (l.code || '').toLowerCase());
        const hasEnFile = $('#fileEnglish')[0].files && $('#fileEnglish')[0].files.length > 0;
        const $extraContainer = $('#dynamicExtraLanguagesContainer');

        // Lọc các ngôn ngữ bổ sung theo đúng thứ tự: Chinese -> Japanese -> French
        const extraDefs = LANGUAGE_DEFINITIONS.filter(d => !d.fixed);

        extraDefs.forEach(def => {
            // Kiểm tra xem truyện này có chứa ngôn ngữ đó không
            const isMatch = supportedCodes.some(c => c === def.key || c.includes(def.key));

            if (isMatch) {
                activeTargetLanguages.push(def.key);

                // MẶC ĐỊNH: data-active="false" (Không tự động tick AI)
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
                            <i class="fas fa-wand-magic-sparkles text-amber-300"></i> <span class="ai-label">AI Translate from English</span>
                        </button>
                    </div>`;

                $extraContainer.append(cardHtml);
            }
        });
    });
}

function pollHangfireJob(jobId, storyId, chapId) {
    const $overlay = $('#aiProcessingOverlay');
    const $msg = $('#aiProcessingMessage');

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
                    showToast('Background job failed! Check Hangfire logs.', 'error');
                } else {
                    showToast('Translated and imported whole chapter successfully!', 'success');
                    $('#filterStory').val(storyId).trigger('change.select2');

                    $.get('/Admin/tblParagraphs/GetChaptersForSelect', { id: storyId }, function (data) {
                        const $chapter = $('#filterChapter').html('<option value="all">Chapter</option>');
                        data.forEach(c => $chapter.append(`<option value="${c.id}">${c.name}</option>`));
                        $chapter.val(chapId).trigger('change.select2');
                        currentPage = 1;
                        loadParagraphList(1);
                    });
                }
            } else if (statusRes.state === 'Processing') {
                $msg.text('AI is translating paragraphs in batches... Synchronizing data.');
            }
        }).fail(function () {
            clearInterval(intervalId);
            hideProcessingOverlay();
            showToast('Lost connection while polling background job!', 'error');
        });
    }, 2500);
}

function resetImportForm() {
    $('#storySelect, #chapterSelect, #fileEnglish, #file_vietnamese').val('');
    $('#fileStatusEN').text('Upload English source file...');
    $('#fileStatus_vietnamese').text('Choose or drop file...');
    $('#dynamicExtraLanguagesContainer').empty();

    $('.ai-btn').prop('disabled', true).addClass('opacity-40 cursor-not-allowed')
        .removeClass('ring-2 ring-purple-500 from-purple-600 to-indigo-600 shadow-md')
        .addClass('from-[#50C9C3] to-[#96DEDA]')
        .data('active', false).find('.ai-label').text('AI Translate from English');

    activeTargetLanguages = ['vietnamese'];
    selectedAiLanguages.clear();
}

function capitalizeFirstLetter(string) {
    if (!string) return '';
    return string.charAt(0).toUpperCase() + string.slice(1);
}