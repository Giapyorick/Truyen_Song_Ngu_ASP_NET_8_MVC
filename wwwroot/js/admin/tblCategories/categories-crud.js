/* categories-crud.js - Hỗ trợ song ngữ & Dịch AI */

const L = window.ADMIN_LANG || {};

let savedCategoryPage = localStorage.getItem('categoryPage');
let currentPage = savedCategoryPage ? parseInt(savedCategoryPage) : 1;
const pageSize = 5;

// ==================== CÁC HÀM XÁC ĐỊNH NGÔN NGỮ HIỆN TẠI ====================
function getCurrentCulture() {
    const cookies = document.cookie.split(';');
    for (let c of cookies) {
        c = c.trim();
        if (c.indexOf('.AspNetCore.Culture=') === 0) {
            const val = decodeURIComponent(c.substring('.AspNetCore.Culture='.length));
            const match = val.match(/uic=([^|;]+)/) || val.match(/c=([^|;]+)/);
            if (match && match[1]) return match[1];
        }
    }
    const htmlLang = $('html').attr('lang');
    if (htmlLang) return htmlLang;

    return 'en-US';
}

function checkIsViMode() {
    return getCurrentCulture().toLowerCase().indexOf('vi') === 0;
}

// Bộ đệm lưu trữ bản dịch trong Modal
let categoryTranslations = {
    en: { name: '', desc: '' },
    vi: { name: '', desc: '' }
};

// 1. Cập nhật nhãn và giao diện Modal theo ngôn ngữ đang chọn
function updateLanguageUIHeader() {
    const isVi = checkIsViMode();

    if (isVi) {
        $('#currentLangFlag').text('🇻🇳');
        $('#currentLangTitleDesc').html('Bản nhập chính: <strong>Tiếng Việt</strong>');
        $('#btnToggleTranslateLabel').html('Thêm / Sửa Tiếng Anh 🇺🇸');
        $('#lblTitlePrimary').html('Tên thể loại (Tiếng Việt) <span class="text-red-500">*</span>');
        $('#lblDescPrimary').text('Mô tả thể loại (Tiếng Việt)');
        $('#primaryName').attr('placeholder', 'Nhập tên thể loại bằng tiếng Việt...');
        $('#primaryDesc').attr('placeholder', 'Nhập mô tả bằng tiếng Việt...');

        $('#secondaryPanelHeader').html('<i class="fas fa-layer-group text-teal-600"></i> Bản dịch Tiếng Anh (English Translation)');
        $('#secondaryPanelNotice').text('* Dịch sang Tiếng Anh để phục vụ độc giả quốc tế.');
        $('#btnAiTranslateLabel').text('Dịch sang Tiếng Anh bằng AI');
        $('#lblRefBoxTitle').html('<i class="fas fa-eye text-gray-400"></i> Bản gốc Tiếng Việt (Tham khảo đối chiếu)');
        $('#lblRefName').text('Tên Tiếng Việt gốc');
        $('#lblRefDesc').text('Mô tả Tiếng Việt gốc');

        $('#lblTargetHeader').html('<i class="fas fa-pen text-teal-500"></i> Nhập bản dịch Tiếng Anh 🇺🇸');
        $('#lblTargetName').text('Tên Tiếng Anh (English)');
        $('#lblTargetDesc').text('Mô tả Tiếng Anh (English)');
        $('#targetName').attr('placeholder', 'Enter category name in English...');
        $('#targetDesc').attr('placeholder', 'Enter description in English...');
    } else {
        $('#currentLangFlag').text('🇺🇸');
        $('#currentLangTitleDesc').html('Primary Version: <strong>English</strong>');
        $('#btnToggleTranslateLabel').html('Add / Edit Vietnamese 🇻🇳');
        $('#lblTitlePrimary').html('Category Name (English) <span class="text-red-500">*</span>');
        $('#lblDescPrimary').text('Category Description (English)');
        $('#primaryName').attr('placeholder', 'Enter category name in English...');
        $('#primaryDesc').attr('placeholder', 'Enter category description in English...');

        $('#secondaryPanelHeader').html('<i class="fas fa-layer-group text-teal-600"></i> Vietnamese Translation (Bản dịch tiếng Việt)');
        $('#secondaryPanelNotice').text('* Translate into Vietnamese for local readers.');
        $('#btnAiTranslateLabel').text('Auto-translate to Vietnamese with AI');
        $('#lblRefBoxTitle').html('<i class="fas fa-eye text-gray-400"></i> Original English (Reference)');
        $('#lblRefName').text('Original English Name');
        $('#lblRefDesc').text('Original English Description');

        $('#lblTargetHeader').html('<i class="fas fa-pen text-teal-500"></i> Vietnamese Input (Tiếng Việt) 🇻🇳');
        $('#lblTargetName').text('Vietnamese Name (Tên TV)');
        $('#lblTargetDesc').text('Vietnamese Description (Mô tả TV)');
        $('#targetName').attr('placeholder', 'Nhập tên thể loại tiếng Việt...');
        $('#targetDesc').attr('placeholder', 'Nhập mô tả tiếng Việt...');
    }
}

// 2. Mở/đóng panel bản dịch song song
function toggleTranslationPanel() {
    const $panel =$('#secondaryLangPanel');
    const isVi = checkIsViMode();

    if (isVi) {
        categoryTranslations.vi.name = ($('#primaryName').val() || '').trim();
        categoryTranslations.vi.desc = ($('#primaryDesc').val() || '').trim();

        $('#refName').val(categoryTranslations.vi.name);
        $('#refDesc').val(categoryTranslations.vi.desc);
        $('#targetName').val(categoryTranslations.en.name);
        $('#targetDesc').val(categoryTranslations.en.desc);
    } else {
        categoryTranslations.en.name = ($('#primaryName').val() || '').trim();
        categoryTranslations.en.desc = ($('#primaryDesc').val() || '').trim();

        $('#refName').val(categoryTranslations.en.name);
        $('#refDesc').val(categoryTranslations.en.desc);
        $('#targetName').val(categoryTranslations.vi.name);
        $('#targetDesc').val(categoryTranslations.vi.desc);
    }

    $panel.toggleClass('hidden');
}
$(document).on('input', '#targetName', function () {
    const isVi = checkIsViMode();
    if (isVi) categoryTranslations.en.name = $(this).val();
    else categoryTranslations.vi.name = $(this).val();
});
$(document).on('input', '#targetDesc', function () {
    const isVi = checkIsViMode();
    if (isVi) categoryTranslations.en.desc = $(this).val();
    else categoryTranslations.vi.desc = $(this).val();
});

// 3. Dịch bằng AI
function translateCurrentViaAi() {
    const isVi = checkIsViMode();
    const sourceName = ($('#primaryName').val() || $('#refName').val() || '').trim();
    const sourceDesc = ($('#primaryDesc').val() || $('#refDesc').val() || '').trim();

    const fromLang = isVi ? 'Vietnamese' : 'English';
    const toLang = isVi ? 'English' : 'Vietnamese';

    if (!sourceName && !sourceDesc) {
        showToast(L.ReqSourceBeforeTranslate || 'Please enter category name or description before translating!', 'error');
        return;
    }

    const $btn =$('#btnAiTranslate');
    const translatingText = L.BtnAiTranslating || 'Translating...';
    $btn.prop('disabled', true).html(`<i class="fas fa-spinner fa-spin mr-1"></i> ${translatingText}`);
    showToast(L.AiTranslatingStatus || 'AI translation in progress...', 'info');

    let hasUpdated = false;
    let requests = [];

    if (sourceName) {
        const nameReq = $.post('/Admin/tblCategories/TranslateWithAi', { text: sourceName, fromLang, toLang }).done(function (res) {
            if (res.success && res.result) {
                $('#targetName').val(res.result);
                if (isVi) categoryTranslations.en.name = res.result;
                else categoryTranslations.vi.name = res.result;
                hasUpdated = true;
            }
        });
        requests.push(nameReq);
    }

    if (sourceDesc) {
        const descReq = $.post('/Admin/tblCategories/TranslateWithAi', { text: sourceDesc, fromLang, toLang }).done(function (res) {
            if (res.success && res.result) {
                $('#targetDesc').val(res.result);
                if (isVi) categoryTranslations.en.desc = res.result;
                else categoryTranslations.vi.desc = res.result;
                hasUpdated = true;
            }
        });
        requests.push(descReq);
    }

    $.when.apply($, requests).always(function () {
        const defaultBtnText = L.BtnAiTranslateDefault || 'Auto-translate with AI';
        $btn.prop('disabled', false).html(`<i class="fas fa-wand-magic-sparkles text-amber-200 mr-1"></i> ${defaultBtnText}`);
        if (hasUpdated) {
            showToast(L.AiTranslateCompleted || 'AI translation completed successfully!', 'success');
        }
    });
}
// Tải danh sách thể loại từ server qua AJAX
function loadCategoryList(page = null) {
    if (page !== null && page !== undefined) {
        currentPage = parseInt(page);
    }
    localStorage.setItem('categoryPage', currentPage);

    const $body = $('#user-list-body');
    const statusVal = $('#filterStatus').val();
    const currentCulture = (typeof getCurrentCulture === 'function') ? getCurrentCulture().trim() : 'en-US';

    console.log("==================== DEBUG CATEGORIES LIST ====================");
    console.log("1. Cookie thô:", document.cookie);
    console.log("2. Thẻ <html lang>:", $('html').attr('lang'));
    console.log("3. Current Culture tính được:", currentCulture);
    console.log("4. Có phải Tiếng Việt (checkIsViMode)?", checkIsViMode());

    const filters = {
        search: $('#filterSearch').val(),
        status: statusVal === 'all' ? '' : statusVal,
        culture: currentCulture,
        page: currentPage,
        pageSize: pageSize
    };

    console.log("5. Params gửi lên API /Admin/tblCategories/List:", filters);

    $.ajax({
        url: '/Admin/tblCategories/List',
        type: 'GET',
        data: filters,
        success: function (data) {
            console.log("6. Dữ liệu Controller trả về:", data);

            if (data && data.categories && data.categories.length > 0) {
                console.log("7. Thể loại đầu tiên nhận được:", {
                    CategoryId: data.categories[0].categoryId,
                    Name: data.categories[0].name,
                    Description: data.categories[0].description
                });
            }

            let html = '';
            if (!data.categories || data.categories.length === 0) {
                if (currentPage > 1) {
                    loadCategoryList(currentPage - 1);
                    return;
                }
                const notFound = L.NotFoundCategories || 'Not found any results.';
                $body.html(`<tr><td colspan="5" class="text-center py-10 text-gray-500">${notFound}</td></tr>`);
                $('#pagination-container').html('');
                return;
            }

            data.categories.forEach(category => {
                const statusActive = category.status === "Active";
                const statusClass = statusActive ? 'active' : 'inactive';
                const statusText = statusActive ? (L.StatusActive || 'Active') : (L.StatusInactive || 'Inactive');

                html += `
                <tr class="group hover:bg-indigo-50/30 transition-all">
                    <td class="px-6 py-5 text-center">
                        <input type="checkbox" class="user-checkbox w-5 h-5 rounded-md border-gray-300" value="${category.categoryId}">
                    </td>
                    <td class="px-4 py-5 font-bold text-gray-700">
                        ${category.name}
                    </td>
                    <td class="px-6 py-5 text-gray-600 text-sm max-w-xs md:max-w-sm">
                        <div class="line-clamp-2 truncate-multiline" title="${(category.description || '').replace(/"/g, '&quot;')}">
                            ${category.description || '<span class="text-gray-400 italic">No description</span>'}
                        </div>
                    </td>
                    <td class="px-6 py-5 text-center">
                        <span class="font-bold px-3 py-1.5 ${statusClass} rounded-xl text-[10px] uppercase tracking-wider">
                            ${statusText}
                        </span>
                    </td>
                    <td class="px-8 py-5 text-right">
                        <div class="flex justify-end gap-3">
                            <button onclick="openModal('edit', ${category.categoryId})"
                                class="w-7 aspect-square p-0 flex items-center justify-center rounded-lg btn-grad bg-blue-50 hover:bg-blue-600 hover:text-white transition-all duration-200" title="${L.TitleEditAccount || 'Edit'}">
                                <i class="fas fa-pencil-alt text-[11px]"></i>
                            </button>
                            <button onclick="deleteCategory(${category.categoryId})"
                                class="w-7 aspect-square p-0 flex items-center justify-center rounded-lg btn-grad-cancel bg-red-50 hover:bg-red-600 hover:text-white transition-all duration-200" title="${L.TitleDeleteAccount || 'Delete'}">
                                <i class="fas fa-trash-alt text-[11px]"></i>
                            </button>
                        </div>
                    </td>
                </tr>`;
            });

            $body.html(html);
            renderPagination(currentPage, data.totalPages);
            updateDeleteButton();
        },
        error: function (xhr) {
            console.error("Lỗi khi gọi API List:", xhr);
            const errLoad = L.ErrLoadCategories || 'Error loading data.';
            $body.html(`<tr><td colspan="5" class="text-center py-10 text-red-500">${errLoad}</td></tr>`);
        }
    });

}
async function deleteCategory(id) {
    const actionText = L.ActionCannotUndo || 'This action cannot be undone.';
    const confirmMsg = (L.ConfirmDeleteSingleCategoryMsg || 'Are you sure to remove this category?') + `<br><small class="text-red-400">${actionText}</small>`;
    const confirmTitle = L.ConfirmDeleteSingleCategoryTitle || "Delete Category";

    const confirmed = await customConfirm(confirmMsg, confirmTitle);
    if (confirmed) {
        const $row = $(`button[onclick="deleteCategory(${id})"]`).closest('tr');
        $row.addClass('opacity-50 pointer-events-none');

        $.ajax({
            url: '/Admin/tblCategories/Delete',
            type: 'POST',
            data: { id: id },
            success: function (response) {
                if (response.success) {
                    showToast(response.message, 'success');

                    // Nếu dòng vừa xóa là dòng duy nhất trên trang hiện tại và không phải trang 1
                    const remainingRows = $('#user-list-body tr').length - 1;
                    if (remainingRows <= 0 && currentPage > 1) {
                        currentPage--;
                    }

                    // Tải lại đúng trang hiện tại
                    loadCategoryList(currentPage);
                } else {
                    showToast("Error: " + response.message, 'error');
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
    const status = $('#filterStatus').val();
    window.location.href = `/Admin/tblCategories/ExportToExcel?search=${search}&status=${status}`;
}

// Thực thi Import file Excel
function executeImport() {
    const fileInput = document.getElementById('excelFile');
    if (fileInput.files.length === 0) {
        showToast(L.ChooseExcelFile || 'Please choose an Excel file!', 'error');
        return;
    }

    const formData = new FormData();
    formData.append('file', fileInput.files[0]);

    const $btn = $('#btnDoImport');
    $btn.prop('disabled', true).html(`<i class="fas fa-spinner fa-spin"></i> ${L.Processing || 'Processing...'}`);

    $.ajax({
        url: '/Admin/tblCategories/ImportExcel',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            if (res.success) {
                showToast(res.message, 'success');
                closeImportModal();
                loadCategoryList(1);
            } else {
                showToast(res.message, 'error');
            }
        },
        error: function () {
            showToast(L.ErrImport || 'Error during importing file!', 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).text(L.ConfirmImport || 'Confirm Import');
        }
    });
}

$('#userForm').on('submit', function (e) {
    e.preventDefault();
    const isVi = checkIsViMode();
    const submitBtn = $(this).find('button[type="submit"]');
    const formData = new FormData(this);

    if (isVi) {
        categoryTranslations.vi.name = ($('#primaryName').val() || '').trim();
        categoryTranslations.vi.desc = ($('#primaryDesc').val() || '').trim();
        categoryTranslations.en.name = ($('#targetName').val() || categoryTranslations.en.name || '').trim();
        categoryTranslations.en.desc = ($('#targetDesc').val() || categoryTranslations.en.desc || '').trim();
    } else {
        categoryTranslations.en.name = ($('#primaryName').val() || '').trim();
        categoryTranslations.en.desc = ($('#primaryDesc').val() || '').trim();
        categoryTranslations.vi.name = ($('#targetName').val() || categoryTranslations.vi.name || '').trim();
        categoryTranslations.vi.desc = ($('#targetDesc').val() || categoryTranslations.vi.desc || '').trim();
    }

    if (!categoryTranslations.en.name && !categoryTranslations.vi.name) {
        showToast(L.ReqCategoryName || 'Please enter category name!', 'error');
        $('#primaryName').focus();
        return;
    }

    formData.set('NameEn', categoryTranslations.en.name);
    formData.set('DescEn', categoryTranslations.en.desc);
    formData.set('NameVi', categoryTranslations.vi.name);
    formData.set('DescVi', categoryTranslations.vi.desc);

    submitBtn.prop('disabled', true).html(`<i class="fas fa-spinner animate-spin"></i> ${L.Processing || 'Processing...'}`);

    $.ajax({
        url: '/Admin/tblCategories/SaveCategory',
        type: 'POST',
        data: formData,
        contentType: false,
        processData: false,
        success: function (res) {
            if (res.success) {
                showToast(res.message, 'success');
                closeModal();
                loadCategoryList(currentPage);
            } else {
                showToast('Error: ' + res.message, 'error');
            }
        },
        error: function () {
            showToast(L.ErrConnectServer || 'Server connection error!', 'error');
        },
        complete: function () {
            submitBtn.prop('disabled', false).html(L.SaveChanges || 'Save Changes');
        }
    });
});

// Render thanh phân trang
function renderPagination(currentPage, totalPages) {
    const container = $('#pagination-container');
    container.empty();

    if (totalPages <= 1) return;

    const maxVisible = 2;
    let html = `<div class="flex items-center gap-1">`;

    html += `
    <button onclick="loadCategoryList(1)"
        class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40' : ''}"
        ${currentPage === 1 ? 'disabled' : ''}>
        ⏮
    </button>`;

    html += `
    <button onclick="loadCategoryList(${currentPage - 1})"
        class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40' : ''}"
        ${currentPage === 1 ? 'disabled' : ''}>
        ◀
    </button>`;

    if (currentPage > maxVisible + 1) {
        html += pageBtn(1, currentPage);
        html += `<span class="px-2">…</span>`;
    }

    for (let i = Math.max(1, currentPage - maxVisible);
        i <= Math.min(totalPages, currentPage + maxVisible);
        i++) {
        html += pageBtn(i, currentPage);
    }

    if (currentPage < totalPages - maxVisible) {
        html += `<span class="px-2">…</span>`;
        html += pageBtn(totalPages, currentPage);
    }

    html += `
    <button onclick="loadCategoryList(${currentPage + 1})"
        class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40' : ''}"
        ${currentPage === totalPages ? 'disabled' : ''}>
        ▶
    </button>`;

    html += `
    <button onclick="loadCategoryList(${totalPages})"
        class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40' : ''}"
        ${currentPage === totalPages ? 'disabled' : ''}>
        ⏭
    </button>`;

    html += `
    <div class="flex items-center gap-1 ml-3">
        <span class="text-sm">Go:</span>
        <input type="number"
            min="1"
            max="${totalPages}"
            value="${currentPage}"
            class="w-16 px-2 py-1 border rounded text-center"
            onkeydown="if(event.key==='Enter') gotoPage(this, ${totalPages})">
    </div>`;

    html += `</div>`;
    container.html(html);
}

function pageBtn(page, current) {
    const active = page === current;
    return `
    <button onclick="loadCategoryList(${page})"
        class="px-3 py-1 border rounded
        ${active ? 'btn-grad text-white font-bold' : 'hover:bg-gray-100'}">
        ${page}
    </button>`;
}

function gotoPage(input, totalPages) {
    let page = parseInt(input.value);
    if (isNaN(page)) return;

    if (page < 1) page = 1;
    if (page > totalPages) page = totalPages;

    loadCategoryList(page);
}

// Quản lý sự kiện Ready, Tìm kiếm và Xóa nhiều phần tử
$(document).ready(function () {
    loadCategoryList();

    $('#filterSearch').on('keyup', function () {
        loadCategoryList(1);
    });

    $('#filterStatus').on('change', function () {
        loadCategoryList(1);
    });

    $('#selectAll').on('change', function () {
        const isChecked = this.checked;

        $('.user-checkbox').each(function () {
            $(this).prop('checked', isChecked)
            .closest('tr')
            .toggleClass('bg-indigo-50/50', isChecked);
            updateDeleteButton();
        });
    });

    $(document).on('change', '.user-checkbox', function () {
        const total = $('.user-checkbox').length;
        const checked = $('.user-checkbox:checked').length;

        $(this).closest('tr').toggleClass('bg-indigo-50/50', this.checked); $('#selectAll').prop('checked', total > 0 && total === checked);
        updateDeleteButton();
    });

    // Xóa nhiều thể loại đã chọn
    $(document).on('click', '#btnDeleteSelected', async function () {
        const ids = $('.user-checkbox:checked')
            .map(function () {
                return parseInt(this.value);
            })
            .get();

        if (ids.length === 0) {
            showToast(L.SelectAtLeastOneCategory || 'Please select at least one category!', 'error');
            return;
        }

        const actionText = L.ActionCannotUndo || 'This action cannot be undone.';
        const multiMsg = (L.ConfirmDeleteMultiCategoriesMsg || 'Are you sure you want to delete <strong>{0}</strong> categories?')
            .replace('{0}', ids.length) + `<br><small class="text-red-400">${actionText}</small>`;
        const multiTitle = L.ConfirmDeleteMultiCategoriesTitle || "Delete Multiple";

        const confirmed = await customConfirm(multiMsg, multiTitle);
        if (!confirmed) return;

        $.ajax({
            url: '/Admin/tblCategories/DeleteMultiple',
            type: 'POST',
            traditional: true,
            data: { ids: ids },
            success: function (res) {
                if (res.blocked && res.blocked.length > 0) {
                    const blockedTemplate = L.BlockedDeleteCategory || 'Cannot delete ID(s): <strong>{0}</strong>.<br>Because they are currently linked to existing foreign keys.';
                    showToast(blockedTemplate.replace('{0}', res.blocked.join(', ')), 'error');
                }
                if (res.deleted && res.deleted.length > 0) {
                    const deletedTemplate = L.DeletedCategoriesSuccess || 'Deleted {0} category(ies) successfully.';
                    showToast(deletedTemplate.replace('{0}', res.deleted.length), 'success');
                }

                const totalOnPage = $('.user-checkbox').length;
                if (ids.length >= totalOnPage && currentPage > 1) {
                    currentPage--;
                }

                loadCategoryList(currentPage);
                $('#selectAll').prop('checked', false);
            }
        });
    });
});