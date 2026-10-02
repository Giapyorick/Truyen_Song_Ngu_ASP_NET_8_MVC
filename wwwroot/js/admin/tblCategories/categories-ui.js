/* categories-ui.js - Quản lý giao diện, hiệu ứng Select2 và đóng mở Modal */

const UI_LANG = window.ADMIN_LANG || {};

$(document).ready(function () {
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

// Mở modal Thêm hoặc Sửa thể loại
function openModal(mode, id = null) {
    const modal = $('#modalOverlay');
    $('#userForm')[0].reset();

    if (mode === 'add') {
        $('#modalTitle').text(UI_LANG.TitleAddCategory || 'Add category');
        $('#categoryId').val('0');
        $('#categoryStatus').val('Active').trigger('change.select2');
    } else {
        $('#modalTitle').text(UI_LANG.TitleEditCategory || 'Update category');
        $.get('/Admin/tblCategories/GetById/' + id, function (data) {
            $('#categoryId').val(data.categoryId);
            $('#categoryName').val(data.name);
            $('#categoryDescription').val(data.description);
            $('#categoryStatus').val(data.status).trigger('change.select2');
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
    const dragDropText = UI_LANG.DragDropExcelCategory || 'Drag and drop or click to import file';
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