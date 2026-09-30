/* categories-ui.js - Quản lý giao diện, hiệu ứng Select2 và đóng mở Modal */

$(document).ready(function () {
    // Khởi tạo Select2 với định dạng màu sắc gradient
    $('.select2-custom').each(function () {
        $(this).select2({
            width: '100%',
            minimumResultsForSearch: Infinity,
            templateResult: formatState,
            templateSelection: formatState
        });
    });
});

// Định dạng chấm tròn và chữ màu gradient cho từng option của Select2
function formatState(state) {
    if (!state.id) { return state.text; }

    const gradient = $(state.element).data('color') || 'linear-gradient(135deg, #667eea, #764ba2)';

    return $(
        '<span style="display: flex; align-items: center;">' +
        '<span style="display:inline-block; width:10px; height:10px; border-radius:50%; background:' + gradient + '; margin-right:10px;"></span>' +
        '<span style="background:' + gradient + '; -webkit-background-clip: text; -webkit-text-fill-color: transparent; font-weight:700;">' + state.text + '</span>' +
        '</span>'
    );
}

// Mở modal Thêm hoặc Sửa thể loại
function openModal(mode, id = null) {
    const modal = $('#modalOverlay');

    $('#userForm')[0].reset();

    // Trường hợp thêm mới
    if (mode === 'add') {
        $('#modalTitle').text('Add category');
        $('#categoryId').val('0');
        $('#categoryStatus').val('Active').trigger('change.select2');
    } else {
        // Trường hợp cập nhật
        $('#modalTitle').text('Update category');
        $.get('/Admin/tblCategories/GetById/' + id, function (data) {
            console.log("data received:", data);
            $('#categoryId').val(data.categoryId);
            $('#categoryName').val(data.name);
            $('#categoryDescription').val(data.description);
            $('#categoryStatus').val(data.status).trigger('change.select2');
        });
    }

    modal.removeClass('hidden').addClass('flex');

    // ĐÃ XÓA: $('.select2-custom').trigger('change'); (Dòng này làm kích hoạt nhầm bộ lọc và nhảy về trang 1)

    setTimeout(() => $('#modalContent').addClass('translate-y-0 opacity-100'), 10);
}

// Đóng modal Thêm hoặc Sửa thể loại
function closeModal() {
    const content = $('#modalContent');
    content.removeClass('translate-y-0 opacity-100 scale-100')
        .addClass('translate-y-10 opacity-0 scale-95');

    setTimeout(() => {
        $('#modalOverlay').removeClass('flex').addClass('hidden');
    }, 300);
}

// Mở modal Import Excel
function openImportModal() {
    const modal = $('#importModal');
    modal.removeClass('hidden').addClass('flex');
    setTimeout(() => $('#importContent').addClass('scale-100 opacity-100'), 10);
}

// Đóng modal Import Excel
function closeImportModal() {
    $('#importContent').removeClass('scale-100 opacity-100');
    setTimeout(() => {
        $('#importModal').removeClass('flex').addClass('hidden');
        resetImportForm();
    }, 300);
}

// Reset trạng thái form chọn file Excel
function resetImportForm() {
    $('#excelFile').val('');
    $('#fileStatus').text('Drag and drop or click to import file');
    $('#dropZone').removeClass('border-indigo-500 bg-indigo-100/50');
}

// Lắng nghe sự kiện chọn file Excel
$('#excelFile').on('change', function (e) {
    const file = e.target.files[0];
    if (file) {
        $('#fileStatus').html(`<span class="text-indigo-600 font-bold italic">${file.name}</span>`);
        $('#dropZone').addClass('border-indigo-500 bg-indigo-100/50');
    }
});

// Cập nhật trạng thái và số lượng trên nút Delete (N)
function updateDeleteButton() {
    const count = $('.user-checkbox:checked').length;
    const $btn = $('#btnDeleteSelected');

    if (count > 0) {
        $btn.html(`<i class="fas fa-trash-alt mr-2"></i>Delete(${count})`);
        $btn.prop('disabled', false); $btn.removeClass('opacity-50 cursor-not-allowed');
    } else {
        $btn.html(`<i class="fas fa-trash-alt mr-2"></i>Delete`);
        $btn.prop('disabled', true); $btn.addClass('opacity-50 cursor-not-allowed');
    }
}