/* admins-ui.js - Quản lý giao diện, hiệu ứng Select2 và đóng mở Modal */

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

// Mở modal Thêm hoặc Sửa Admin
function openModal(mode, id = null) {
    if (window.IS_VIEWER_MODE) {
        showToast(UI_LANG.ViewerNoPermissionModal || 'Viewer account has view-only permissions. Cannot add or edit accounts!', 'error');
        return;
    }

    const modal = $('#modalOverlay');
    $('#userForm')[0].reset();

    if (mode === 'add') {
        $('#modalTitle').text(UI_LANG.ModalTitleAdd || 'Add Account');
        $('#adminId').val('0');
        $('#adminPassword').prop('required', true);
        $('#pwdNotice').text(UI_LANG.PwdNoticeAdd || '(required for new account)');
        $('#adminRole').val('Viewer').trigger('change.select2');
        $('#adminStatus').val('true').trigger('change.select2');
    } else {
        $('#modalTitle').text(UI_LANG.ModalTitleEdit || 'Update Account');
        $('#adminPassword').prop('required', false);
        $('#pwdNotice').text(UI_LANG.PwdNoticeEdit || '(leave blank to keep current password)');

        $.get('/Admin/tblAdmins/GetById/' + id, function (res) {
            if (res.success) {
                const data = res.data;
                $('#adminId').val(data.adminId);
                $('#adminUsername').val(data.username);
                $('#adminFullName').val(data.fullName);
                $('#adminRole').val(data.role).trigger('change.select2');
                $('#adminStatus').val(data.isActive ? "true" : "false").trigger('change.select2');
            } else {
                showToast(res.message || (UI_LANG.CannotFetchInfo || 'Cannot fetch account information!'), 'error');
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

// Cập nhật trạng thái và số lượng trên nút Delete (N)
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