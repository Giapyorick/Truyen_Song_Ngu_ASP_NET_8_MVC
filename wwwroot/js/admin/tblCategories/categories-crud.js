/* categories-crud.js - Xử lý API, Bộ lọc, CRUD, Phân trang, Xóa nhiều, Import và Export */

let currentPage = 1;
const pageSize = 5;

// Tải danh sách thể loại từ server qua AJAX
function loadCategoryList(page = 1) {
    currentPage = page;
    const $body = $('#user-list-body');
    const statusVal = $('#filterStatus').val();
    const filters = {
        search: $('#filterSearch').val(),
        status: statusVal === 'all' ? '' : statusVal,
        page: currentPage,
        pageSize: pageSize
    };

    $.ajax({
        url: '/Admin/tblCategories/List',
        type: 'GET',
        data: filters,
        success: function (data) {
            let html = '';
            console.log("Data received from the Server:", data);
            if (!data.categories || data.categories.length === 0) {
                $body.html('<tr><td colspan="5" class="text-center py-10 text-gray-500">Not found any results.</td></tr>');
                $('#pagination-container').html('');
                return;
            }

            data.categories.forEach(category => {
                const statusActive = category.status === "Active";
                const statusClass = statusActive ? 'active' : 'inactive';

                html += `
                <tr class="group hover:bg-indigo-50/30 transition-all">
                    <td class="px-6 py-5 text-center">
                        <input type="checkbox" class="user-checkbox w-5 h-5 rounded-md border-gray-300" value="${category.categoryId}" data-id="${category.categoryId}">
                    </td>
                    <td class="px-4 py-5">
                        <div class="flex items-center gap-4">
                            <div class="font-bold text-gray-700">${category.name}</div>
                        </div>
                    </td>
                    <td class="px-6 py-5">
                        <span class="px-3 py-1.5 bg-slate-100 text-gray-600 rounded-lg text-xs font-semibold">
                            ${category.description || ''}
                        </span>
                    </td>
                    <td class="px-6 py-5 text-center">
                        <span class="font-bold px-3 py-1.5 ${statusClass} rounded-xl text-[10px] uppercase tracking-wider">
                            ${category.status}
                        </span>
                    </td>
                    <td class="px-8 py-5 text-right">
                        <div class="flex justify-end gap-3">
                            <button onclick="openModal('edit', ${category.categoryId})"
                                class="w-7 aspect-square p-0
                                    flex items-center justify-center
                                    rounded-lg btn-grad bg-blue-50
                                    hover:bg-blue-600 hover:text-white
                                    transition-all duration-200">
                                <i class="fas fa-pencil-alt text-[11px]"></i>
                            </button>

                            <button onclick="deleteCategory(${category.categoryId})"
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
            $body.html('<tr><td colspan="5" class="text-center py-10 text-red-500">Error loading data.</td></tr>');
        }
    });
}

// Xóa một thể loại đơn lẻ
async function deleteCategory(id) {
    const confirmed = await customConfirm(
        `Are you sure to remove this category?<br><small class="text-red-400">This action cannot be undone.</small>`,
        "Delete Category"
    );
    if (confirmed) {
        const $row = $(`button[onclick="deleteCategory(${id})"]`).closest('tr');
        $row.addClass('opacity-50 pointer-events-none');

        $.ajax({
            url: '/Admin/tblCategories/Delete',
            type: 'POST',
            data: { id: id },
            success: function (response) {
                if (response.success) {
                    $row.fadeOut(400, function () {
                        $(this).remove();
                        showToast(response.message, 'success');
                    });
                } else {
                    showToast("Error: " + response.message, 'error');
                    $row.removeClass('opacity-50 pointer-events-none');
                }
            },
            error: function () {
                showToast("Cannot connect to the server to delete.", 'error');
                $row.removeClass('opacity-50 pointer-events-none');
            }
        });
    }
}

// Xuất file Excel
function exportExcel() {
    const search = $('#filterSearch').val();
    const status = $('#filterStatus').val();
    window.location.href = `/Admin/tblCategories/ExportToExcel?search=${search}&status=${status}`;
}

// Thực thi Import file Excel
function executeImport() {
    const fileInput = document.getElementById('excelFile');
    if (fileInput.files.length === 0) {
        showToast('Please choose an Excel file!', 'error');
        return;
    }

    const formData = new FormData();
    formData.append('file', fileInput.files[0]);

    const $btn = $('#btnDoImport');
    $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> Processing...');

    $.ajax({
        url: '/Admin/tblCategories/ImportExcel',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            if (res.success) {
                showToast(res.message, 'success');
                closeImportModal();
                loadCategoryList(1);
            } else {
                showToast(res.message, 'error');
            }
        },
        error: function () {
            showToast('Error during importing file!', 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).text('Confirm Import');
        }
    });
}

// Xử lý gửi Form Thêm / Cập nhật
$('#userForm').on('submit', function (e) {
    e.preventDefault();

    const submitBtn = $(this).find('button[type="submit"]');
    const formData = new FormData(this);
    const categoryId = $('#categoryId').val();
    const url = (categoryId == "0" || categoryId == "") ? '/Admin/tblCategories/Add' : '/Admin/tblCategories/Update';

    submitBtn.prop('disabled', true).html('<i class="fas fa-spinner animate-spin"></i> Processing...');

    $.ajax({
        url: url,
        type: 'POST',
        data: formData,
        contentType: false,
        processData: false,
        success: function (response) {
            if (response.success) {
                showToast(response.message, 'success');
                closeModal();
                loadCategoryList();
            } else {
                showToast("Error: " + response.message, 'error');
            }
        },
        error: function () {
            showToast("Cannot connect to the server.", 'error');
        },
        complete: function () {
            submitBtn.prop('disabled', false).html('Save Changes');
        }
    });
});

// Render thanh phân trang
function renderPagination(currentPage, totalPages) {
    const container = $('#pagination-container');
    container.empty();

    if (totalPages <= 1) return;

    const maxVisible = 2; // Số trang hiển thị hai bên trang hiện tại
    let html = `<div class="flex items-center gap-1">`;

    // Nút về trang đầu tiên
    html += `
    <button onclick="loadCategoryList(1)"
        class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40' : ''}"
        ${currentPage === 1 ? 'disabled' : ''}>
        ⏮
    </button>`;

    // Nút lùi về trang trước
    html += `
    <button onclick="loadCategoryList(${currentPage - 1})"
        class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40' : ''}"
        ${currentPage === 1 ? 'disabled' : ''}>
        ◀
    </button>`;

    // Trang 1 và dấu ba chấm bên trái nếu ở xa
    if (currentPage > maxVisible + 1) {
        html += pageBtn(1, currentPage);
        html += `<span class="px-2">…</span>`;
    }

    // Các trang xung quanh trang hiện tại
    for (let i = Math.max(1, currentPage - maxVisible);
        i <= Math.min(totalPages, currentPage + maxVisible);
        i++) {
        html += pageBtn(i, currentPage);
    }

    // Dấu ba chấm và trang cuối cùng bên phải
    if (currentPage < totalPages - maxVisible) {
        html += `<span class="px-2">…</span>`;
        html += pageBtn(totalPages, currentPage);
    }

    // Nút sang trang kế tiếp
    html += `
    <button onclick="loadCategoryList(${currentPage + 1})"
        class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40' : ''}"
        ${currentPage === totalPages ? 'disabled' : ''}>
        ▶
    </button>`;

    // Nút đến trang cuối cùng
    html += `
    <button onclick="loadCategoryList(${totalPages})"
        class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40' : ''}"
        ${currentPage === totalPages ? 'disabled' : ''}>
        ⏭
    </button>`;

    // Ô nhập số trang nhảy nhanh
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
    <button onclick="loadCategoryList(${page})"
        class="px-3 py-1 border rounded
        ${active ? 'btn-grad text-white font-bold' : 'hover:bg-gray-100'}">
        ${page}
    </button>`;
}

// Nhảy đến trang được nhập
function gotoPage(input, totalPages) {
    let page = parseInt(input.value);
    if (isNaN(page)) return;

    if (page < 1) page = 1;
    if (page > totalPages) page = totalPages;

    loadCategoryList(page);
}

// Quản lý sự kiện Ready, Tìm kiếm và Xóa nhiều phần tử
$(document).ready(function () {
    loadCategoryList();

    // Tìm kiếm với sự kiện gõ phím
    $('#filterSearch').on('keyup', function () {
        loadCategoryList(1);
    });

    // Lọc theo trạng thái
    $('#filterStatus').on('change', function () {
        loadCategoryList(1);
    });

    // Chọn / Bỏ chọn toàn bộ Checkbox
    $('#selectAll').on('change', function () {
        const isChecked = this.checked;

        $('.user-checkbox').each(function () {
            $(this).prop('checked', isChecked)
            .closest('tr')
            .toggleClass('bg-indigo-50/50', isChecked);
            updateDeleteButton();
        });
    });

    // Chọn Checkbox từng dòng
    $(document).on('change', '.user-checkbox', function () {
        const total = $('.user-checkbox').length;
        const checked = $('.user-checkbox:checked').length;

        $(this).closest('tr')
            .toggleClass('bg-indigo-50/50', this.checked);

        $('#selectAll').prop('checked', total > 0 && total === checked);
        updateDeleteButton();
    });

    // Xóa nhiều thể loại đã chọn
    $(document).on('click', '#btnDeleteSelected', async function () {
        const ids = $('.user-checkbox:checked')
            .map(function () {
                return parseInt(this.value);
            })
            .get();

        if (ids.length === 0) {
            showToast('Please select at least one category!', 'error');
            return;
        }

        const confirmed = await customConfirm(
            `Are you sure you want to delete <strong>${ids.length}</strong> categories?<br>` +
            `<small class="text-red-400">This action cannot be undone.</small>`,
            "Delete Multiple"
        );

        if (!confirmed) return;

        $.ajax({
            url: '/Admin/tblCategories/DeleteMultiple',
            type: 'POST',
            traditional: true,
            data: { ids: ids },
            success: function (res) {
                if (res.blocked && res.blocked.length > 0) {
                    showToast(
                        `Cannot delete ID(s): <strong>${res.blocked.join(', ')}</strong>.<br>` +
                        `Because they are currently linked to existing foreign keys.`,
                        'error'
                    );
                }
                if (res.deleted && res.deleted.length > 0) {
                    showToast(`Deleted ${res.deleted.length} category(ies) successfully.`, 'success');
                }
                loadCategoryList();
                $('#selectAll').prop('checked', false);
            }
        });
    });
});