/* chapters-crud.js - Xử lý API, Bảng dữ liệu, Lọc, CRUD form, Xóa nhiều & Import/Export Excel */

const L = window.ADMIN_LANG || {};

let savedChapterPage = localStorage.getItem('chapterPage');
let currentPage = savedChapterPage ? parseInt(savedChapterPage) : 1;
const pageSize = 5;

// Mở modal Thêm hoặc Sửa chapter
function openModal(mode, id = null) {
    const modal = $('#modalOverlay');
    const $imgPreview = $('#imgPreview');
    const $uploadIcon = $('#uploadIcon');
    $('#chapterForm')[0].reset();

    $imgPreview.addClass('hidden').attr('src', ''); $uploadIcon.removeClass('hidden');

    if (mode === 'add') {
        $('#modalTitle').text(L.TitleAddChapter || 'Add chapter');
        $('#chapterId').val('0');
        $('#chapterStoryId').val('Unknow').trigger('change.select2');
    } else {
        $('#modalTitle').text(L.TitleEditChapter || 'Update chapter');

        $.get('/Admin/tblChapters/GetById/' + id, function (data) {
            console.log("data received:", data);
            $('#chapterId').val(data.chapterId);
            $('#chapterTitle').val(data.title);
            $('#chapterStoryId')
                .val(data.storyId)
                .trigger('change.select2');
            $('#chapterChapterNumber').val(data.chapterNumber || data.country);
        });
    }

    modal.removeClass('hidden').addClass('flex');
    setTimeout(() => $('#modalContent').addClass('translate-y-0 opacity-100'), 10);
}

// Đóng modal Thêm / Sửa
function closeModal() {
    const content = $('#modalContent');
    content.removeClass('translate-y-0 opacity-100 scale-100')
        .addClass('translate-y-10 opacity-0 scale-95');

    setTimeout(() => {
        $('#modalOverlay').removeClass('flex').addClass('hidden');
    }, 300);
}

// Xóa 1 chapter đơn lẻ
async function deleteChapter(id) {
    const actionText = L.ActionCannotUndo || 'This action cannot be undone.';
    const confirmMsg = (L.ConfirmDeleteSingleChapterMsg || 'Are you sure to remove this chapter?') + `<br><small class="text-red-400">${actionText}</small>`;
    const confirmTitle = L.ConfirmDeleteSingleChapterTitle || "Delete Chapter";

    const confirmed = await customConfirm(confirmMsg, confirmTitle);
    if (confirmed) {
        const $row = $(`button[onclick="deleteChapter(${id})"]`).closest('tr');
        $row.addClass('opacity-50 pointer-events-none');

        $.ajax({
            url: '/Admin/tblChapters/Delete',
            type: 'POST',
            data: { id: id },
            success: function (response) {
                if (response.success) {
                    showToast(response.message, 'success');

                    const remainingRows = $('#user-list-body tr').length - 1;
                    if (remainingRows <= 0 && currentPage > 1) {
                        currentPage--;
                    }

                    loadChapterList(currentPage);
                } else {
                    showToast("Error: " + response.message, 'error');
                    $row.removeClass('opacity-50 pointer-events-none');
                }
            },
            error: function () {
                showToast(L.ErrConnectDelete || "Cannot connect to the server to delete.", 'error');
                $row.removeClass('opacity-50 pointer-events-none');
            }
        });
    }
}

// Xuất danh sách chapter ra file Excel
function exportExcel() {
    const search = $('#filterSearch').val();
    const storyId = $('#filterStory').val();

    window.location.href = `/Admin/tblChapters/ExportToExcel?search=${search}&storyId=${storyId}`;
}

// Submit form Thêm / Cập nhật chapter
$('#chapterForm').on('submit', function (e) {
    e.preventDefault();

    localStorage.setItem('chapterPage', currentPage);

    const submitBtn = $(this).find('button[type="submit"]');
    const formData = new FormData(this);

    const chapterId = $('#chapterId').val();
    const isAdding = (chapterId == "0" || chapterId == "" || !chapterId);
    const url = isAdding ? '/Admin/tblChapters/Add' : '/Admin/tblChapters/Update';

    submitBtn.prop('disabled', true).html(`<i class="fas fa-spinner animate-spin"></i> ${L.Processing || 'Processing...'}`);

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

                if (isAdding) {
                    loadChapterList(1);
                } else {
                    const savedPage = localStorage.getItem('chapterPage');
                    const targetPage = savedPage ? parseInt(savedPage) : currentPage;
                    loadChapterList(targetPage);
                }
            } else {
                showToast("Error: " + response.message, 'error');
            }
        },
        error: function () {
            showToast(L.ErrConnectServer || "Cannot connect to the server.", 'error');
        },
        complete: function () {
            submitBtn.prop('disabled', false).html(L.SaveChanges || 'Save Changes');
        }
    });
});

// Tải danh sách chapter qua AJAX
function loadChapterList(page = null) {
    if (page !== null && page !== undefined) {
        currentPage = parseInt(page);
    }

    localStorage.setItem('chapterPage', currentPage);

    const $body = $('#user-list-body');
    const storyVal = $('#filterStory').val();
    const filters = {
        search: $('#filterSearch').val(),
        storyId: storyVal === 'all' ? '' : storyVal,
        page: currentPage,
        pageSize: pageSize
    };

    $.ajax({
        url: '/Admin/tblChapters/List',
        type: 'GET',
        data: filters,
        success: function (data) {
            let html = '';
            if (!data.chapters || data.chapters.length === 0) {
                if (currentPage > 1) {
                    loadChapterList(currentPage - 1);
                    return;
                }
                const notFound = L.NotFoundChapters || 'Not found any results.';
                $body.html(`<tr><td colspan="6" class="text-center py-10 text-gray-500">${notFound}</td></tr>`);
                $('#pagination-container').html('');
                return;
            }

            data.chapters.forEach(chapter => {
                html += `
                <tr class="group hover:bg-indigo-50/30 transition-all">
                    <td class="px-6 py-5 text-left">
                        <input type="checkbox" class="user-checkbox w-5 h-5 rounded-md border-gray-300" value="${chapter.chapterId}" data-id="${chapter.chapterId}">
                    </td>
                    <td class="px-4 py-5 text-left">
                        <div class="flex items-center gap-4">
                            <div>
                                <div class="font-bold text-gray-700">${chapter.title}</div>
                                <div class="text-xs text-gray-400">${chapter.storyTitle || ''}</div>
                            </div>
                        </div>
                    </td>
                    <td class="px-6 py-5 text-left">
                        <span class="px-3 py-1.5 bg-slate-100 text-gray-600 rounded-lg text-xs font-semibold">
                            ${chapter.createDate || ''}
                        </span>
                    </td>

                    <td class="px-6 py-5 text-left">
                        <span class="font-bold px-3 py-1.5 rounded-xl text-[10px] uppercase tracking-wider">
                            ${chapter.chapterNumber}
                        </span>
                    </td>
                    <td class="px-8 py-5 text-right">
                        <div class="flex justify-end gap-3">
                            <button onclick="openModal('edit', ${chapter.chapterId})"
                                class="w-7 aspect-square p-0
                                    flex items-center justify-center
                                    rounded-lg btn-grad bg-blue-50
                                    hover:bg-blue-600 hover:text-white
                                    transition-all duration-200" title="${L.TitleEditAccount || 'Edit'}">
                                <i class="fas fa-pencil-alt text-[11px]"></i>
                            </button>

                            <button onclick="deleteChapter(${chapter.chapterId})"
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
        error: function (xhr) {
            const errLoad = L.ErrLoadChapters || 'Error: Input data.';
            $body.html(`<tr><td colspan="6" class="text-center py-10 text-red-500">${errLoad}</td></tr>`);
        }
    });
}

// Xử lý Import Excel
function executeImport() {
    const fileInput = document.getElementById('excelFile');
    if (fileInput.files.length === 0) {
        showToast(L.ChooseExcelFile || 'Please choose file Excel!', 'error');
        return;
    }

    const formData = new FormData();
    formData.append('file', fileInput.files[0]);

    const $btn = $('#btnDoImport');
    $btn.prop('disabled', true).html(`<i class="fas fa-spinner fa-spin"></i> ${L.Processing || 'Loading...'}`);

    $.ajax({
        url: '/Admin/tblChapters/ImportToExcel',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            if (res.success) {
                showToast(res.message, 'success');
                closeImportModal();
                loadChapterList(1);
            } else {
                showToast(res.message, 'error');
            }
        },
        error: function () {
            showToast(L.ErrImport || 'Error during import file!', 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).text(L.ConfirmImport || 'Confirm Import');
        }
    });
}

// Render các nút phân trang
function renderPagination(currentPage, totalPages) {
    const container = $('#pagination-container');
    container.empty();

    if (totalPages <= 1) return;

    const maxVisible = 2;
    let html = `<div class="flex items-center gap-1">`;

    html += `
    <button onclick="loadChapterList(1)"
        class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40' : ''}"
        ${currentPage === 1 ? 'disabled' : ''}>
        ⏮
    </button>`;

    html += `
    <button onclick="loadChapterList(${currentPage - 1})"
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
    <button onclick="loadChapterList(${currentPage + 1})"
        class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40' : ''}"
        ${currentPage === totalPages ? 'disabled' : ''}>
        ▶
    </button>`;

    html += `
    <button onclick="loadChapterList(${totalPages})"
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
    <button onclick="loadChapterList(${page})"
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

    loadChapterList(page);
}

// Lắng nghe sự kiện trang tải, Lọc và Xóa nhiều chương truyện
$(document).ready(function () {
    const savedPage = localStorage.getItem('chapterPage');
    currentPage = savedPage ? parseInt(savedPage) : 1;
    loadChapterList(currentPage);

    $('#filterSearch').on('keyup', function () {
        loadChapterList(1);
    });

    $('#filterStory').on('change', function () {
        loadChapterList(1);
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

    // Xóa nhiều
    $(document).on('click', '#btnDeleteSelected', async function () {
        const ids = $('.user-checkbox:checked')
            .map(function () {
                return parseInt(this.value);
            })
            .get();

        if (ids.length === 0) {
            showToast(L.SelectAtLeastOneChapter || 'Please select at least one chapter!', 'error');
            return;
        }

        const actionText = L.ActionCannotUndo || 'This action cannot be undone.';
        const multiMsg = (L.ConfirmDeleteMultiChaptersMsg || 'Are you sure you want to delete <strong>{0}</strong> chapters?')
            .replace('{0}', ids.length) + `<br><small class="text-red-400">${actionText}</small>`;
        const multiTitle = L.ConfirmDeleteMultiChaptersTitle || "Delete Multiple";

        const confirmed = await customConfirm(multiMsg, multiTitle);
        if (!confirmed) return;

        $.ajax({
            url: '/Admin/tblChapters/DeleteMultiple',
            type: 'POST',
            traditional: true,
            data: { ids: ids },
            success: function (res) {
                if (res.blocked && res.blocked.length > 0) {
                    const blockedTemplate = L.BlockedDeleteChapter || 'Cannot delete this ID: {0}\nBecause it was used to link foreign keys.';
                    showToast(blockedTemplate.replace('{0}', res.blocked.join(', ')), 'error');
                }
                if (res.deleted && res.deleted.length > 0) {
                    const deletedTemplate = L.DeletedChaptersSuccess || 'Deleted {0} chapter(s) successfully.';
                    showToast(deletedTemplate.replace('{0}', res.deleted.length), 'success');
                }

                const totalOnPage = $('.user-checkbox').length;
                if (ids.length >= totalOnPage && currentPage > 1) {
                    currentPage--;
                }

                loadChapterList(currentPage);
                $('#selectAll').prop('checked', false);
            }
        });
    });
});