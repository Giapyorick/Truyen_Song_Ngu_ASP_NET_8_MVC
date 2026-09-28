/* stories-crud.js - Xử lý API, Bảng dữ liệu, Lọc, CRUD form, Xóa nhiều & Import/Export Excel */

let currentPage = 1;
const pageSize = 5;

// Mở modal Thêm hoặc Sửa truyện
function openModal(mode, id = null) {
    const modal = $('#modalOverlay');
    const $form = $('#storyForm');
    const $imgPreview = $('#imgPreview');
    const $uploadIcon = $('#uploadIcon');
    const $storyLang = $('#storyLang');
    const $langNotice = $('#langNotice');

    // Reset form
    $form[0].reset(); $('#storyId').val('0');
    $('input[name="CategoryIds"]').val('');
    $('#selectedCategories').html('<span class="text-gray-400">No categories selected</span>');
    $imgPreview.addClass('hidden').attr('src', ''); $uploadIcon.removeClass('hidden');

    modal.removeClass('hidden').addClass('flex');

    if (mode === 'add') {
        $('#modalTitle').text('Add Story');

        // MỞ KHÓA CHO PHÉP CHỌN NGÔN NGỮ KHI TẠO MỚI
        $storyLang.prop('disabled', false).val(['Tiếng Anh', 'Tiếng Việt']).trigger('change');
        $langNotice.text('* English & Vietnamese are default').removeClass('text-amber-600').addClass('text-teal-600');
    } else {
        $('#modalTitle').text('Update Story');

        // KHÓA LẠI KHÔNG CHO PHÉP SỬA NGÔN NGỮ KHI CẬP NHẬT
        $storyLang.prop('disabled', true); $langNotice.text('🔒 Languages cannot be modified after creation').removeClass('text-teal-600').addClass('text-amber-600 font-bold');

        $.get('/Admin/tblStories/GetById/' + id, function (data) {
            $('#storyId').val(data.storyId);
            $('#storyTitle').val(data.title);
            $('#storyPublicationDate').val(data.publicationDate);
            $('#storyAuthorName').val(data.authorId).trigger('change');
            $('#storyDescription').val(data.description);

            // Nạp giá trị ngôn ngữ hiện tại của truyện
            if (data.lang && data.lang.trim() !== "") {
                const selectedLangs = data.lang.split(',').map(s => s.trim());
                $storyLang.val(selectedLangs).trigger('change');
            } else {
                $storyLang.val(['Tiếng Anh', 'Tiếng Việt']).trigger('change');
            }

            // Nạp danh mục
            selectedCategoryIds.clear();
            const preview = document.getElementById('selectedCategories');
            preview.innerHTML = '';

            if (data.categories && data.categories.length > 0) {
                data.categories.forEach(c => {
                    selectedCategoryIds.add(String(c.categoryId));
                    preview.innerHTML += `
                        <span class="px-2.5 py-1 text-xs font-semibold bg-gray-100 text-gray-700 rounded-lg border border-gray-200">
                            ${c.name}
                        </span>
                        <input type="hidden" name="CategoryIds[]" value="${c.categoryId}">
                    `;
                });
            } else {
                preview.innerHTML = `<span class="text-xs text-gray-400">No category selected</span>`;
            }
            loadCategories();

            $('#storyStatus').val(data.status).trigger('change');

            if (data.img && data.img.trim() !== "") {
                const fullPath = data.img.startsWith('/') ? data.img : '/' + data.img;
                $imgPreview.attr('src', fullPath).removeClass('hidden'); $uploadIcon.addClass('hidden');
            } else {
                $imgPreview.addClass('hidden').attr('src', ''); $uploadIcon.removeClass('hidden');
            }
        });
    }

    setTimeout(() => {
        $('#modalContent').addClass('translate-y-0 opacity-100');
    }, 10);
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

// Xóa 1 truyện đơn lẻ
async function deleteStory(id) {
    const confirmed = await customConfirm(
        `Are you sure to remove this story ?<br><small class="text-red-400">This action cannot be undone.</small>`,
        "Delete Story"
    );
    if (confirmed) {
        const $row = $(`button[onclick="deleteStory(${id})"]`).closest('tr');
        $row.addClass('opacity-50 pointer-events-none');

        $.ajax({
            url: '/Admin/tblStories/Delete',
            type: 'POST',
            data: { id: id },
            success: function (response) {
                if (response.success) {
                    $row.fadeOut(400, function () {
                        $(this).remove();
                        showToast(response.message, 'success');
                    });
                } else {
                    showToast("Error: " + response.message + (response.inner || ''), 'error');
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
    const category = $('#filterCategory').val();
    const status = $('#filterStatus').val();

    window.location.href = `/Admin/tblStories/ExportToExcel?search=${search}&status=${status}&categoryId=${category}`;
}

// Submit form Thêm / Cập nhật
$('#storyForm').on('submit', function (e) {
    e.preventDefault();
    localStorage.setItem('storyPage', currentPage);

    const submitBtn = $(this).find('button[type="submit"]');
    const formData = new FormData(this);
    const storyId = $('#storyId').val();
    const isAdd = (storyId === "0" || storyId === "");

    // Khi Add mới lấy giá trị từ select, khi Edit thì backend tự giữ nguyên Lang cũ
    if (isAdd) {
        const selectedLangs = $('#storyLang').val();
        if (!selectedLangs || selectedLangs.length === 0) {
            showToast("Please select at least one language for this story!", "error");
            return;
        }
        formData.set('Lang', selectedLangs.join(', '));
    }

    const url = isAdd ? '/Admin/tblStories/Add' : '/Admin/tblStories/Update';

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
                const savedPage = localStorage.getItem('storyPage');
                currentPage = savedPage ? parseInt(savedPage) : 1;
                loadStoryList(currentPage);
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

// Tải bảng danh sách truyện qua AJAX
function loadStoryList(page = 1) {
    currentPage = page;
    const $body = $('#user-list-body');
    const statusVal = $('#filterStatus').val();
    const categoryVal = $('#filterCategory').val();
    const filters = {
        search: $('#filterSearch').val(),
        status: statusVal === 'all' ? '' : statusVal,
        categoryId: categoryVal === 'all' ? '' : categoryVal,
        page: currentPage,
        pageSize: pageSize
    };

    $.ajax({
        url: '/Admin/tblStories/List',
        type: 'GET',
        data: filters,
        success: function (data) {
            let html = '';
            if (!data.stories || data.stories.length === 0) {
                $body.html('<tr><td colspan="9" class="text-center py-10 text-gray-500">Not found any results.</td></tr>');
                $('#pagination-container').html('');
                return;
            }

            data.stories.forEach(story => {
                const avatarHtml = story.img
                    ? `<img onclick="openImagePreview(this.src)"
                        src="/${story.img}"
                        class="w-11 h-11 rounded-2xl object-cover shadow-sm border border-gray-100 cursor-pointer">`
                    : `<div class="w-11 h-11 rounded-2xl bg-gradient-to-tr from-purple-500 to-indigo-600
                                flex items-center justify-center text-white font-bold shadow-sm">
                        ${story.title ? story.title.charAt(0).toUpperCase() : 'U'}
                    </div>`;

                const statusClass =
                    story.status === "Completed" ? "active" :
                        story.status === "Posting" ? "posting" : "comingsoon";

                const categoryHtml = story.categories && story.categories.length
                    ? story.categories.map(c =>
                        `<span class="px-2 py-1 bg-indigo-50 text-indigo-600 rounded-md text-[13px] font-semibold">${c}</span>`
                    ).join(" ")
                    : `<span class="text-sm text-gray-400">No category</span>`;

                // Render Badge ngôn ngữ
                const langArr = (story.lang || "Tiếng Anh, Tiếng Việt").split(',').map(l => l.trim());
                const langBadges = langArr.map(l => {
                    let flag = "🌍";
                    let short = l;
                    if (l.includes("Anh")) { flag = "🇺🇸"; short = "EN"; }
                    else if (l.includes("Việt")) { flag = "🇻🇳"; short = "VN"; }
                    else if (l.includes("Trung")) { flag = "🇨🇳"; short = "ZH"; }
                    else if (l.includes("Nhật")) { flag = "🇯🇵"; short = "JA"; }
                    else if (l.includes("Pháp")) { flag = "🇫🇷"; short = "FR"; }

                    return `<span class="inline-flex items-center gap-1 px-2 py-0.5 bg-teal-50 text-teal-700 border border-teal-200 rounded-lg text-xs font-bold shadow-sm" title="${l}">
                                <span>${flag}</span> <span>${short}</span>
                            </span>`;
                }).join(" ");

                html += `
                <tr class="group hover:bg-indigo-50/30 transition-all">
                    <!-- Checkbox -->
                    <td class="px-6 py-5 text-center">
                        <input type="checkbox"
                            class="user-checkbox w-5 h-5 rounded-md border-gray-300"
                            value="${story.storyId}">
                    </td>

                    <!-- Information -->
                    <td class="px-4 py-5">
                        <div class="flex items-center gap-4">
                            ${avatarHtml}
                            <div>
                                <div class="font-bold text-gray-700">${story.title}</div>
                                <div class="text-xs text-gray-400">${story.authorName}</div>
                            </div>
                        </div>
                    </td>

                    <!-- Languages -->
                    <td class="px-6 py-5">
                        <div class="flex flex-wrap gap-1 max-w-[180px]">
                            ${langBadges}
                        </div>
                    </td>

                    <!-- Publication Date -->
                    <td class="px-6 py-5">
                        <span class="px-3 py-1.5 bg-slate-100 text-gray-600 rounded-lg text-xs font-semibold">
                            ${story.publicationDate ?? ""}
                        </span>
                    </td>

                    <!-- Description -->
                    <td class="px-6 py-5 max-w-xs truncate text-gray-600 text-sm">
                        ${story.description ?? ""}
                    </td>

                    <!-- Engagement metrics -->
                    <td class="px-6 py-5">
                        <div class="text-sm text-gray-600 leading-6 grid grid-cols-2 gap-x-4 gap-y-2">
                            <div class="flex items-center gap-2">
                                <i class="fa-solid text-deny fa-heart w-4 text-center"></i> 
                                <span>${story.likes} loves</span>
                            </div>
                            <div class="flex items-center gap-2">
                                <i class="fa-solid text-wait fa-star w-4 text-center"></i> 
                                <span>${story.rate} rates</span>
                            </div>
                            <div class="flex items-center gap-2">
                                <i class="fa-solid text-cus fa-comment-dollar w-4 text-center"></i> 
                                <span>${story.countRate} rated</span>
                            </div>
                            <div class="flex items-center gap-2">
                                <i class="fa-solid text-accepted fa-users w-4 text-center"></i> 
                                <span>${story.countFolower} followers</span>
                            </div>
                        </div>
                    </td>

                    <!-- Category -->
                    <td class="px-6 py-5 max-w-xs truncate">
                        <div class="flex flex-wrap gap-2">
                            ${categoryHtml}
                        </div>
                    </td>

                    <!-- Status -->
                    <td class="px-6 py-5 text-center">
                        <span class="font-bold px-3 py-1.5 ${statusClass} rounded-xl text-[10px] uppercase tracking-wider">
                            ${story.status}
                        </span>
                    </td>

                    <!-- Actions -->
                  
                    <td class="px-8 py-5 text-right">
                        <div class="flex justify-end gap-3">
                            <button onclick="openModal('edit', ${story.storyId})"
                                class="w-7 aspect-square p-0
                                    flex items-center justify-center
                                    rounded-lg btn-grad bg-blue-50
                                    hover:bg-blue-600 hover:text-white
                                    transition-all duration-200">
                                <i class="fas fa-pencil-alt text-[11px]"></i>
                            </button>

                            <button onclick="deleteStory(${story.storyId})"
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
        error: function () {
            $body.html('<tr><td colspan="9" class="text-center py-10 text-red-500">Error: Cannot load data.</td></tr>');
        }
    });
}
// Xử lý Import Excel
function executeImport() {
    const fileInput = document.getElementById('excelFile');
    if (fileInput.files.length === 0) {
        showToast('Please choose file Excel!', 'error');
        return;
    }

    const formData = new FormData();
    formData.append('file', fileInput.files[0]);

    const $btn = $('#btnDoImport');
    $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> Loading...');

    $.ajax({
        url: '/Admin/tblStories/ImportFromExcel',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            if (res.success) {
                showToast(res.message, 'success');
                closeImportModal();
                loadStoryList(1);
            } else {
                let errorDetails = `${res.message || 'Import failed'}`;
                if (res.error) {
                    errorDetails += `<br><small class="opacity-90">Error: ${res.error}</small>`;
                }
                if (res.stack) {
                    errorDetails += `<br><small class="opacity-75">Detail: ${res.stack}</small>`;
                }
                showToast(errorDetails, 'error');
            }
        },
        error: function (xhr) {
            let errorMsg = 'Error during import file!';
            if (xhr.responseJSON) {
                const res = xhr.responseJSON;
                errorMsg += `<br><small class="opacity-90">Error: ${res.error || res.message}</small>`;
                if (res.stack) {
                    errorMsg += `<br><small class="opacity-75">Detail: ${res.stack}</small>`;
                }
            } else if (xhr.responseText) {
                let rawError = xhr.responseText;
                if (rawError.includes("<i>") && rawError.includes("</i>")) {
                    rawError = rawError.split("<i>")[1].split("</i>")[0];
                } else if (rawError.length > 150) {
                    rawError = rawError.substring(0, 150) + "...";
                }
                errorMsg += `<br><small class="opacity-80">Server Crash: ${rawError}</small>`;
            } else {
                errorMsg += `<br><small class="opacity-75">Status: ${xhr.status} (${xhr.statusText})</small>`;
            }
            showToast(errorMsg, 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).text('Confirm Import');
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

    // First
    html += `
    <button onclick="loadStoryList(1)"
        class="px-2 py-1 border rounded ${currentPage === 1 ? 'opacity-40' : ''}"
        ${currentPage === 1 ? 'disabled' : ''}>
        ⏮
    </button>`;

    // Prev
    html += `
    <button onclick="loadStoryList(${currentPage - 1})"
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

    // Next
    html += `
    <button onclick="loadStoryList(${currentPage + 1})"
        class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40' : ''}"
        ${currentPage === totalPages ? 'disabled' : ''}>
        ▶
    </button>`;

    // Last
    html += `
    <button onclick="loadStoryList(${totalPages})"
        class="px-2 py-1 border rounded ${currentPage === totalPages ? 'opacity-40' : ''}"
        ${currentPage === totalPages ? 'disabled' : ''}>
        ⏭
    </button>`;

    // Go to page
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
    <button onclick="loadStoryList(${page})"
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

    loadStoryList(page);
}

// Lắng nghe sự kiện trang tải, Lọc và Xóa nhiều truyện
$(document).ready(function () {
    const savedPage = localStorage.getItem('storyPage');
    currentPage = savedPage ? parseInt(savedPage) : 1;
    loadStoryList(currentPage);

    $('#filterSearch').on('keyup', function () {
        loadStoryList(1);
    });

    $('#filterCategory, #filterStatus').on('change', function () {
        loadStoryList(1);
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

    // Checkbox từng hàng
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
            showToast('Please select at least one author!', 'error');
            return;
        }

        const confirmed = await customConfirm(
            `Are you sure you want to delete <strong>${ids.length}</strong> stories ?<br>` +
            `<small class="text-red-400">This action cannot be undone.</small>`,
            "Delete Multiple"
        );

        if (!confirmed) return;

        $.ajax({
            url: '/Admin/tblStories/DeleteMultiple',
            type: 'POST',
            traditional: true,
            data: { ids: ids },
            success: function (res) {
                if (res.blocked && res.blocked.length > 0) {
                    showToast(
                        `Cannot delete this ID: ${res.blocked.join(', ')}\n` +
                        `Because it was used to link foreign keys.`,
                        'error'
                    );
                    updateDeleteButton();
                }
                if (res.deleted && res.deleted.length > 0) {
                    showToast(`Deleted ${res.deleted.length} story.`, 'error');
                }
                const savedPage = localStorage.getItem('storyPage');
                currentPage = savedPage ? parseInt(savedPage) : 1;
                loadStoryList(currentPage);
                $('#selectAll').prop('checked', false);
            }
        });
    });
});