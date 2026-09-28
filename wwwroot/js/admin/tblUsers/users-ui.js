/* users-ui.js - Quản lý giao diện, hiệu ứng Select2, Flatpickr, phóng to ảnh và đóng/mở Modal */

$(document).ready(function () {
    // Khởi tạo Flatpickr cho ô chọn ngày sinh
    $(".datepicker").flatpickr({
        dateFormat: "Y-m-d",
        allowInput: true
    });

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

// Định dạng chấm tròn và chữ gradient cho từng option của Select2
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

// Mở modal Thêm hoặc Sửa người dùng
function openModal(mode, id = null) {
    const modal = $('#modalOverlay');
    const $imgPreview = $('#imgPreview');
    const $uploadIcon = $('#uploadIcon');
    $('#userForm')[0].reset();

    $imgPreview.addClass('hidden').attr('src', ''); $uploadIcon.removeClass('hidden');

    // Trường hợp thêm mới
    if (mode === 'add') {
        $('#modalTitle').text('Add member');
        $('#userId').val('0');
        $('#passwordContainer').show();
    } else {
        // Trường hợp cập nhật
        $('#modalTitle').text('Update member');
        $('#passwordContainer').show();
        $('#userPassword')
            .val('')
            .attr('placeholder', 'Leave blank to keep current password');

        $.get('/Admin/tblUsers/GetById/' + id, function (data) {
            console.log("data received:", data);
            $('#userId').val(data.userId);
            $('#userName').val(data.name);
            $('#userEmail').val(data.email);
            $('#userPhone').val(data.phone);
            $('#userDoB').val(data.doB);
            $('#userGender').val(data.gender).trigger('change.select2');
            $('#userStatus').val(data.status).trigger('change.select2');

            if (data.img && data.img.trim() !== "") {
                const fullPath = data.img.startsWith('/') ? data.img : '/' + data.img;
                console.log("last image path:", fullPath);

                $imgPreview.attr('src', fullPath);
                $imgPreview.removeClass('hidden'); $uploadIcon.addClass('hidden');
            } else {
                $imgPreview.addClass('hidden').attr('src', ''); $uploadIcon.removeClass('hidden');
            }
        });
    }

    modal.removeClass('hidden').addClass('flex');
    $('.select2-custom').trigger('change');

    setTimeout(() => $('#modalContent').addClass('translate-y-0 opacity-100'), 10);
}

// Đóng modal Thêm hoặc Sửa người dùng
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

// Xem trước ảnh khi chọn từ file input
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

// Mở modal phóng to ảnh
function openImagePreview(src) {
    if (!src) return;
    $('#imageModalContent').attr('src', src);
    $('#imageModal').removeClass('hidden').addClass('flex');
}

// Đóng modal phóng to ảnh
function closeImagePreview() {
    $('#imageModal').addClass('hidden').removeClass('flex');
}

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