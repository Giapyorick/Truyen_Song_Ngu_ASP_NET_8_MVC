/* stories-crud.js - Xử lý API, Bảng dữ liệu, Lọc, CRUD form, Xóa nhiều & Import/Export Excel */

const L = window.ADMIN_LANG || {};

let currentPage = 1;
const pageSize = 5;

// ==================== CÁC HÀM XÁC ĐỊNH NGÔN NGỮ HIỆN TẠI ====================
function getCurrentCulture() {
    // 1. Kiểm tra cookie ASP.NET Localization
    const cookies = document.cookie.split(';');
    for (let c of cookies) {
        c = c.trim();
        if (c.indexOf('.AspNetCore.Culture=') === 0) {
            const val = decodeURIComponent(c.substring('.AspNetCore.Culture='.length));
            const match = val.match(/uic=([^|;]+)/) || val.match(/c=([^|;]+)/);
            if (match && match[1]) return match[1];
        }
    }
    // 2. Kiểm tra thẻ html lang
    const htmlLang = $('html').attr('lang');
    if (htmlLang) return htmlLang;

    return 'en-US';
}

function checkIsViMode() {
    return getCurrentCulture().toLowerCase().indexOf('vi') === 0;
}

// 1. Cập nhật nhãn và giao diện Modal theo ngôn ngữ đang chọn
function updateLanguageUIHeader() {
    const isVi = checkIsViMode();

    if (isVi) {
        // GIAO DIỆN TIẾNG VIỆT
        $('#currentLangFlag').text('🇻🇳');
        $('#currentLangTitleDesc').html('Bản nhập chính: <strong>Tiếng Việt</strong>');
        $('#btnToggleTranslateLabel').html('Thêm / Chỉnh sửa Tiếng Anh 🇺🇸');
        $('#lblTitlePrimary').html('Tiêu đề truyện (Tiếng Việt) <span class="text-red-500">*</span>');
        $('#lblDescPrimary').text('Mô tả truyện (Tiếng Việt)');
        $('#primaryTitle').attr('placeholder', 'Nhập tiêu đề truyện bằng tiếng Việt...');
        $('#primaryDesc').attr('placeholder', 'Nhập mô tả truyện bằng tiếng Việt...');

        $('#secondaryPanelHeader').html('<i class="fas fa-layer-group text-teal-600"></i> Bản dịch Tiếng Anh (English Translation)');
        $('#secondaryPanelNotice').text('* Dịch sang Tiếng Anh để phục vụ độc giả quốc tế.');
        $('#btnAiTranslateLabel').text('Dịch sang Tiếng Anh bằng AI');
        $('#lblRefBoxTitle').html('<i class="fas fa-eye text-gray-400"></i> Bản gốc Tiếng Việt (Tham khảo đối chiếu)');
        $('#lblRefTitle').text('Tiêu đề Tiếng Việt gốc');
        $('#lblRefDesc').text('Mô tả Tiếng Việt gốc');

        $('#lblTargetHeader').html('<i class="fas fa-pen text-teal-500"></i> Nhập bản dịch Tiếng Anh 🇺🇸');
        $('#lblTargetTitle').text('Tiêu đề Tiếng Anh (English)');
        $('#lblTargetDesc').text('Mô tả Tiếng Anh (English)');
        $('#targetTitle').attr('placeholder', 'Enter title in English...');
        $('#targetDesc').attr('placeholder', 'Enter description in English...');
    } else {
        // GIAO DIỆN TIẾNG ANH (CHUẨN HÓA TOÀN BỘ TIẾNG ANH)
        $('#currentLangFlag').text('🇺🇸');
        $('#currentLangTitleDesc').html('Primary Version: <strong>English</strong>');
        $('#btnToggleTranslateLabel').html('Add / Edit Vietnamese 🇻🇳');
        $('#lblTitlePrimary').html('Story Title (English) <span class="text-red-500">*</span>');
        $('#lblDescPrimary').text('Story Description (English)');
        $('#primaryTitle').attr('placeholder', 'Enter story title in English...');
        $('#primaryDesc').attr('placeholder', 'Enter story description in English...');

        $('#secondaryPanelHeader').html('<i class="fas fa-layer-group text-teal-600"></i> Vietnamese Translation (Bản dịch tiếng Việt)');
        $('#secondaryPanelNotice').text('* Translate into Vietnamese for local bilingual readers.');
        $('#btnAiTranslateLabel').text('Auto-translate to Vietnamese with AI');
        $('#lblRefBoxTitle').html('<i class="fas fa-eye text-gray-400"></i> Original English (Reference)');
        $('#lblRefTitle').text('Original English Title');
        $('#lblRefDesc').text('Original English Description');

        $('#lblTargetHeader').html('<i class="fas fa-pen text-teal-500"></i> Vietnamese Input (Tiếng Việt) 🇻🇳');
        $('#lblTargetTitle').text('Vietnamese Title (Tiêu đề TV)');
        $('#lblTargetDesc').text('Vietnamese Description (Mô tả TV)');
        $('#targetTitle').attr('placeholder', 'Nhập tiêu đề tiếng Việt...');
        $('#targetDesc').attr('placeholder', 'Nhập mô tả tiếng Việt...');
    }
}
function toggleTranslationPanel() {
    const $panel = $('#secondaryLangPanel');
    const isVi = checkIsViMode();

    if (isVi) {
        storyTranslations.vi.title = ($('#primaryTitle').val() || '').trim();
        storyTranslations.vi.desc = ($('#primaryDesc').val() || '').trim();

        $('#refTitle').val(storyTranslations.vi.title);
        $('#refDesc').val(storyTranslations.vi.desc);
        $('#targetTitle').val(storyTranslations.en.title);
        $('#targetDesc').val(storyTranslations.en.desc);
    } else {
        storyTranslations.en.title = ($('#primaryTitle').val() || '').trim();
        storyTranslations.en.desc = ($('#primaryDesc').val() || '').trim();

        $('#refTitle').val(storyTranslations.en.title);
        $('#refDesc').val(storyTranslations.en.desc);
        $('#targetTitle').val(storyTranslations.vi.title);
        $('#targetDesc').val(storyTranslations.vi.desc);
    }

    $panel.toggleClass('hidden');
}
$(document).on('input', '#targetTitle', function () {
    const isVi = checkIsViMode();
    if (isVi) storyTranslations.en.title = $(this).val();
    else storyTranslations.vi.title = $(this).val();
});
$(document).on('input', '#targetDesc', function () {
    const isVi = checkIsViMode();
    if (isVi) storyTranslations.en.desc = $(this).val();
    else storyTranslations.vi.desc = $(this).val();
});

// 3. Hàm gọi AI dịch tự động từ bản gốc sang bản phụ
function translateCurrentViaAi() {
    const isVi = checkIsViMode();
    const sourceTitle = ($('#primaryTitle').val() || $('#refTitle').val() || '').trim();
    const sourceDesc = ($('#primaryDesc').val() || $('#refDesc').val() || '').trim();

    // Xác định chiều dịch linh hoạt
    const fromLang = isVi ? 'Vietnamese' : 'English';
    const toLang = isVi ? 'English' : 'Vietnamese';

    if (!sourceTitle && !sourceDesc) {
        showToast(isVi ? 'Vui lòng nhập tiêu đề hoặc mô tả nguồn trước khi dịch!' : 'Please enter source title or description before translating!', 'error');
        return;
    }

    const $btn =$('#btnAiTranslate');
    const loadingText = isVi ? 'Đang dịch sang Tiếng Anh...' : 'Đang dịch sang Tiếng Việt...';
    $btn.prop('disabled', true).html(`<i class="fas fa-spinner fa-spin mr-1"></i> ${loadingText}`);
    showToast(isVi ? 'AI đang dịch sang Tiếng Anh...' : 'AI đang dịch sang Tiếng Việt...', 'info');

    let hasUpdated = false;
    let requests = [];

    // Dịch Title
    if (sourceTitle) {
        const titleReq = $.post('/Admin/tblStories/TranslateWithAi', { 
            text: sourceTitle, 
            fromLang: fromLang, 
            toLang: toLang 
        }).done(function (res) {
            if (res.success && res.result) {
                $('#targetTitle').val(res.result);
                if (isVi) storyTranslations.en.title = res.result;
                else storyTranslations.vi.title = res.result;
                hasUpdated = true;
            } else {
                showToast(res.message || 'Lỗi khi dịch tiêu đề!', 'error');
            }
        });
        requests.push(titleReq);
    }

    // Dịch Description
    if (sourceDesc) {
        const descReq = $.post('/Admin/tblStories/TranslateWithAi', { 
            text: sourceDesc, 
            fromLang: fromLang, 
            toLang: toLang 
        }).done(function (res) {
            if (res.success && res.result) {
                $('#targetDesc').val(res.result);
                if (isVi) storyTranslations.en.desc = res.result;
                else storyTranslations.vi.desc = res.result;
                hasUpdated = true;
            } else {
                showToast(res.message || 'Lỗi khi dịch mô tả!', 'error');
            }
        });
        requests.push(descReq);
    }

    $.when.apply($, requests).always(function () {
        const btnLabel = isVi ? 'Dịch sang Tiếng Anh bằng AI' : 'Dịch sang Tiếng Việt bằng AI';
        $btn.prop('disabled', false).html(`<i class="fas fa-wand-magic-sparkles text-amber-200 mr-1"></i> ${btnLabel}`);
        if (hasUpdated) {
            showToast(isVi ? 'AI đã dịch sang Tiếng Anh thành công!' : 'AI đã dịch sang Tiếng Việt thành công!', 'success');
        }
    });
}
// Mở modal Thêm hoặc Sửa truyện
// Mở modal Thêm hoặc Sửa truyện
function openModal(mode, id = null) {
    const modal = $('#modalOverlay');
    const $form = $('#storyForm');
    const $imgPreview = $('#imgPreview');
    const $uploadIcon = $('#uploadIcon');
    const isVi = checkIsViMode();

    const noCatText = L.NoCategoriesSelected || 'Chưa chọn thể loại nào';

    // 1. Reset Form cơ bản & input ẩn
    $form[0].reset(); $('#storyId').val('0');
    $('input[name="CategoryIds"]').val('');
    $imgPreview.addClass('hidden').attr('src', ''); $uploadIcon.removeClass('hidden');

    // 2. Xóa sạch danh mục đã chọn
    if (typeof selectedCategoryIds !== 'undefined') {
        selectedCategoryIds.clear();
    }
    $('#selectedCategories').html(`<span class="comingsoon text-xs text-gray-400">${noCatText}</span>`);

    // 3. Reset dropdown Tác giả & Trạng thái
    $('#storyAuthorName').val('').trigger('change');
    $('#storyStatus').val('Completed').trigger('change');

    // 4. Reset bộ đệm bản dịch
    storyTranslations = {
        en: { title: '', desc: '' },
        vi: { title: '', desc: '' }
    };
    $('#primaryTitle, #primaryDesc, #refTitle, #refDesc, #targetTitle, #targetDesc').val('');
    $('#secondaryLangPanel').addClass('hidden');
    updateLanguageUIHeader();

    modal.removeClass('hidden').addClass('flex');

    // ================= CHẾ ĐỘ THÊM MỚI (ADD) =================
    if (mode === 'add') {
        $('#modalTitle').text(L.TitleAddStory || 'Thêm truyện mới');
        $('.lang-checkbox').prop('disabled', false).prop('checked', false).closest('.lang-chip').removeClass('active');
        setSelectedLanguages('Tiếng Anh, Tiếng Việt');
        if (typeof loadCategories === 'function') loadCategories();
    }
    // ================= CHẾ ĐỘ CẬP NHẬT (EDIT) =================
    else {
        $('#modalTitle').text(L.TitleEditStory || 'Cập nhật thông tin truyện');
        $('.lang-checkbox').prop('disabled', true);

        $.get('/Admin/tblStories/GetById/' + id, function (data) {
            if (!data) return;

            $('#storyId').val(data.storyId);
            $('#storyPublicationDate').val(data.publicationDate);
            $('#storyAuthorName').val(data.authorId).trigger('change');
            $('#storyStatus').val(data.status).trigger('change');

            // [SỬA LỖI 1]: HIỂN THỊ ẢNH BÌA NẾU CÓ
            if (data.img && data.img.trim() !== '') {
                const imgSrc = data.img.startsWith('/') ? data.img : '/' + data.img;
                $imgPreview.attr('src', imgSrc).removeClass('hidden'); $uploadIcon.addClass('hidden');
            } else {
                $imgPreview.addClass('hidden').attr('src', ''); $uploadIcon.removeClass('hidden');
            }

            // [SỬA LỖI 2]: CƠ CHẾ FALLBACK NỘI DUNG (Không để Tiếng Việt bị trắng toát)
            const titleEn = data.titleEn || '';
            const descEn = data.descEn || '';
            // Nếu chưa có bản dịch tiếng Việt, lấy luôn tiếng Anh làm giá trị ban đầu để người xem không bị trắng
            const titleVi = data.titleVi ? data.titleVi : titleEn;
            const descVi = data.descVi ? data.descVi : descEn;

            storyTranslations.en = { title: titleEn, desc: descEn };
            storyTranslations.vi = { title: titleVi, desc: descVi };

            if (isVi) {
                $('#primaryTitle').val(storyTranslations.vi.title);
                $('#primaryDesc').val(storyTranslations.vi.desc);
                $('#refTitle').val(storyTranslations.vi.title);
                $('#refDesc').val(storyTranslations.vi.desc);
                $('#targetTitle').val(storyTranslations.en.title);
                $('#targetDesc').val(storyTranslations.en.desc);
            } else {
                $('#primaryTitle').val(storyTranslations.en.title);
                $('#primaryDesc').val(storyTranslations.en.desc);
                $('#refTitle').val(storyTranslations.en.title);
                $('#refDesc').val(storyTranslations.en.desc);
                $('#targetTitle').val(storyTranslations.vi.title);
                $('#targetDesc').val(storyTranslations.vi.desc);
            }

            if (data.lang && data.lang.trim() !== "") {
                setSelectedLanguages(data.lang);
            } else {
                setSelectedLanguages('Tiếng Anh, Tiếng Việt');
            }

            // [SỬA LỖI 1]: HIỂN THỊ CHIP THỂ LOẠI NGAY LẬP TỨC
            const preview = document.getElementById('selectedCategories');
            if (preview) {
                preview.innerHTML = '';
                if (data.categories && data.categories.length > 0) {
                    let chipsHtml = '';
                    data.categories.forEach(c => {
                        if (typeof selectedCategoryIds !== 'undefined') {
                            selectedCategoryIds.add(String(c.categoryId));
                        }
                        chipsHtml += `
                            <span class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-semibold bg-indigo-50 text-indigo-700 rounded-lg border border-indigo-200 shadow-sm mr-1.5 mb-1">
                                <i class="fas fa-tag text-[10px] opacity-60"></i>
                                <span>${c.name}</span>
                                <input type="hidden" name="CategoryIds[]" value="${c.categoryId}">
                            </span>
                        `;
                    });
                    preview.innerHTML = chipsHtml;
                } else {
                    preview.innerHTML = `<span class="comingsoon text-xs text-gray-400">${noCatText}</span>`;
                }
            }

            if (typeof loadCategories === 'function') {
                loadCategories();
            }
        }).fail(function () {
            showToast('Không thể tải thông tin truyện từ máy chủ!', 'error');
        });
    }

    setTimeout(() => {
        $('#modalContent').addClass('translate-y-0 opacity-100');
    }, 10);
}
// Đóng modal Thêm / Sửa
function closeModal() {
    const content = $('#modalContent');
    content.removeClass('translate-y-0 opacity-100 scale-100')
        .addClass('translate-y-10 opacity-0 scale-95');

    setTimeout(() => {
        $('#modalOverlay').removeClass('flex').addClass('hidden');
    }, 300);
}

// Submit form Thêm / Cập nhật
// Submit form Thêm / Cập nhật kèm Debug
$('#storyForm').on('submit', function (e) {
    e.preventDefault();
    const isVi = checkIsViMode();
    const submitBtn = $(this).find('button[type="submit"]');
    const formData = new FormData(this);

    // Đồng bộ giá trị đang nhập từ ô chính
    if (isVi) {
        storyTranslations.vi.title = ($('#primaryTitle').val() || '').trim();
        storyTranslations.vi.desc = ($('#primaryDesc').val() || '').trim();
        storyTranslations.en.title = ($('#targetTitle').val() || storyTranslations.en.title || '').trim();
        storyTranslations.en.desc = ($('#targetDesc').val() || storyTranslations.en.desc || '').trim();
    } else {
        storyTranslations.en.title = ($('#primaryTitle').val() || '').trim();
        storyTranslations.en.desc = ($('#primaryDesc').val() || '').trim();
        storyTranslations.vi.title = ($('#targetTitle').val() || storyTranslations.vi.title || '').trim();
        storyTranslations.vi.desc = ($('#targetDesc').val() || storyTranslations.vi.desc || '').trim();
    }

    // Fallback: nếu một bên trống thì lấy bên còn lại bù vào
    if (!storyTranslations.en.title) storyTranslations.en.title = storyTranslations.vi.title;
    if (!storyTranslations.vi.title) storyTranslations.vi.title = storyTranslations.en.title;

    if (!storyTranslations.en.title && !storyTranslations.vi.title) {
        showToast('Vui lòng nhập tiêu đề cho truyện!', 'error');
        $('#primaryTitle').focus();
        return;
    }

    formData.set('TitleEn', storyTranslations.en.title);
    formData.set('DescEn', storyTranslations.en.desc);
    formData.set('TitleVi', storyTranslations.vi.title);
    formData.set('DescVi', storyTranslations.vi.desc);

    const langString = getSelectedLanguagesString();
    formData.set('Lang', langString);

    // Đẩy các ID thể loại đã chọn vào FormData
    if (typeof selectedCategoryIds !== 'undefined' && selectedCategoryIds.size > 0) {
        formData.delete('CategoryIds');
        selectedCategoryIds.forEach(id => {
            formData.append('CategoryIds', id);
        });
    }

    console.group('%c[DEBUG SUBMIT STORY FORM]', 'color: #0284c7; font-weight: bold;');
    console.log('URL Gửi đi:', '/Admin/tblStories/SaveStory');
    console.log('StoryId:', formData.get('StoryId'));
    console.log('TitleEn:', formData.get('TitleEn'));
    console.log('TitleVi:', formData.get('TitleVi'));
    console.log('CategoryIds:', formData.getAll('CategoryIds'));
    console.log('File ảnh:', formData.get('formFile'));
    console.groupEnd();

    submitBtn.prop('disabled', true).html(`<i class="fas fa-spinner animate-spin"></i> ${L.Processing || 'Đang lưu...'}`);

    $.ajax({
        url: '/Admin/tblStories/SaveStory',
        type: 'POST',
        data: formData,
        contentType: false,
        processData: false,
        success: function (res) {
            console.log('%c[KẾT QUẢ TỪ SERVER]:', 'color: #10b981; font-weight: bold;', res);
            if (res.success) {
                showToast(res.message, 'success');
                closeModal();
                loadStoryList(currentPage);
            } else {
                showToast('Lỗi: ' + res.message, 'error');
            }
        },
        error: function (xhr) {
            console.error('[LỖI AJAX SAVESTORY]:', xhr.status, xhr.statusText, xhr.responseText);
            showToast(`Lỗi kết nối máy chủ (${xhr.status}: ${xhr.statusText})`, 'error');
        },
        complete: function () {
            submitBtn.prop('disabled', false).html(L.SaveChanges || 'Lưu thay đổi');
        }
    });
});
// Xóa 1 truyện đơn lẻ
async function deleteStory(id) {
    const actionText = L.ActionCannotUndo || 'This action cannot be undone.';
    const confirmMsg = (L.ConfirmDeleteSingleStoryMsg || 'Are you sure to remove this story ?') + `<br><small class="text-red-400">${actionText}</small>`;
    const confirmTitle = L.ConfirmDeleteSingleStoryTitle || "Delete Story";

    const confirmed = await customConfirm(confirmMsg, confirmTitle);
    if (confirmed) {
        const $row =$(`button[onclick="deleteStory(${id})"]`).closest('tr');
        $row.addClass('opacity-50 pointer-events-none');

        $.ajax({
            url: '/Admin/tblStories/Delete',
            type: 'POST',
            data: { id: id },
            success: function (response) {
                if (response.success) {
                    $row.fadeOut(400, function () {$(this).remove();
                        showToast(response.message, 'success');
                    });
                } else {
                    showToast("Error: " + response.message + (response.inner || ''), 'error');
                    $row.removeClass('opacity-50 pointer-events-none');
                }
            },
            error: function () {
                showToast(L.ErrConnectDelete || "Cannot connect to the server to delete.", 'error');
                $row.removeClass('opacity-50 pointer-events-none');
            }
        });
    }
}

// Xuất file Excel
function exportExcel() {
    const search = $('#filterSearch').val();
    const category = $('#filterCategory').val();
    const status = $('#filterStatus').val();

    window.location.href = `/Admin/tblStories/ExportToExcel?search=${search}&status=${status}&categoryId=${category}`;
}

// Tải bảng danh sách truyện qua AJAX
// Tải bảng danh sách truyện qua AJAX
function loadStoryList(page = 1) {
    currentPage = page;
    const $body = $('#user-list-body');
    const statusVal = $('#filterStatus').val();
    const categoryVal = $('#filterCategory').val();
    const currentCulture = (typeof getCurrentCulture === 'function') ? getCurrentCulture().trim() : 'vi-VN';

    console.log("==================== DEBUG LOAD STORY LIST ====================");
    console.log("1. Cookie thô:", document.cookie);
    console.log("2. Thẻ <html lang>: ", $('html').attr('lang'));
    console.log("3. Current Culture tính được:", currentCulture);
    console.log("4. Có phải Tiếng Việt (checkIsViMode)?", checkIsViMode());

    const filters = {
        search: $('#filterSearch').val(),
        status: statusVal === 'all' ? '' : statusVal,
        categoryId: categoryVal === 'all' ? '' : categoryVal,
        culture: currentCulture, // Luôn gửi culture chuẩn
        page: currentPage,
        pageSize: pageSize
    };

    console.log("5. Params gửi lên API /Admin/tblStories/List:", filters);

    $.ajax({
        url: '/Admin/tblStories/List',
        type: 'GET',
        data: filters,
        success: function (data) {
            console.log("6. Dữ liệu Controller trả về:", data);
            if (data && data.stories && data.stories.length > 0) {
                console.log("7. Mẫu truyện đầu tiên nhận được:", {
                    StoryId: data.stories[0].storyId,
                    Title: data.stories[0].title,
                    Description: data.stories[0].description
                });
            }

            let html = '';
            if (!data.stories || data.stories.length === 0) {
                const notFound = L.NotFoundStories || 'Not found any results.';
                $body.html(`<tr><td colspan="9" class="text-center py-10 text-gray-500">${notFound}</td></tr>`);
                $('#pagination-container').html('');
                return;
            }

            data.stories.forEach(story => {
                const avatarHtml = story.img
                    ? `<img onclick="openImagePreview(this.src)" src="/${story.img}" class="w-11 h-11 rounded-2xl object-cover shadow-sm border border-gray-100 cursor-pointer">`
                    : `<div class="w-11 h-11 rounded-2xl bg-gradient-to-tr from-purple-500 to-indigo-600 flex items-center justify-center text-white font-bold shadow-sm">${story.title ? story.title.charAt(0).toUpperCase() : 'U'}</div>`;

                // XỬ LÝ SONG NGỮ STATUS CHO TỪNG TRUYỆN TẠI ĐÂY
                let statusClass = "comingsoon";
                let statusText = L.StatusComingSoon || "Coming soon";
                const rawStatus = (story.status || "").toLowerCase().trim();

                if (rawStatus === "completed") {
                    statusClass = "active";
                    statusText = L.StatusCompleted || "Completed";
                } else if (rawStatus === "posting") {
                    statusClass = "posting";
                    statusText = L.StatusPosting || "Posting";
                } else if (rawStatus.includes("coming")) {
                    statusClass = "comingsoon";
                    statusText = L.StatusComingSoon || "Coming soon";
                }

                const noCatText = L.NoCategoriesSelected || 'No category';
                const categoryHtml = story.categories && story.categories.length
                    ? story.categories.map(c => `<span class="px-2 py-1 bg-indigo-50 text-indigo-600 rounded-md text-[13px] font-semibold">${c}</span>`).join(" ")
                    : `<span class="text-sm text-gray-400">${noCatText}</span>`;

                const langArr = (story.lang || "Tiếng Anh, Tiếng Việt").split(',').map(l => l.trim());
                const langBadges = langArr.map(l => {
                    let flag = "🌍", short = l;
                    if (l.includes("Anh")) { flag = "🇺🇸"; short = "EN"; }
                    else if (l.includes("Việt")) { flag = "🇻🇳"; short = "VN"; }
                    else if (l.includes("Trung")) { flag = "🇨🇳"; short = "ZH"; }
                    else if (l.includes("Nhật")) { flag = "🇯🇵"; short = "JA"; }
                    else if (l.includes("Pháp")) { flag = "🇫🇷"; short = "FR"; }

                    return `<span class="inline-flex items-center gap-1 px-2 py-0.5 bg-teal-50 text-teal-700 border border-teal-200 rounded-lg text-xs font-bold shadow-sm" title="${l}"><span>${flag}</span> <span>${short}</span></span>`;
                }).join(" ");

                html += `
                <tr class="group hover:bg-indigo-50/30 transition-all">
                    <td class="px-6 py-5 text-center"><input type="checkbox" class="user-checkbox w-5 h-5 rounded-md border-gray-300" value="${story.storyId}"></td>
                    <td class="px-4 py-5"><div class="flex items-center gap-4">${avatarHtml}<div><div class="font-bold text-gray-700">${story.title}</div><div class="text-xs text-gray-400">${story.authorName || ''}</div></div></div></td>
                    <td class="px-6 py-5"><div class="flex flex-wrap gap-1 max-w-[180px]">${langBadges}</div></td>
                    <td class="px-6 py-5"><span class="px-3 py-1.5 bg-slate-100 text-gray-600 rounded-lg text-xs font-semibold">${story.publicationDate ?? ""}</span></td>
                    <td class="px-6 py-5 max-w-xs truncate text-gray-600 text-sm">${story.description ?? ""}</td>
                    <td class="px-6 py-5">
                        <div class="text-sm text-gray-600 leading-6 grid grid-cols-2 gap-x-4 gap-y-2">
                            <div class="flex items-center gap-2"><i class="fa-solid text-deny fa-heart w-4 text-center"></i> <span>${story.likes} ${L.MetricLoves || 'loves'}</span></div>
                            <div class="flex items-center gap-2"><i class="fa-solid text-wait fa-star w-4 text-center"></i> <span>${story.rate} ${L.MetricRates || 'rates'}</span></div>
                            <div class="flex items-center gap-2"><i class="fa-solid text-cus fa-comment-dollar w-4 text-center"></i> <span>${story.countRate} ${L.MetricRated || 'rated'}</span></div>
                            <div class="flex items-center gap-2"><i class="fa-solid text-accepted fa-users w-4 text-center"></i> <span>${story.countFolower} ${L.MetricFollowers || 'followers'}</span></div>
                        </div>
                    </td>
                    <td class="px-6 py-5 max-w-xs truncate"><div class="flex flex-wrap gap-2">${categoryHtml}</div></td>
                    <td class="px-6 py-5 text-center">
                        <span class="font-bold px-3 py-1.5 ${statusClass} rounded-xl text-[10px] uppercase tracking-wider">
                            ${statusText}
                        </span>
                    </td>                   
                    <td class="px-8 py-5 text-right">
                        <div class="flex justify-end gap-3">
                            <button onclick="openModal('edit', ${story.storyId})" class="w-7 aspect-square p-0 flex items-center justify-center rounded-lg btn-grad bg-blue-50 hover:bg-blue-600 hover:text-white transition-all duration-200" title="${L.TitleEditAccount || 'Edit'}"><i class="fas fa-pencil-alt text-[11px]"></i></button>
                            <button onclick="deleteStory(${story.storyId})" class="w-7 aspect-square p-0 flex items-center justify-center rounded-lg btn-grad-cancel bg-red-50 hover:bg-red-600 hover:text-white transition-all duration-200" title="${L.TitleDeleteAccount || 'Delete'}"><i class="fas fa-trash-alt text-[11px]"></i></button>
                        </div>
                    </td>
                </tr>`;
            });

            $body.html(html);
            renderPagination(data.currentPage, data.totalPages);
            updateDeleteButton();
        },
        error: function (xhr) {
            console.error("Lỗi khi gọi API List:", xhr);
            const errLoad = L.ErrLoadStories || 'Error: Cannot load data.';
            $body.html(`<tr><td colspan="9" class="text-center py-10 text-red-500">${errLoad}</td></tr>`);
        }
    });
}

// Xử lý Import Excel
function executeImport() {
    const fileInput = document.getElementById('excelFile');
    if (fileInput.files.length === 0) {
        showToast(L.ChooseExcelFile || 'Please choose file Excel!', 'error');
        return;
    }

    const formData = new FormData();
    formData.append('file', fileInput.files[0]);

    const $btn =$('#btnDoImport');
    $btn.prop('disabled', true).html(`<i class="fas fa-spinner fa-spin"></i> ${L.Processing || 'Loading...'}`);

    $.ajax({
        url: '/Admin/tblStories/ImportFromExcel',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            if (res.success) {
                showToast(res.message, 'success');
                closeImportModal();
                loadStoryList(1);
            } else {
                let errorDetails = `${res.message || 'Import failed'}`;
                if (res.error) errorDetails += `<br><small class="opacity-90">Error: ${res.error}</small>`;
                if (res.stack) errorDetails += `<br><small class="opacity-75">Detail: ${res.stack}</small>`;
                showToast(errorDetails, 'error');
            }
        },
        error: function (xhr) {
            let errorMsg = L.ErrImport || 'Error during import file!';
            if (xhr.responseJSON) {
                const res = xhr.responseJSON;
                errorMsg += `<br><small class="opacity-90">Error: ${res.error || res.message}</small>`;
            }
            showToast(errorMsg, 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).text(L.ConfirmImport || 'Confirm Import');
        }
    });
}

// Render các nút phân trang
function renderPagination(currentPage, totalPages) {
    const container = $('#pagination-container');
    container.empty();

    if (totalPages <= 1) return;

    const maxVisible = 2;
    let html = `<div class="flex items-center gap-1">`;

    // First
    html += `
    <button onclick="loadStoryList(1)" class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40' : ''}" ${currentPage === 1 ? 'disabled' : ''}>⏮</button>`;

    // Prev
    html += `
    <button onclick="loadStoryList(${currentPage - 1})" class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40' : ''}" ${currentPage === 1 ? 'disabled' : ''}>◀</button>`;

    if (currentPage > maxVisible + 1) {
        html += pageBtn(1, currentPage);
        html += `<span class="px-2">…</span>`;
    }

    for (let i = Math.max(1, currentPage - maxVisible); i <= Math.min(totalPages, currentPage + maxVisible); i++) {
        html += pageBtn(i, currentPage);
    }

    if (currentPage < totalPages - maxVisible) {
        html += `<span class="px-2">…</span>`;
        html += pageBtn(totalPages, currentPage);
    }

    // Next
    html += `
    <button onclick="loadStoryList(${currentPage + 1})" class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40' : ''}" ${currentPage === totalPages ? 'disabled' : ''}>▶</button>`;

    // Last
    html += `
    <button onclick="loadStoryList(${totalPages})" class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40' : ''}" ${currentPage === totalPages ? 'disabled' : ''}>⏭</button>`;

    // Go to page
    html += `
    <div class="flex items-center gap-1 ml-3">
        <span class="text-sm">Go:</span>
        <input type="number" min="1" max="${totalPages}" value="${currentPage}" class="w-16 px-2 py-1 border rounded text-center" onkeydown="if(event.key==='Enter') gotoPage(this, ${totalPages})">
    </div>`;

    html += `</div>`;
    container.html(html);
}

function pageBtn(page, current) {
    const active = page === current;
    return `
    <button onclick="loadStoryList(${page})" class="px-3 py-1 border rounded ${active ? 'btn-grad text-white font-bold' : 'hover:bg-gray-100'}">
        ${page}
    </button>`;
}

function gotoPage(input, totalPages) {
    let page = parseInt(input.value);
    if (isNaN(page)) return;
    if (page < 1) page = 1;
    if (page > totalPages) page = totalPages;
    loadStoryList(page);
}

// Khởi tạo các sự kiện lọc, checkbox
$(document).ready(function () {
    const savedPage = localStorage.getItem('storyPage');
    currentPage = savedPage ? parseInt(savedPage) : 1;
    loadStoryList(currentPage);

    $('#filterSearch').on('keyup', function () { loadStoryList(1); });
    $('#filterCategory, #filterStatus').on('change', function () { loadStoryList(1); });

    $('#selectAll').on('change', function () {
        const isChecked = this.checked;
        $('.user-checkbox').each(function () {$(this).prop('checked', isChecked).closest('tr').toggleClass('bg-indigo-50/50', isChecked);
            updateDeleteButton();
        });
    });

    $(document).on('change', '.user-checkbox', function () {
        const total = $('.user-checkbox').length;
        const checked = $('.user-checkbox:checked').length;
        $(this).closest('tr').toggleClass('bg-indigo-50/50', this.checked);$('#selectAll').prop('checked', total > 0 && total === checked);
        updateDeleteButton();
    });

    $(document).on('click', '#btnDeleteSelected', async function () {
        const ids = $('.user-checkbox:checked').map(function () { return parseInt(this.value); }).get();
        if (ids.length === 0) {
            showToast(L.SelectAtLeastOneStory || 'Please select at least one story!', 'error');
            return;
        }

        const actionText = L.ActionCannotUndo || 'This action cannot be undone.';
        const multiMsg = (L.ConfirmDeleteMultiStoriesMsg || 'Are you sure you want to delete <strong>{0}</strong> stories ?').replace('{0}', ids.length) + `<br><small class="text-red-400">${actionText}</small>`;
        const multiTitle = L.ConfirmDeleteMultiStoriesTitle || "Delete Multiple";

        const confirmed = await customConfirm(multiMsg, multiTitle);
        if (!confirmed) return;

        $.ajax({
            url: '/Admin/tblStories/DeleteMultiple',
            type: 'POST',
            traditional: true,
            data: { ids: ids },
            success: function (res) {
                if (res.blocked && res.blocked.length > 0) {
                    const blockedTemplate = L.BlockedDeleteStory || 'Cannot delete this ID: {0}\nBecause it was used to link foreign keys.';
                    showToast(blockedTemplate.replace('{0}', res.blocked.join(', ')), 'error');
                    updateDeleteButton();
                }
                if (res.deleted && res.deleted.length > 0) {
                    const deletedTemplate = L.DeletedStoriesSuccess || 'Deleted {0} story.';
                    showToast(deletedTemplate.replace('{0}', res.deleted.length), 'success');
                }
                loadStoryList(currentPage);
                $('#selectAll').prop('checked', false);
            }
        });
    });
});