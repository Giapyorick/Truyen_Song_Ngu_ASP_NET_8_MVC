/* users-crud.js - Xử lý API, Lọc dữ liệu, Phân trang, CRUD người dùng, Xóa nhiều & Import/Export Excel */

let currentPage = 1;
const pageSize = 5;

// Tải danh sách người dùng qua AJAX
function loadUserList(page = 1) {
    currentPage = page;
    const $body = $('#user-list-body');
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
            console.log("Data received from the Server:", data);
            if (!data.users || data.users.length === 0) {
                $body.html('<tr><td colspan="6" class="text-center py-10 text-gray-500">Not found any results.</td></tr>');
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
                const statusGender = user.gender === "Male";
                const statusClassGender = statusGender ? 'male' : 'female';

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
                            ${user.gender}
                        </span>
                    </td>
                    <td class="px-6 py-5 text-center">
                        <span class="font-bold px-3 py-1.5 ${statusClass} rounded-xl text-[10px] uppercase tracking-wider">
                            ${user.status}
                        </span>
                    </td>
                    <td class="px-8 py-5 text-right">
                        <div class="flex justify-end gap-3">
                            <button onclick="openModal('edit', ${user.userId})"
                                class="w-7 aspect-square p-0
                                    flex items-center justify-center
                                    rounded-lg btn-grad bg-blue-50
                                    hover:bg-blue-600 hover:text-white
                                    transition-all duration-200">
                                <i class="fas fa-pencil-alt text-[11px]"></i>
                            </button>

                            <button onclick="deleteUser(${user.userId})"
                                class="w-7 aspect-square p-0
                                    flex items-center justify-center
                                    rounded-lg btn-grad-cancel bg-red-50
                                    hover:bg-red-600 hover:text-white
                                    transition-all duration-200">
                                <i class="fas fa-trash-alt text-[11px]"></i>
                            </button>
                        </div>
                    </td>
                </tr>`;
            });

            $body.html(html);
            renderPagination(data.currentPage, data.totalPages);
            updateDeleteButton();
        },
        error: function (xhr) {
            $body.html('<tr><td colspan="6" class="text-center py-10 text-red-500">Error loading data.</td></tr>');
        }
    });
}

// Xóa một người dùng đơn lẻ
function deleteUser(id) {
    if (confirm('Are you sure to remove this member? This action cannot be undone.')) {
        const $row = $(`button[onclick="deleteUser(${id})"]`).closest('tr');
        $row.addClass('opacity-50 pointer-events-none');

        $.ajax({
            url: '/Admin/tblUsers/Delete',
            type: 'POST',
            data: { id: id },
            success: function (response) {
                if (response.success) {
                    $row.fadeOut(400, function () {
                        $(this).remove();
                        alert(response.message);
                    });
                } else {
                    alert("Error: " + response.message);
                    $row.removeClass('opacity-50 pointer-events-none');
                }
            },
            error: function () {
                alert("Cannot connect to the server to delete.");
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
    const url = (userId == "0" || userId == "") ? '/Admin/tblUsers/Add' : '/Admin/tblUsers/Update';

    submitBtn.prop('disabled', true).html('<i class="fas fa-spinner animate-spin"></i> Processing...');

    $.ajax({
        url: url,
        type: 'POST',
        data: formData,
        contentType: false,
        processData: false,
        success: function (response) {
            if (response.success) {
                alert(response.message);
                closeModal();
                const savedPage = localStorage.getItem('userPage');
                currentPage = savedPage ? parseInt(savedPage) : 1;
                loadUserList(currentPage);
            } else {
                alert("Error: " + response.message);
            }
        },
        error: function () {
            alert("Cannot connect to the server.");
        },
        complete: function () {
            submitBtn.prop('disabled', false).html('Save Changes');
        }
    });
});

// Xử lý Import dữ liệu từ Excel
function executeImport() {
    const fileInput = document.getElementById('excelFile');
    if (fileInput.files.length === 0) {
        alert('Please choose an Excel file!');
        return;
    }

    const formData = new FormData();
    formData.append('file', fileInput.files[0]);

    const $btn = $('#btnDoImport');
    $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> Loading...');

    $.ajax({
        url: '/Admin/tblUsers/ImportExcel',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            if (res.success) {
                alert(res.message);
                closeImportModal();
                loadUserList(1);
            } else {
                alert(res.message);
            }
        },
        error: function (xhr) {
            var errMsg = 'Error during importing file!';
            try {
                var res = JSON.parse(xhr.responseText);
                if (res) {
                    if (res.message) errMsg = res.message;
                    if (res.detail) errMsg += '\n\nDetails:\n' + res.detail;
                }
            } catch (e) {
                // Bỏ qua lỗi parse JSON
            }

            alert(errMsg);
            console.error('ImportExcel error', xhr);
        },
        complete: function () {
            $btn.prop('disabled', false).text('Confirm Import');
        }
    });
}

// Render các nút bấm phân trang
function renderPagination(currentPage, totalPages) {
    const container = $('#pagination-container');
    container.empty();

    if (totalPages <= 1) return;

    const maxVisible = 2; // Số trang hiển thị hai bên trang hiện tại
    let html = `<div class="flex items-center gap-1">`;

    // Nút về trang đầu tiên
    html += `
    <button onclick="loadUserList(1)"
        class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40' : ''}"
        ${currentPage === 1 ? 'disabled' : ''}>
        ⏮
    </button>`;

    // Nút về trang trước
    html += `
    <button onclick="loadUserList(${currentPage - 1})"
        class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40' : ''}"
        ${currentPage === 1 ? 'disabled' : ''}>
        ◀
    </button>`;

    // Trang 1 và dấu ba chấm nếu ở xa
    if (currentPage > maxVisible + 1) {
        html += pageBtn(1, currentPage);
        html += `<span class="px-2">…</span>`;
    }

    // Các trang xung quanh
    for (let i = Math.max(1, currentPage - maxVisible);
        i <= Math.min(totalPages, currentPage + maxVisible);
        i++) {
        html += pageBtn(i, currentPage);
    }

    // Dấu ba chấm và trang cuối
    if (currentPage < totalPages - maxVisible) {
        html += `<span class="px-2">…</span>`;
        html += pageBtn(totalPages, currentPage);
    }

    // Nút sang trang kế tiếp
    html += `
    <button onclick="loadUserList(${currentPage + 1})"
        class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40' : ''}"
        ${currentPage === totalPages ? 'disabled' : ''}>
        ▶
    </button>`;

    // Nút về trang cuối
    html += `
    <button onclick="loadUserList(${totalPages})"
        class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40' : ''}"
        ${currentPage === totalPages ? 'disabled' : ''}>
        ⏭
    </button>`;

    // Ô nhảy trang nhanh
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

// Tạo nút bấm cho từng trang
function pageBtn(page, current) {
    const active = page === current;
    return `
    <button onclick="loadUserList(${page})"
        class="px-3 py-1 border rounded
        ${active ? 'btn-grad text-white font-bold' : 'hover:bg-gray-100'}">
        ${page}
    </button>`;
}

// Nhảy đến số trang chỉ định
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

    // Checkbox chọn tất cả
    $('#selectAll').on('change', function () {
        const isChecked = this.checked;

        $('.user-checkbox').each(function () {
            $(this).prop('checked', isChecked)
            .closest('tr')
            .toggleClass('bg-indigo-50/50', isChecked);
            updateDeleteButton();
        });
    });

    // Checkbox từng dòng
    $(document).on('change', '.user-checkbox', function () {
        const total = $('.user-checkbox').length;
        const checked = $('.user-checkbox:checked').length;

        $(this).closest('tr').toggleClass('bg-indigo-50/50', this.checked); $('#selectAll').prop('checked', total > 0 && total === checked);
        updateDeleteButton();
    });

    // Xóa nhiều người dùng đã chọn
    $(document).on('click', '#btnDeleteSelected', function () {
        const ids = $('.user-checkbox:checked')
            .map(function () {
                return parseInt(this.value);
            })
            .get();

        if (ids.length === 0) {
            alert('Please select at least one user!');
            return;
        }

        if (!confirm(`Delete ${ids.length} user(s)? This action cannot be undone.`)) return;

        $.ajax({
            url: '/Admin/tblUsers/DeleteMultiple',
            type: 'POST',
            traditional: true,
            data: { ids: ids },
            success: function (res) {
                if (res.blocked && res.blocked.length > 0) {
                    alert(
                        `Cannot delete this ID: ${res.blocked.join(', ')}\n` +
                        `Because it was used to link foreign keys.`
                    );
                }
                if (res.deleted && res.deleted.length > 0) {
                    alert(`Deleted ${res.deleted.length} user(s) successfully.`);
                }
                const savedPage = localStorage.getItem('userPage');
                currentPage = savedPage ? parseInt(savedPage) : 1;
                loadUserList(currentPage);
                $('#selectAll').prop('checked', false);
            }
        });
    });
});