/**
 * ============================================================================
 *  FILE      : wwwroot/js/admin/authors/authors-ui.js
 *  CHỨC NĂNG : Quản lý Tác giả – giao diện: Flatpickr, Select2 quốc gia, xem trước ảnh, modal Import
 *  DÙNG TẠI  : Areas/Admin/Views/tblAuthors/Authors.cshtml
 *  PHỤ THUỘC : jQuery, Select2, Flatpickr, window.ADMIN_LANG
 *  THỨ TỰ    : Nạp TRƯỚC authors-crud.js
 * ----------------------------------------------------------------------------
 *  MỤC LỤC
 *    1. BIẾN NGÔN NGỮ GIAO DIỆN
 *    2. KHỞI TẠO FLATPICKR / SELECT2 KHI TẢI TRANG
 *    3. DROPDOWN QUỐC GIA (Select2)
 *    4. ẢNH ĐẠI DIỆN – xem trước & phóng to
 *    5. MODAL IMPORT EXCEL
 *    6. NÚT XÓA NHIỀU – cập nhật số lượng đã chọn
 * ============================================================================
 */

// ============================================================================
// 1. BIẾN NGÔN NGỮ GIAO DIỆN
// ============================================================================
const UI_LANG = window.ADMIN_LANG || {};

// ============================================================================
// 2. KHỞI TẠO FLATPICKR / SELECT2 KHI TẢI TRANG
// ============================================================================
$(document).ready(function () {
    // 1. Khởi tạo Flatpickr
    $(".datepicker").flatpickr({
        dateFormat: "Y-m-d",
        allowInput: true
    });

    // 2. Khởi tạo Select2 Custom màu gradient cho các dropdown khác
    $('.select2-custom').not('#authorCountry').each(function () {
        $(this).select2({
            width: '100%',
            minimumResultsForSearch: Infinity,
            templateResult: formatSelectState,
            templateSelection: formatSelectState
        });
    });

    // 3. Nạp danh sách quốc gia bất đồng bộ
    loadCountries();
});

// ============================================================================
// 3. DROPDOWN QUỐC GIA (Select2)
// ============================================================================
function loadCountries(selectedValue = "Unknow") {
    $.get('/data/countries.txt', function (data) {
        const lines = data.split('\n');
        const defaultText = UI_LANG.SelectCountry || 'Select country';
        let options = `<option value="Unknow">${defaultText}</option>`;

        lines.forEach(c => {
            const country = c.trim();
            if (country !== '') {
                options += `<option value="${country}">${country}</option>`;
            }
        });

        const $select = $('#authorCountry');
        $select.html(options);

        if ($select.hasClass("select2-hidden-accessible")) {
            $select.select2('destroy');
        }

        $select.select2({
            width: '100%',
            allowClear: false,
            dropdownParent: $('#modalOverlay'),
            placeholder: defaultText
        });

        $select.val(selectedValue).trigger('change');
    }).fail(function () {
        console.error("Không thể tải file /data/countries.txt");
    });
}

function formatSelectState(state) {
    if (!state.id) return state.text;
    const gradient = $(state.element).data('color') || 'linear-gradient(135deg, #667eea, #764ba2)';
    return $(`
        <span style="display: flex; align-items: center;">
            <span style="display:inline-block; width:10px; height:10px; border-radius:50%; background:${gradient}; margin-right:10px;"></span>
            <span style="background:${gradient}; -webkit-background-clip: text; -webkit-text-fill-color: transparent; font-weight:700;">${state.text}</span>
        </span>
    `);
}

// ============================================================================
// 4. ẢNH ĐẠI DIỆN – xem trước & phóng to
// ============================================================================
// Preview ảnh khi chọn file ở form Add/Edit
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

// Modal zoom ảnh lớn
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
// Bật tắt Import Modal
function openImportModal() {
    const modal = $('#importModal');
    modal.removeClass('hidden').addClass('flex');
    setTimeout(() => $('#importContent').addClass('scale-100 opacity-100'), 10);
}

function closeImportModal() {
    $('#importContent').removeClass('scale-100 opacity-100');
    setTimeout(() => {
        $('#importModal').removeClass('flex').addClass('hidden');
        $('#excelFile').val('');
        const dragDropText = UI_LANG.DragDropExcel || 'Drag and drop file here or click to choose';
        $('#fileStatus').text(dragDropText);
        $('#dropZone').removeClass('border-indigo-500 bg-indigo-100/50');
    }, 300);
}

// ============================================================================
// 6. NÚT XÓA NHIỀU – cập nhật số lượng đã chọn
// ============================================================================
// Cập nhật text nút Delete(N)
function updateDeleteButton() {
    const count = $('.user-checkbox:checked').length;
    const $btn = $('#btnDeleteSelected');
    const deleteLabel = UI_LANG.BtnDeleteText || 'Delete';

    if (count > 0) {
        $btn.html(`<i class="fas fa-trash-alt mr-2"></i>${deleteLabel}(${count})`).prop('disabled', false).removeClass('opacity-50 cursor-not-allowed');
    } else {
        $btn.html(`<i class="fas fa-trash-alt mr-2"></i>${deleteLabel}`).prop('disabled', true).addClass('opacity-50 cursor-not-allowed');
    }
}
