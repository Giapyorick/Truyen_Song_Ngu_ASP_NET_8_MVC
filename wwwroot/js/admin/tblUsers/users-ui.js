/* users-ui.js - Quản lý giao diện, hiệu ứng Select2, Flatpickr, phóng to ảnh và đóng/mở Modal */

const UI_LANG = window.ADMIN_LANG || {};

$(document).ready(function () {
    $(".datepicker").flatpickr({
        dateFormat: "Y-m-d",
        allowInput: true
    });

    $('.select2-custom').each(function () {
        $(this).select2({
            width: '100%',
            minimumResultsForSearch: Infinity,
            templateResult: formatState,
            templateSelection: formatState
        });
    });
});

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

    if (mode === 'add') {
        $('#modalTitle').text(UI_LANG.TitleAddMember || 'Add member');
        $('#userId').val('0');
        $('#passwordContainer').show();
        $('#userGender').val('Male').trigger('change.select2');
        $('#userStatus').val('Active').trigger('change.select2');
    } else {
        $('#modalTitle').text(UI_LANG.TitleEditMember || 'Update member');
        $('#passwordContainer').show();
        $('#userPassword')
            .val('')
            .attr('placeholder', UI_LANG.PwdPlaceholderKeep || 'Leave blank to keep current password');

        $.get('/Admin/tblUsers/GetById/' + id, function (data) {
            $('#userId').val(data.userId);
            $('#userName').val(data.name);
            $('#userEmail').val(data.email);
            $('#userPhone').val(data.phone);
            $('#userDoB').val(data.doB);

            $('#userGender').val(data.gender).trigger('change.select2');
            $('#userStatus').val(data.status).trigger('change.select2');

            if (data.img && data.img.trim() !== "") {
                const fullPath = data.img.startsWith('/') ? data.img : '/' + data.img;
                $imgPreview.attr('src', fullPath).removeClass('hidden'); $uploadIcon.addClass('hidden');
            } else {
                $imgPreview.addClass('hidden').attr('src', ''); $uploadIcon.removeClass('hidden');
            }
        });
    }

    modal.removeClass('hidden').addClass('flex');
    setTimeout(() => $('#modalContent').addClass('translate-y-0 opacity-100'), 10);
}

function closeModal() {
    const content = $('#modalContent');
    content.removeClass('translate-y-0 opacity-100 scale-100')
        .addClass('translate-y-10 opacity-0 scale-95');

    setTimeout(() => {
        $('#modalOverlay').removeClass('flex').addClass('hidden');
    }, 300);
}

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

function resetImportForm() {
    $('#excelFile').val('');
    const dragDropText = UI_LANG.DragDropExcelUser || 'Drag and drop or click to import file';
    $('#fileStatus').text(dragDropText);
    $('#dropZone').removeClass('border-indigo-500 bg-indigo-100/50');
}

$('#excelFile').on('change', function (e) {
    const file = e.target.files[0];
    if (file) {
        $('#fileStatus').html(`<span class="text-indigo-600 font-bold italic">${file.name}</span>`);
        $('#dropZone').addClass('border-indigo-500 bg-indigo-100/50');
    }
});

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