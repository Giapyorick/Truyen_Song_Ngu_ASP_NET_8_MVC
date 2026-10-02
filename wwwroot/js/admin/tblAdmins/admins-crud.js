/* admins-crud.js - Xử lý API, Bộ lọc, Phân trang, Thêm, Sửa, Xóa đơn lẻ và Xóa nhiều cho Admins */

const L = window.ADMIN_LANG || {};

let savedAdminPage = localStorage.getItem('adminPage');
let currentPage = savedAdminPage ? parseInt(savedAdminPage) : 1;
const pageSize = 5;

// Tải danh sách Admin từ server qua AJAX
function loadAdminList(page = null) {
    if (page !== null && page !== undefined) {
        currentPage = parseInt(page);
    }

    localStorage.setItem('adminPage', currentPage);

    const $body =$('#user-list-body');
    const roleVal = $('#filterRole').val();
    const statusVal = $('#filterStatus').val();

    const filters = {
        search: $('#filterSearch').val(),
        role: roleVal === 'all' ? '' : roleVal,
        status: statusVal === 'all' ? '' : statusVal,
        page: currentPage,
        pageSize: pageSize
    };

    $.ajax({
        url: '/Admin/tblAdmins/List',
        type: 'GET',
        data: filters,
        success: function (data) {
            let html = '';
            if (!data.admins || data.admins.length === 0) {
                if (currentPage > 1) {
                    loadAdminList(currentPage - 1);
                    return;
                }
                const notFoundMsg = L.NotFoundAccounts || 'Not found any accounts.';
                $body.html(`<tr><td colspan="7" class="text-center py-10 text-gray-500 font-semibold">${notFoundMsg}</td></tr>`);
                $('#pagination-container').html('');
                return;
            }

            data.admins.forEach(item => {
                const isRoleAdmin = (item.role || '').toLowerCase() === 'admin';
                const roleClass = isRoleAdmin ? 'text-field' : 'text-wait';
                
                // Đồng bộ đa ngôn ngữ cho Role hiển thị
                const roleText = isRoleAdmin 
                    ? (L.RoleAdmin || (window.ADMIN_LANG && window.ADMIN_LANG.RoleAdmin) || 'Admin')
                    : (L.RoleViewer || (window.ADMIN_LANG && window.ADMIN_LANG.RoleViewer) || 'Viewer');

                const statusClass = item.isActive ? 'active' : 'inactive';
                const statusText = item.isActive ? (L.StatusActive || 'Active') : (L.StatusInactive || 'Inactive');
                const displayName = item.fullName || (L.NoDisplayName || 'No display name');
                const lastLoginText = item.lastLogin || (L.Never || 'Never');

                const avatarChar = item.username ? item.username.charAt(0).toUpperCase() : 'A';

                html += `
                <tr class="group hover:bg-indigo-50/30 transition-all">
                    <td class="px-6 py-5 text-center">
                        <input type="checkbox" class="user-checkbox w-5 h-5 rounded-md border-gray-300 cursor-pointer" value="${item.adminId}" data-id="${item.adminId}">
                    </td>
                    <td class="px-4 py-5">
                        <div class="flex items-center gap-4">
                            <div class="w-11 h-11 rounded-2xl bg-gradient-to-tr from-cyan-400 to-teal-500 flex items-center justify-center text-white font-extrabold shadow-sm">
                                ${avatarChar}
                            </div>
                            <div>
                                <div class="font-bold text-gray-700">${item.username}</div>
                                <div class="text-xs text-gray-400">${displayName}</div>
                            </div>
                        </div>
                    </td>
                    <td class="px-6 py-5 text-center">
                        <span class="font-bold px-3 py-1.5 ${roleClass} rounded-xl text-[10px] uppercase tracking-wider">
                            ${roleText}
                        </span>
                    </td>
                    <td class="px-6 py-5 text-center">
                        <span class="font-bold px-3 py-1.5 ${statusClass} rounded-xl text-[10px] uppercase tracking-wider">
                            ${statusText}
                        </span>
                    </td>
                    <td class="px-6 py-5 text-xs text-gray-500 font-medium">
                        ${lastLoginText}
                    </td>
                    <td class="px-6 py-5 text-xs text-gray-400">
                        ${item.createdAt || 'N/A'}
                    </td>
                    <td class="px-8 py-5 text-right">
                        <div class="flex justify-end gap-3">
                            <button onclick="openModal('edit', ${item.adminId})"
                                class="w-7 aspect-square p-0 flex items-center justify-center rounded-lg btn-grad bg-blue-50 hover:bg-blue-600 hover:text-white transition-all duration-200" title="${L.TitleEditAccount || 'Edit account'}">
                                <i class="fas fa-pencil-alt text-[11px]"></i>
                            </button>

                            <button onclick="deleteAdmin(${item.adminId})"
                                class="w-7 aspect-square p-0 flex items-center justify-center rounded-lg btn-grad-cancel bg-red-50 hover:bg-red-600 hover:text-white transition-all duration-200" title="${L.TitleDeleteAccount || 'Delete account'}">
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
            const errMsg = L.ErrLoadAccounts || 'Error loading accounts data.';
            $body.html(`<tr><td colspan="7" class="text-center py-10 text-red-500 font-semibold">${errMsg}</td></tr>`);
            showToast(L.ErrConnectServerLoad || 'Cannot connect to server to load data.', 'error');
        }
    });
}

// Xóa một Admin đơn lẻ
async function deleteAdmin(id) {
    if (window.IS_VIEWER_MODE) {
        showToast(L.ViewerNoPermissionDelete || 'Viewer account has view-only permissions. Cannot delete data!', 'error');
        return;
    }

    const actionText = L.ActionCannotUndo || 'This action cannot be undone.';
    const confirmMsg = (L.ConfirmDeleteSingleMsg || 'Are you sure to remove this account?') + `<br><small class="text-red-400">${actionText}</small>`;
    const confirmTitle = L.ConfirmDeleteSingleTitle || 'Delete Account';

    const confirmed = await customConfirm(confirmMsg, confirmTitle);

    if (confirmed) {
        const $row = $(`button[onclick="deleteAdmin(${id})"]`).closest('tr');
        $row.addClass('opacity-50 pointer-events-none');

        $.ajax({
            url: '/Admin/tblAdmins/Delete',
            type: 'POST',
            data: { id: id },
            success: function (response) {
                if (response.success) {
                    showToast(response.message, 'success');

                    const remainingRows = $('#user-list-body tr').length - 1;
                    if (remainingRows <= 0 && currentPage > 1) {
                        currentPage--;
                    }

                    loadAdminList(currentPage);
                } else {
                    showToast("Error: " + response.message, 'error');
                    $row.removeClass('opacity-50 pointer-events-none');
                }
            },
            error: function () {
                showToast(L.ErrConnectDelete || 'Cannot connect to server to delete account.', 'error');
                $row.removeClass('opacity-50 pointer-events-none');
            }
        });
    }
}

// Gửi form Thêm / Cập nhật Admin
$('#userForm').on('submit', function (e) {
    e.preventDefault();

    if (window.IS_VIEWER_MODE) {
        showToast(L.ViewerNoPermissionSave || 'Viewer account has view-only permissions. Cannot save changes!', 'error');
        return;
    }

    localStorage.setItem('adminPage', currentPage);

    const submitBtn = $(this).find('button[type="submit"]');
    const adminId = parseInt($('#adminId').val()) || 0;
    const isAdding = (adminId === 0);
    const url = isAdding ? '/Admin/tblAdmins/Add' : '/Admin/tblAdmins/Update';

    const payload = {
        adminId: adminId,
        username: $('#adminUsername').val().trim(),
        password: $('#adminPassword').val(),
        fullName: $('#adminFullName').val().trim(),
        role: $('#adminRole').val(),
        isActive: $('#adminStatus').val() === 'true'
    };

    submitBtn.prop('disabled', true).html(`<i class="fas fa-spinner animate-spin"></i> ${L.Processing || 'Processing...'}`);

    $.ajax({
        url: url,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(payload),
        success: function (response) {
            if (response.success) {
                showToast(response.message, 'success');
                closeModal();

                if (isAdding) {
                    loadAdminList(1);
                } else {
                    const savedPage = localStorage.getItem('adminPage');
                    const targetPage = savedPage ? parseInt(savedPage) : currentPage;
                    loadAdminList(targetPage);
                }
            } else {
                showToast("Error: " + response.message, 'error');
            }
        },
        error: function () {
            showToast(L.ErrConnectServer || 'Cannot connect to the server.', 'error');
        },
        complete: function () {
            submitBtn.prop('disabled', false).html(L.SaveChanges || 'Save Changes');
        }
    });
});

// Render các nút bấm phân trang
function renderPagination(currentPage, totalPages) {
    const container = $('#pagination-container');
    container.empty();

    if (totalPages <= 1) return;

    const maxVisible = 2;
    let html = `<div class="flex items-center gap-1">`;

    html += `
    <button onclick="loadAdminList(1)"
        class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40 pointer-events-none' : ''}">
        ⏮
    </button>`;

    html += `
    <button onclick="loadAdminList(${currentPage - 1})"
        class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40 pointer-events-none' : ''}">
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
    <button onclick="loadAdminList(${currentPage + 1})"
        class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40 pointer-events-none' : ''}">
        ▶
    </button>`;

    html += `
    <button onclick="loadAdminList(${totalPages})"
        class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40 pointer-events-none' : ''}">
        ⏭
    </button>`;

    html += `
    <div class="flex items-center gap-1 ml-3">
        <span class="text-sm">Go:</span>
        <input type="number"
            min="1"
            max="${totalPages}"
            value="${currentPage}"
            class="w-16 px-2 py-1 border rounded text-center text-sm"
            onkeydown="if(event.key==='Enter') gotoPage(this, ${totalPages})">
    </div>`;

    html += `</div>`;
    container.html(html);
}

function pageBtn(page, current) {
    const active = page === current;
    return `
    <button onclick="loadAdminList(${page})"
        class="px-3 py-1 border rounded text-sm
        ${active ? 'btn-grad text-white font-bold' : 'hover:bg-gray-100'}">
        ${page}
    </button>`;
}

function gotoPage(input, totalPages) {
    let page = parseInt(input.value);
    if (isNaN(page)) return;

    if (page < 1) page = 1;
    if (page > totalPages) page = totalPages;

    loadAdminList(page);
}

// Khởi chạy sự kiện khi tài liệu đã sẵn sàng
$(document).ready(function () {
    const savedPage = localStorage.getItem('adminPage');
    currentPage = savedPage ? parseInt(savedPage) : 1;
    loadAdminList(currentPage);

    $('#filterSearch').on('keyup', function () {
        loadAdminList(1);
    });

    $('#filterRole, #filterStatus').on('change', function () {
        loadAdminList(1);
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

    // Xóa nhiều Admin đã chọn
    $(document).on('click', '#btnDeleteSelected', async function () {
        if (window.IS_VIEWER_MODE) {
            showToast(L.ViewerNoPermissionDelete || 'Viewer account has view-only permissions. Cannot delete data!', 'error');
            return;
        }

        const ids = $('.user-checkbox:checked')
            .map(function () {
                return parseInt(this.value);
            })
            .get();

        if (ids.length === 0) {
            showToast(L.SelectAtLeastOne || 'Please select at least one account to delete!', 'error');
            return;
        }

        const actionText = L.ActionCannotUndo || 'This action cannot be undone.';
        const multiMsg = (L.ConfirmDeleteMultiMsg || 'Are you sure you want to delete {0} account(s)?')
            .replace('{0}', `<strong>${ids.length}</strong>`) + `<br><small class="text-red-400">${actionText}</small>`;
        const multiTitle = L.ConfirmDeleteMultiTitle || 'Delete Accounts';

        const confirmed = await customConfirm(multiMsg, multiTitle);

        if (!confirmed) return;

        $.ajax({
            url: '/Admin/tblAdmins/DeleteMultiple',
            type: 'POST',
            traditional: true,
            data: { ids: ids },
            success: function (res) {
                if (res.success) {
                    showToast(res.message, 'success');
                } else {
                    showToast(res.message || 'Error occurred while deleting accounts.', 'error');
                }

                const totalOnPage = $('.user-checkbox').length;
                if (ids.length >= totalOnPage && currentPage > 1) {
                    currentPage--;
                }

                loadAdminList(currentPage);
                $('#selectAll').prop('checked', false);
            },
            error: function () {
                showToast(L.ErrConnectDelete || 'Failed to connect to the server to delete accounts.', 'error');
            }
        });
    });
});