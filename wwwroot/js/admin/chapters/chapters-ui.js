/**
 * ============================================================================
 *  FILE      : wwwroot/js/admin/chapters/chapters-ui.js
 *  CHỨC NĂNG : Quản lý Chương – giao diện: Flatpickr, Select2, dropdown truyện, xem trước ảnh, modal Import
 *  DÙNG TẠI  : Areas/Admin/Views/tblChapters/Chapters.cshtml
 *  PHỤ THUỘC : jQuery, Select2, Flatpickr, window.ADMIN_LANG
 *  THỨ TỰ    : Nạp TRƯỚC chapters-crud.js
 * ----------------------------------------------------------------------------
 *  MỤC LỤC
 *    1. BIẾN NGÔN NGỮ GIAO DIỆN
 *    2. KHỞI TẠO FLATPICKR / SELECT2
 *    3. DROPDOWN DANH SÁCH TRUYỆN (modal & bộ lọc)
 *    4. ẢNH – xem trước & phóng to
 *    5. MODAL IMPORT EXCEL
 *    6. NÚT XÓA NHIỀU – cập nhật số lượng đã chọn
 * ============================================================================
 */

// ============================================================================
// 1. BIẾN NGÔN NGỮ GIAO DIỆN
// ============================================================================
const UI_LANG = window.ADMIN_LANG || {};

// ============================================================================
// 2. KHỞI TẠO FLATPICKR / SELECT2
// ============================================================================
$(document).ready(function () {
    $(".datepicker").flatpickr({
        dateFormat: "Y-m-d",
        allowInput: true
    });

    $('.select2-custom').not('#filterStory').each(function () {
        $(this).select2({
            width: '100%',
            minimumResultsForSearch: Infinity,
            templateResult: formatState,
            templateSelection: formatState
        });
    });

    loadStoriesDropdown();
    loadStories();
});

// Hàm định dạng hiển thị dot màu và chữ gradient cho Select2
function formatState(state) {
    if (!state.id) { return state.text; }

    const gradient = $(state.element).data('color') || 'linear-gradient(135deg, #667eea, #764ba2)';

    return $(
        '<span style="display: flex; align-items: center;">' +
        '<span style="display:inline-block; width:10px; height:10px; border-radius:50%; background:' + gradient + '; margin-right:10px; flex-shrink: 0;"></span>' +
        '<span style="background:' + gradient + '; -webkit-background-clip: text; -webkit-text-fill-color: transparent; font-weight:700;">' + state.text + '</span>' +
        '</span>'
    );
}

// ============================================================================
// 3. DROPDOWN DANH SÁCH TRUYỆN (modal & bộ lọc)
// ============================================================================
// Nạp danh sách truyện vào dropdown (hỗ trợ Select2 chuẩn trong modal & bộ lọc ngoài)
function loadStoriesDropdown(selectedStoryId = null, filterSelector = null) {
    const currentCulture = (typeof getCurrentCulture === 'function') ? getCurrentCulture().trim() : 'vi-VN';

    fetch(`/Admin/tblChapters/GetStoriesForSelect?culture=${currentCulture}`)
        .then(res => res.json())
        .then(data => {
            if (filterSelector) {
                // 1. Dropdown lọc ngoài thanh công cụ (#filterStory)
                const $filter = $(filterSelector);
                if ($filter.hasClass("select2-hidden-accessible")) {
                    $filter.select2('destroy');
                }
                $filter.empty(); $filter.append(`<option data-color="linear-gradient(to right, #50C9C3 0%, #96DEDA 51%, #50C9C3 100%)" value="all">${L.FilterAllStories || 'All Stories'}</option>`);

                data.forEach(s => {
                    $filter.append(`<option data-color="linear-gradient(135deg, #667eea, #764ba2)" value="${s.id}">${s.name}</option>`);
                });

                $filter.select2({
                    width: '100%',
                    minimumResultsForSearch: Infinity,
                    templateResult: formatState,
                    templateSelection: formatState
                });

                $filter.val(selectedStoryId || 'all').trigger('change');
            } else {
                // 2. Dropdown chọn truyện bên trong Modal (#chapterStoryId)
                const $select = $('#chapterStoryId');
                if ($select.hasClass("select2-hidden-accessible")) {
                    $select.select2('destroy');
                }
                $select.empty(); $select.append(`<option value="">${L.SelectStoryPlaceholder || 'Select story...'}</option>`);

                data.forEach(s => {
                    const selected = (selectedStoryId && selectedStoryId == s.id) ? 'selected' : '';
                    $select.append(`<option data-color="linear-gradient(135deg, #667eea, #764ba2)" value="${s.id}" ${selected}>${s.name}</option>`);
                });

                // Khởi tạo Select2 kèm dropdownParent và templateResult
                $select.select2({
                    width: '100%',
                    dropdownParent: $('#modalOverlay'),
                    placeholder: L.SelectStoryPlaceholder || 'Select story...',
                    allowClear: false,
                    minimumResultsForSearch: Infinity,
                    templateResult: formatState,
                    templateSelection: formatState
                });

                $select.val(selectedStoryId || '').trigger('change');
            }
        });
}
function loadStories(selectedId = null) {
    fetch('/Admin/tblChapters/GetStoriesForSelect')
        .then(res => res.json())
        .then(data => {
            const select = document.getElementById('chapterStoryId');
            if (!select) return;
            const selectStoryText = UI_LANG.SelectStory || 'Select story';
            select.innerHTML = `<option value="Unknow">${selectStoryText}</option>`;

            data.forEach(a => {
                const selected = selectedId == a.id ? 'selected' : '';
                select.innerHTML += `
                    <option value="${a.id}" ${selected}>
                        ${a.name}
                    </option>
                `;
            });
        });
}

// ============================================================================
// 4. ẢNH – xem trước & phóng to
// ============================================================================
function previewImage(input) {
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = function (e) {
            $('#imgPreview').attr('src', e.target.result).removeClass('hidden');
            $('#uploadIcon').addClass('hidden');
        };
        reader.readAsDataURL(input.files[0]);
    }
}

function openImagePreview(src) {
    if (!src) return;
    $('#imageModalContent').attr('src', src);
    $('#imageModal').removeClass('hidden').addClass('flex');
}

function closeImagePreview() {
    $('#imageModal').addClass('hidden').removeClass('flex');
}

// ============================================================================
// 5. MODAL IMPORT EXCEL
// ============================================================================
function openImportModal() {
    const modal = $('#importModal');
    modal.removeClass('hidden').addClass('flex');
    setTimeout(() => $('#importContent').addClass('scale-100 opacity-100'), 10);
}

function closeImportModal() {
    $('#importContent').removeClass('scale-100 opacity-100');
    setTimeout(() => {
        $('#importModal').removeClass('flex').addClass('hidden');
        resetImportForm();
    }, 300);
}

$('#excelFile').on('change', function (e) {
    const file = e.target.files[0];
    if (file) {
        $('#fileStatus').html(`<span class="text-indigo-600 font-bold italic">${file.name}</span>`);
        $('#dropZone').addClass('border-indigo-500 bg-indigo-100/50');
    }
});

function resetImportForm() {
    $('#excelFile').val('');
    const dragDropText = UI_LANG.DragDropExcelChapter || 'Drag and drop or click to import file';
    $('#fileStatus').text(dragDropText);
    $('#dropZone').removeClass('border-indigo-500 bg-indigo-100/50');
}

// ============================================================================
// 6. NÚT XÓA NHIỀU – cập nhật số lượng đã chọn
// ============================================================================
function updateDeleteButton() {
    const count = $('.user-checkbox:checked').length;
    const $btn = $('#btnDeleteSelected');
    const deleteLabel = UI_LANG.BtnDeleteText || 'Delete';

    if (count > 0) {
        $btn.html(`<i class="fas fa-trash-alt mr-2"></i>${deleteLabel}(${count})`);
        $btn.prop('disabled', false); $btn.removeClass('opacity-50 cursor-not-allowed');
    } else {
        $btn.html(`<i class="fas fa-trash-alt mr-2"></i>${deleteLabel}`);
        $btn.prop('disabled', true); $btn.addClass('opacity-50 cursor-not-allowed');
    }
}
