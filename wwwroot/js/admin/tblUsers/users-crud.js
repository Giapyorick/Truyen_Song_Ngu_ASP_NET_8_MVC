/* users-crud.js - Xử lý API, Lọc dữ liệu, Phân trang, CRUD người dùng, Xóa nhiều & Import/Export Excel */

const L = window.ADMIN_LANG || {};

let savedUserPage = localStorage.getItem('userPage');
let currentPage = savedUserPage ? parseInt(savedUserPage) : 1;
const pageSize = 5;

// Tải danh sách người dùng qua AJAX
function loadUserList(page = null) {
    if (page !== null && page !== undefined) {
        currentPage = parseInt(page);
    }

    localStorage.setItem('userPage', currentPage);

    const $body =$('#user-list-body');
    const genderVal = $('#filterGender').val();
    const statusVal = $('#filterStatus').val();
    const filters = {
        search: $('#filterSearch').val(),
        gender: genderVal === 'all' ? '' : genderVal,
        status: statusVal === 'all' ? '' : statusVal,
        page: currentPage,
        pageSize: pageSize
    };

    $.ajax({
        url: '/Admin/tblUsers/List',
        type: 'GET',
        data: filters,
        success: function (data) {
            let html = '';
            if (!data.users || data.users.length === 0) {
                if (currentPage > 1) {
                    loadUserList(currentPage - 1);
                    return;
                }
                const notFound = L.NotFoundUsers || 'Not found any results.';
                $body.html(`<tr><td colspan="6" class="text-center py-10 text-gray-500">${notFound}</td></tr>`);
                $('#pagination-container').html('');
                return;
            }

            data.users.forEach(user => {
                const avatarHtml = user.img
                    ? `<img onclick="openImagePreview(this.src)" src="/${user.img}" class="w-11 h-11 rounded-2xl object-cover shadow-sm border border-gray-100 cursor-pointer">`
                    : `<div class="w-11 h-11 rounded-2xl bg-gradient-to-tr from-purple-500 to-indigo-600 flex items-center justify-center text-white font-bold shadow-sm">
                        ${user.name ? user.name.charAt(0).toUpperCase() : 'U'}
                    </div>`;

                const statusActive = user.status === "Active";
                const statusClass = statusActive ? 'active' : 'inactive';
                const statusText = statusActive ? (L.StatusActive || 'Active') : (L.StatusInactive || 'Inactive');

                // Đồng bộ đa ngôn ngữ cho Giới tính
                const isMale = (user.gender || '').toLowerCase() === "male";
                const statusClassGender = isMale ? 'male' : 'female';
                const genderText = isMale 
                    ? (L.GenderMale || (window.ADMIN_LANG && window.ADMIN_LANG.GenderMale) || 'Male') 
                    : (L.GenderFemale || (window.ADMIN_LANG && window.ADMIN_LANG.GenderFemale) || 'Female');

                html += `
                <tr class="group hover:bg-indigo-50/30 transition-all">
                    <td class="px-6 py-5 text-center">
                        <input type="checkbox" class="user-checkbox w-5 h-5 rounded-md border-gray-300" value="${user.userId}" data-id="${user.userId}">
                    </td>
                    <td class="px-4 py-5">
                        <div class="flex items-center gap-4">
                            ${avatarHtml}
                            <div>
                                <div class="font-bold text-gray-700">${user.name}</div>
                                <div class="text-xs text-gray-400">${user.email}</div>
                                <div class="text-xs text-gray-400">${user.phone}</div>
                            </div>
                        </div>
                    </td>
                    <td class="px-6 py-5">
                        <span class="px-3 py-1.5 bg-slate-100 text-gray-600 rounded-lg text-xs font-semibold">
                            ${user.doB}
                        </span>
                    </td>
                    <td class="px-6 py-5 text-center">
                        <span class="font-bold px-3 py-1.5 ${statusClassGender} rounded-xl text-[10px] uppercase tracking-wider">
                            ${genderText}
                        </span>
                    </td>
                    <td class="px-6 py-5 text-center">
                        <span class="font-bold px-3 py-1.5 ${statusClass} rounded-xl text-[10px] uppercase tracking-wider">
                            ${statusText}
                        </span>
                    </td>
                    <td class="px-8 py-5 text-right">
                        <div class="flex justify-end gap-3">
                            <button onclick="openModal('edit', ${user.userId})"
                                class="w-7 aspect-square p-0
                                    flex items-center justify-center
                                    rounded-lg btn-grad bg-blue-50
                                    hover:bg-blue-600 hover:text-white
                                    transition-all duration-200" title="${L.TitleEditAccount || 'Edit'}">
                                <i class="fas fa-pencil-alt text-[11px]"></i>
                            </button>

                            <button onclick="deleteUser(${user.userId})"
                                class="w-7 aspect-square p-0
                                    flex items-center justify-center
                                    rounded-lg btn-grad-cancel bg-red-50
                                    hover:bg-red-600 hover:text-white
                                    transition-all duration-200" title="${L.TitleDeleteAccount || 'Delete'}">
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
        error: function () {
            const errLoad = L.ErrLoadUsers || 'Error loading data.';
            $body.html(`<tr><td colspan="6" class="text-center py-10 text-red-500">${errLoad}</td></tr>`);
            if (typeof showToast === 'function') {
                showToast(L.ErrLoadUsers || 'Error loading user data!', 'error');
            }
        }
    });
}

// Xóa một người dùng đơn lẻ
async function deleteUser(id) {
    const actionText = L.ActionCannotUndo || 'This action cannot be undone.';
    const confirmMsg = (L.ConfirmDeleteSingleUserMsg || 'Are you sure to remove this user?') + `<br><small class="text-red-400">${actionText}</small>`;
    const confirmTitle = L.ConfirmDeleteSingleUserTitle || "Delete User";

    const confirmed = await customConfirm(confirmMsg, confirmTitle);

    if (confirmed) {
        const $row = $(`button[onclick="deleteUser(${id})"]`).closest('tr');
        $row.addClass('opacity-50 pointer-events-none');

        $.ajax({
            url: '/Admin/tblUsers/Delete',
            type: 'POST',
            data: { id: id },
            success: function (response) {
                if (response.success) {
                    if (typeof showToast === 'function') {
                        showToast(response.message, 'success');
                    }

                    const remainingRows = $('#user-list-body tr').length - 1;
                    if (remainingRows <= 0 && currentPage > 1) {
                        currentPage--;
                    }

                    loadUserList(currentPage);
                } else {
                    if (typeof showToast === 'function') {
                        showToast(response.message || 'Error occurred while deleting!', 'error');
                    }
                    $row.removeClass('opacity-50 pointer-events-none');
                }
            },
            error: function () {
                if (typeof showToast === 'function') {
                    showToast(L.ErrConnectDelete || 'Cannot connect to the server to delete.', 'error');
                }
                $row.removeClass('opacity-50 pointer-events-none');
            }
        });
    }
}

// Xuất danh sách người dùng ra file Excel
function exportExcel() {
    const search = $('#filterSearch').val();
    const gender = $('#filterGender').val();
    const status = $('#filterStatus').val();

    window.location.href = `/Admin/tblUsers/ExportToExcel?search=${search}&gender=${gender}&status=${status}`;
}

// Gửi form Thêm / Cập nhật người dùng
$('#userForm').on('submit', function (e) {
    e.preventDefault();

    localStorage.setItem('userPage', currentPage);

    const submitBtn = $(this).find('button[type="submit"]');
    const formData = new FormData(this);

    const userId = $('#userId').val();
    const isAdding = (userId == "0" || userId == "" || !userId);
    const url = isAdding ? '/Admin/tblUsers/Add' : '/Admin/tblUsers/Update';

    submitBtn.prop('disabled', true).html(`<i class="fas fa-spinner animate-spin"></i> ${L.Processing || 'Processing...'}`);

    $.ajax({
        url: url,
        type: 'POST',
        data: formData,
        contentType: false,
        processData: false,
        success: function (response) {
            if (response.success) {
                if (typeof showToast === 'function') {
                    showToast(response.message, 'success');
                }
                closeModal();

                if (isAdding) {
                    loadUserList(1);
                } else {
                    const savedPage = localStorage.getItem('userPage');
                    const targetPage = savedPage ? parseInt(savedPage) : currentPage;
                    loadUserList(targetPage);
                }
            } else {
                if (typeof showToast === 'function') {
                    showToast(response.message || 'Operation failed!', 'error');
                }
            }
        },
        error: function () {
            if (typeof showToast === 'function') {
                showToast(L.ErrConnectServer || 'Cannot connect to the server.', 'error');
            }
        },
        complete: function () {
            submitBtn.prop('disabled', false).html(L.SaveChanges || 'Save Changes');
        }
    });
});

// Xử lý Import dữ liệu từ Excel
function executeImport() {
    const fileInput = document.getElementById('excelFile');
    if (fileInput.files.length === 0) {
        if (typeof showToast === 'function') {
            showToast(L.ChooseExcelFile || 'Please choose an Excel file!', 'error');
        }
        return;
    }

    const formData = new FormData();
    formData.append('file', fileInput.files[0]);

    const $btn = $('#btnDoImport');
    $btn.prop('disabled', true).html(`<i class="fas fa-spinner fa-spin"></i> ${L.Processing || 'Loading...'}`);

    $.ajax({
        url: '/Admin/tblUsers/ImportExcel',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            if (res.success) {
                if (typeof showToast === 'function') {
                    showToast(res.message, 'success');
                }
                closeImportModal();
                loadUserList(1);
            } else {
                if (typeof showToast === 'function') {
                    showToast(res.message, 'error');
                }
            }
        },
        error: function (xhr) {
            let errMsg = L.ErrImport || 'Error during importing file!';
            try {
                const res = JSON.parse(xhr.responseText);
                if (res) {
                    if (res.message) errMsg = res.message;
                    if (res.detail) errMsg += ' - Details: ' + res.detail;
                }
            } catch (e) { }

            if (typeof showToast === 'function') {
                showToast(errMsg, 'error');
            }
            console.error('ImportExcel error', xhr);
        },
        complete: function () {
            $btn.prop('disabled', false).text(L.ConfirmImport || 'Confirm Import');
        }
    });
}

// Render các nút bấm phân trang
function renderPagination(currentPage, totalPages) {
    const container = $('#pagination-container');
    container.empty();

    if (totalPages <= 1) return;

    const maxVisible = 2;
    let html = `<div class="flex items-center gap-1">`;

    html += `
    <button onclick="loadUserList(1)"
        class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40' : ''}"
        ${currentPage === 1 ? 'disabled' : ''}>
        ⏮
    </button>`;

    html += `
    <button onclick="loadUserList(${currentPage - 1})"
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
    <button onclick="loadUserList(${currentPage + 1})"
        class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40' : ''}"
        ${currentPage === totalPages ? 'disabled' : ''}>
        ▶
    </button>`;

    html += `
    <button onclick="loadUserList(${totalPages})"
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
    <button onclick="loadUserList(${page})"
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

    loadUserList(page);
}

// Lắng nghe sự kiện tải trang, Tìm kiếm, Lọc và Xóa nhiều người dùng
$(document).ready(function () {
    const savedPage = localStorage.getItem('userPage');
    currentPage = savedPage ? parseInt(savedPage) : 1;
    loadUserList(currentPage);

    $('#filterSearch').on('keyup', function () {
        loadUserList(1);
    });

    $('#filterGender, #filterStatus').on('change', function () {
        loadUserList(1);
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

    // Xóa nhiều người dùng đã chọn
    $(document).on('click', '#btnDeleteSelected', async function () {
        const ids = $('.user-checkbox:checked')
            .map(function () {
                return parseInt(this.value);
            })
            .get();

        if (ids.length === 0) {
            if (typeof showToast === 'function') {
                showToast(L.SelectAtLeastOneUser || 'Please select at least one user!', 'error');
            }
            return;
        }

        const actionText = L.ActionCannotUndo || 'This action cannot be undone.';
        const multiMsg = (L.ConfirmDeleteMultiUsersMsg || 'Are you sure to remove <b>{0}</b> user(s)?')
            .replace('{0}', ids.length) + `<br><small class="text-red-400">${actionText}</small>`;
        const multiTitle = L.ConfirmDeleteMultiUsersTitle || "Delete Users";

        const confirmed = await customConfirm(multiMsg, multiTitle);
        if (!confirmed) return;

        $.ajax({
            url: '/Admin/tblUsers/DeleteMultiple',
            type: 'POST',
            traditional: true,
            data: { ids: ids },
            success: function (res) {
                if (res.blocked && res.blocked.length > 0) {
                    if (typeof showToast === 'function') {
                        const blockedTemplate = L.BlockedDeleteUser || 'Cannot delete ID: {0} due to foreign key constraints.';
                        showToast(blockedTemplate.replace('{0}', res.blocked.join(', ')), 'error');
                    }
                }
                if (res.deleted && res.deleted.length > 0) {
                    if (typeof showToast === 'function') {
                        const deletedTemplate = L.DeletedUsersSuccess || 'Deleted {0} user(s) successfully.';
                        showToast(deletedTemplate.replace('{0}', res.deleted.length), 'success');
                    }
                }

                const totalOnPage = $('.user-checkbox').length;
                if (ids.length >= totalOnPage && currentPage > 1) {
                    currentPage--;
                }

                loadUserList(currentPage);
                $('#selectAll').prop('checked', false);
            },
            error: function () {
                if (typeof showToast === 'function') {
                    showToast(L.ErrDeleteUsersServer || 'Failed to delete selected users from server.', 'error');
                }
            }
        });
    });
});