/* authors-crud.js - Xử lý API, Filter, CRUD & Pagination */

const L = window.ADMIN_LANG || {};

let currentPage = 1;
const pageSize = 5;
let isUpdating = false;
let editingPage = null;

$(document).ready(function () {
    const savedPageAuthor = localStorage.getItem('authorPage');
    currentPage = savedPageAuthor ? parseInt(savedPageAuthor) : 1;
    loadAuthorList(currentPage);

    // Tìm kiếm debounce
    let searchTimeout;
    $('#filterSearch').on('keyup', function () {
        if (isUpdating) return;
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(() => loadAuthorList(1), 350);
    });

    // Lọc theo giới tính và trạng thái
    $('#filterGender, #filterStatus').on('change', function (e) {
        if (isUpdating) return;

        if (
            e.originalEvent ||
            $(this).is(':focus') || $(e.target).closest('#modalOverlay').length === 0
        ) {
        localStorage.setItem('authorPage', 1);
        loadAuthorList(1);
    }
});
});

// Submit form Thêm / Sửa Tác giả
$('#authorForm').on('submit', function (e) {
    e.preventDefault();
    localStorage.setItem('authorPage', currentPage);
    isUpdating = true;

    const submitBtn = $(this).find('button[type="submit"]');
    const formData = new FormData(this);
    const authorId = $('#authorId').val();
    const isAddAction = (!authorId || authorId === "0");
    const url = isAddAction ? '/Admin/tblAuthors/Add' : '/Admin/tblAuthors/Update';

    submitBtn.prop('disabled', true).html(`<i class="fas fa-spinner animate-spin"></i> ${L.Processing || 'Processing...'}`);

    $.ajax({
        url: url,
        type: 'POST',
        data: formData,
        contentType: false,
        processData: false,
        success: function (res) {
            if (res.success) {
                showToast(res.message, 'success');
                closeModal();
                currentPage = isAddAction ? 1 : (editingPage !== null ? editingPage : currentPage);
                localStorage.setItem('authorPage', currentPage);
                editingPage = null;
                loadAuthorList(currentPage);
            } else {
                showToast(res.message || "An error occurred while saving author.", 'error');
                isUpdating = false;
            }
        },
        error: function () {
            showToast(L.ErrConnectServer || "Cannot connect to the server.", 'error');
            isUpdating = false;
        },
        complete: function () {
            submitBtn.prop('disabled', false).html(L.SaveChanges || 'Save Changes');
        }
    });
});

// Checkbox chọn tất cả các dòng
$('#selectAll').on('change', function () {
    const isChecked = this.checked;
    $('.user-checkbox').each(function () {
        $(this).prop('checked', isChecked).closest('tr').toggleClass('bg-indigo-50/50', isChecked);
        updateDeleteButton();
    });
});

// Checkbox từng dòng tác giả
$(document).on('change', '.user-checkbox', function () {
    const total = $('.user-checkbox').length;
    const checked = $('.user-checkbox:checked').length;
    $(this).closest('tr').toggleClass('bg-indigo-50/50', this.checked); $('#selectAll').prop('checked', total > 0 && total === checked);
    updateDeleteButton();
});

// Xóa nhiều tác giả đã chọn
$('#btnDeleteSelected').on('click', async function () {
    const ids = $('.user-checkbox:checked').map(function () { return parseInt(this.value); }).get();
    if (ids.length === 0) {
        showToast(L.SelectAtLeastOneAuthor || 'Please select at least one author!', 'error');
        return;
    }

    const actionText = L.ActionCannotUndo || 'This action cannot be undone.';
    const confirmMsg = (L.ConfirmDeleteMultiAuthors || 'Are you sure you want to delete <strong>{0}</strong> authors?')
        .replace('{0}', ids.length) + `<br><small class="text-red-400">${actionText}</small>`;
    const confirmTitle = L.ConfirmDeleteMultiAuthorsTitle || "Delete Multiple Authors";

    const confirmed = await customConfirm(confirmMsg, confirmTitle);
    if (!confirmed) return;

    $.ajax({
        url: '/Admin/tblAuthors/DeleteMultiple',
        type: 'POST',
        traditional: true,
        data: { ids: ids },
        success: function (res) {
            if (res.blocked && res.blocked.length > 0) {
                const blockedTemplate = L.BlockedDeleteAuthor || 'Cannot delete ID(s): <strong>{0}</strong> (Linked to existing stories).';
                showToast(blockedTemplate.replace('{0}', res.blocked.join(', ')), 'error');
            }
            if (res.deleted && res.deleted.length > 0) {
                const deletedTemplate = L.DeletedAuthorsSuccess || 'Successfully deleted {0} author(s).';
                showToast(deletedTemplate.replace('{0}', res.deleted.length), 'success');
            }
            loadAuthorList(currentPage);
            $('#selectAll').prop('checked', false);
        },
        error: function () {
            showToast(L.ErrConnectDelete || 'Cannot connect to server to delete authors.', 'error');
        }
    });
});

// Kéo thả hoặc thay đổi tệp Excel
$('#excelFile').on('change', function (e) {
    const file = e.target.files[0];
    if (file) {
        $('#fileStatus').html(`<span class="text-indigo-600 font-bold italic">${file.name}</span>`);
        $('#dropZone').addClass('border-indigo-500 bg-indigo-100/50');
    }
});

// Load danh sách Author qua AJAX
function loadAuthorList(page = 1) {
    currentPage = page;
    const $body = $('#user-list-body');
    const genderVal = $('#filterGender').val();
    const statusVal = $('#filterStatus').val();

    $.ajax({
        url: '/Admin/tblAuthors/List',
        type: 'GET',
        data: {
            search: $('#filterSearch').val(),
            gender: genderVal === 'all' ? '' : genderVal,
            status: statusVal === 'all' ? '' : statusVal,
            page: currentPage,
            pageSize: pageSize
        },
        success: function (data) {
            if (!data.authors || data.authors.length === 0) {
                const notFound = L.NotFoundAuthors || 'No authors found matching criteria.';
                $body.html(`<tr><td colspan="6" class="text-center py-10 text-gray-500">${notFound}</td></tr>`);
                $('#pagination-container').html('');
                return;
            }

            let html = '';
            data.authors.forEach(author => {
                const avatarHtml = author.img
                    ? `<img onclick="openImagePreview(this.src)" src="/${author.img}" class="w-11 h-11 rounded-2xl object-cover shadow-sm border border-gray-100 cursor-pointer">`
                    : `<div class="w-11 h-11 rounded-2xl bg-gradient-to-tr from-purple-500 to-indigo-600 flex items-center justify-center text-white font-bold shadow-sm">${author.authorName ? author.authorName.charAt(0).toUpperCase() : 'A'}</div>`;

                const statusText = author.status === 'Active' ? (L.StatusActive || 'Active') : (L.StatusInactive || 'Inactive');

                // Xác định giới tính động
                const isMale = (author.gender || '').toLowerCase() === 'male';
                const genderClass = isMale ? 'male' : 'female';
                const genderText = isMale
                    ? (L.GenderMale || 'Male')
                    : (L.GenderFemale || 'Female');

                html += `
                <tr class="group hover:bg-indigo-50/30 transition-all">
                    <td class="px-6 py-5 text-center">
                        <input type="checkbox" class="user-checkbox w-5 h-5 rounded-md border-gray-300" value="${author.authorId}">
                    </td>
                    <td class="px-4 py-5">
                        <div class="flex items-center gap-4">
                            ${avatarHtml}
                            <div>
                                <div class="font-bold text-gray-700">${author.authorName}</div>
                                <div class="text-xs text-gray-400">${author.email || ''}</div>
                                <div class="text-xs text-gray-400">${author.country || ''}</div>
                            </div>
                        </div>
                    </td>
                    <td class="px-6 py-5">
                        <span class="px-3 py-1.5 bg-slate-100 text-gray-600 rounded-lg text-xs font-semibold">${author.doB || ''}</span>
                    </td>
                    <td class="px-6 py-5 text-center">
                        <span class="font-bold px-3 py-1.5 ${genderClass} rounded-xl text-[10px] uppercase tracking-wider">
                            ${genderText}
                        </span>
                    </td>
                    <td class="px-6 py-5 text-center">
                        <span class="font-bold px-3 py-1.5 ${author.status === 'Active' ? 'active' : 'inactive'} rounded-xl text-[10px] uppercase tracking-wider">
                            ${statusText}
                        </span>
                    </td>
                    <td class="px-8 py-5 text-right">
                        <div class="flex justify-end gap-3">
                            <button onclick="openModal('edit', ${author.authorId})" class="w-7 aspect-square p-0 flex items-center justify-center rounded-lg btn-grad bg-blue-50 hover:bg-blue-600 hover:text-white transition-all duration-200" title="${L.TitleEditAuthor || 'Edit Author'}">
                                <i class="fas fa-pencil-alt text-[11px]"></i>
                            </button>
                            <button onclick="deleteAuthor(${author.authorId})" class="w-7 aspect-square p-0 flex items-center justify-center rounded-lg btn-grad-cancel bg-red-50 hover:bg-red-600 hover:text-white transition-all duration-200" title="${L.TitleDeleteAuthor || 'Delete Author'}">
                                <i class="fas fa-trash-alt text-[11px]"></i>
                            </button>
                        </div>
                    </td>
                </tr>`;
            });

            $body.html(html);
            renderPagination(data.currentPage, data.totalPages);
            updateDeleteButton();
            isUpdating = false;
        },
        error: function () {
            const errLoad = L.ErrLoadAuthors || 'Error loading author data.';
            $body.html(`<tr><td colspan="6" class="text-center py-10 text-red-500">${errLoad}</td></tr>`);
            isUpdating = false;
        }
    });
}

// Mở Modal Thêm mới / Cập nhật tác giả
function openModal(mode, id = null) {
    const modal = $('#modalOverlay');
    const $imgPreview = $('#imgPreview');
    const $uploadIcon = $('#uploadIcon');
    $('#authorForm')[0].reset();

    $imgPreview.addClass('hidden').attr('src', ''); $uploadIcon.removeClass('hidden');

    if (mode === 'add') {
        $('#modalTitle').text(L.TitleAddAuthor || 'Add New Author');
        $('#authorId').val('0');
    } else {
        $('#modalTitle').text(L.TitleEditAuthor || 'Update Author');
        editingPage = currentPage;

        $.get('/Admin/tblAuthors/GetById/' + id, function (data) {
            $('#authorId').val(data.authorId);
            $('#authorName').val(data.authorName);
            $('#authorEmail').val(data.email);
            $('#authorCountry').val(data.country).trigger('change');
            $('#authorDoB').val(data.doB);
            $('#authorGender').val(data.gender).trigger('change.select2');
            $('#authorStatus').val(data.status).trigger('change.select2');

            if (data.img && data.img.trim() !== "") {
                const fullPath = data.img.startsWith('/') ? data.img : '/' + data.img;
                $imgPreview.attr('src', fullPath).removeClass('hidden'); $uploadIcon.addClass('hidden');
            }
        });
    }

    modal.removeClass('hidden').addClass('flex');
    $('.select2-custom').trigger('change');
    setTimeout(() => $('#modalContent').addClass('translate-y-0 opacity-100'), 10);
}

function closeModal() {
    $('#modalContent').removeClass('translate-y-0 opacity-100 scale-100').addClass('translate-y-10 opacity-0 scale-95');
    setTimeout(() => $('#modalOverlay').removeClass('flex').addClass('hidden'), 300);
}

// Xóa 1 tác giả
async function deleteAuthor(id) {
    const actionText = L.ActionCannotUndo || 'This action cannot be undone.';
    const confirmMsg = (L.ConfirmDeleteSingleAuthor || 'Are you sure you want to remove this author?') + `<br><small class="text-red-400">${actionText}</small>`;
    const confirmTitle = L.ConfirmDeleteSingleAuthorTitle || "Delete Author";

    const confirmed = await customConfirm(confirmMsg, confirmTitle);
    if (!confirmed) return;

    const $row = $(`button[onclick="deleteAuthor(${id})"]`).closest('tr');
    $row.addClass('opacity-50 pointer-events-none');

    $.ajax({
        url: '/Admin/tblAuthors/Delete',
        type: 'POST',
        data: { id: id },
        success: function (res) {
            if (res.success) {
                $row.fadeOut(400, function () {
                    $(this).remove();
                    showToast(res.message, 'success');
                });
            } else {
                showToast(res.message || "Failed to delete author.", 'error');
                $row.removeClass('opacity-50 pointer-events-none');
            }
        },
        error: function () {
            showToast(L.ErrConnectServer || "Cannot connect to server.", 'error');
            $row.removeClass('opacity-50 pointer-events-none');
        }
    });
}

// Lắng nghe sự kiện Kéo & Thả file Import Excel
$(document).ready(function () {
    const dropZone = document.getElementById('dropZone');
    const fileInput = document.getElementById('excelFile');
    const fileStatus = document.getElementById('fileStatus');

    if (dropZone) {
        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
            dropZone.addEventListener(eventName, e => {
                e.preventDefault();
                e.stopPropagation();
            }, false);
        });

        ['dragenter', 'dragover'].forEach(eventName => {
            dropZone.addEventListener(eventName, () => dropZone.classList.add('border-indigo-500', 'bg-indigo-100/50'), false);
        });

        ['dragleave', 'drop'].forEach(eventName => {
            dropZone.addEventListener(eventName, () => dropZone.classList.remove('border-indigo-500', 'bg-indigo-100/50'), false);
        });

        dropZone.addEventListener('drop', e => {
            const dt = e.dataTransfer;

            // Kéo thả file trực tiếp từ máy tính
            if (dt.files && dt.files.length > 0) {
                fileInput.files = dt.files;
                $(dropZone).attr('data-selected-filename', '');
                if (fileStatus) fileStatus.innerHTML = `<span class="text-indigo-600 font-bold italic">${dt.files[0].name}</span>`;
            }
            // Kéo thả file từ Kho lưu trữ (FileManager)
            else {
                const fileName = dt.getData('text/plain') || dt.getData('fileName');
                if (fileName) {
                    fileInput.value = '';
                    $(dropZone).attr('data-selected-filename', fileName);
                    const fileSelectedMsg = L.FileSelectedFromStorage || 'Selected file: {0} (From storage)';
                    if (fileStatus) fileStatus.innerHTML = `<span class="text-indigo-600 font-bold italic">📄 ${decodeURIComponent(fileName)} (Storage)</span>`;
                    if (typeof showToast === 'function') showToast(fileSelectedMsg.replace('{0}', decodeURIComponent(fileName)), 'info');
                }
            }
        });
    }
});

// Thực thi Import Excel
function executeImport() {
    const fileInput = document.getElementById('excelFile');
    const dropZone = document.getElementById('dropZone');

    let savedFileName = $(dropZone) ? $(dropZone).attr('data-selected-filename') : '';
    if (savedFileName) {
        savedFileName = decodeURIComponent(savedFileName);
    }

    const importUrl = $(dropZone).attr('data-import-url') || '/Admin/tblAuthors/ImportExcel';
    const file = fileInput && fileInput.files && fileInput.files.length > 0 ? fileInput.files[0] : null;

    if (!file && !savedFileName) {
        showToast(L.ChooseExcelFile || 'Please select an Excel file to import!', 'error');
        return;
    }

    const formData = new FormData();

    if (file) {
        formData.append('file', file);
    } else if (savedFileName) {
        formData.append('serverFileName', savedFileName);
    }

    const $btn = $('#btnDoImport').prop('disabled', true).html(`<i class="fas fa-spinner fa-spin"></i> ${L.Processing || 'Processing...'}`);

    $.ajax({
        url: importUrl,
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            if (res.success) {
                showToast(res.message, 'success');
                closeImportModal();
                loadAuthorList(1);
            } else {
                showToast(res.message, 'error');
            }
        },
        error: () => showToast(L.ErrImport || 'Error occurred during file import!', 'error'),
        complete: () => $btn.prop('disabled', false).text(L.ConfirmImport || 'Confirm Import')
    });
}

// Xuất file Excel
function exportExcel() {
    const search = $('#filterSearch').val();
    const gender = $('#filterGender').val();
    const status = $('#filterStatus').val();
    window.location.href = `/Admin/tblAuthors/ExportToExcel?search=${search}&gender=${gender}&status=${status}`;
}

// Render các nút Phân Trang
function renderPagination(curr, total) {
    const container = $('#pagination-container').empty();
    if (total <= 1) return;

    const maxVisible = 2;
    let html = `<div class="flex items-center gap-1">`;
    html += `<button onclick="loadAuthorList(1)" class="px-2 py-1 border rounded ${curr === 1 ? 'opacity-40' : ''}" ${curr === 1 ? 'disabled' : ''}>⏮</button>`;
    html += `<button onclick="loadAuthorList(${curr - 1})" class="px-2 py-1 border rounded ${curr === 1 ? 'opacity-40' : ''}" ${curr === 1 ? 'disabled' : ''}>◀</button>`;

    if (curr > maxVisible + 1) {
        html += pageBtn(1, curr) + `<span class="px-2">…</span>`;
    }

    for (let i = Math.max(1, curr - maxVisible); i <= Math.min(total, curr + maxVisible); i++) {
        html += pageBtn(i, curr);
    }

    if (curr < total - maxVisible) {
        html += `<span class="px-2">…</span>` + pageBtn(total, curr);
    }

    html += `<button onclick="loadAuthorList(${curr + 1})" class="px-2 py-1 border rounded ${curr === total ? 'opacity-40' : ''}" ${curr === total ? 'disabled' : ''}>▶</button>`;
    html += `<button onclick="loadAuthorList(${total})" class="px-2 py-1 border rounded ${curr === total ? 'opacity-40' : ''}" ${curr === total ? 'disabled' : ''}>⏭</button>`;
    html += `<div class="flex items-center gap-1 ml-3"><span class="text-sm">${L.PageGo || 'Go:'}</span><input type="number" min="1" max="${total}" value="${curr}" class="w-16 px-2 py-1 border rounded text-center" onkeydown="if(event.key==='Enter') gotoPage(this, ${total})"></div>`;
    html += `</div>`;
    container.html(html);
}

function pageBtn(p, c) {
    return `<button onclick="loadAuthorList(${p})" class="px-3 py-1 border rounded ${p === c ? 'btn-grad text-white font-bold' : 'hover:bg-gray-100'}">${p}</button>`;
}

function gotoPage(input, total) {
    let p = parseInt(input.value);
    if (!isNaN(p)) loadAuthorList(Math.max(1, Math.min(p, total)));
}