/* paragraphs-crud.js - Xử lý Bảng dữ liệu, Phân trang, Bộ lọc, CRUD câu đơn, Xóa nhiều & Export */

var L = window.ADMIN_LANG || {};

let currentPage = 1;
const pageSize = 10;

let quillEnglish, quillVietnamese, quillChinese, quillJapanese, quillFrench;

const quillToolbar = [
    ['bold', 'italic', 'underline', 'strike'],
    [{ 'color': [] }, { 'background': [] }],
    ['clean']
];

$(document).ready(function () {
    // 1. Khởi tạo Flatpickr & Select2
    $(".datepicker").flatpickr({ dateFormat: "Y-m-d", allowInput: true });

    $('.select2-custom').each(function () {
        $(this).select2({
            width: '100%',
            minimumResultsForSearch: Infinity,
            templateResult: formatSelectState,
            templateSelection: formatSelectState
        });
    });

    initModalQuillEditors();

    // 2. Load bộ lọc và danh sách bảng
    const savedPage = localStorage.getItem('paragraphPage');
    currentPage = savedPage ? parseInt(savedPage) : 1;

    loadFilterStories();
    loadParagraphList(currentPage);

    $('#filterSearch').on('keyup', () => { currentPage = 1; loadParagraphList(1); });
    $('#filterChapter').on('change', () => { currentPage = 1; loadParagraphList(1); });

    // 3. Sự kiện Checkbox chọn tất cả
    $('#selectAll').on('change', function () {
        const isChecked = this.checked;
        $('.user-checkbox').each(function () {
            $(this).prop('checked', isChecked).closest('tr').toggleClass('bg-indigo-50/50', isChecked);
        });
        updateDeleteButton();
    });

    // 4. Checkbox từng hàng
    $(document).on('change', '.user-checkbox', function () {
        const total = $('.user-checkbox').length;
        const checked = $('.user-checkbox:checked').length;
        $(this).closest('tr').toggleClass('bg-indigo-50/50', this.checked);
        $('#selectAll').prop('checked', total > 0 && total === checked);
        updateDeleteButton();
    });

    // 5. Nút xóa nhiều dòng
    $(document).on('click', '#btnDeleteSelected', async function () {
        const ids = $('.user-checkbox:checked').map(function () { return parseInt(this.value); }).get();
        if (ids.length === 0) {
            showToast(L.SelectAtLeastOnePara || 'Please select at least one paragraph!', 'error');
            return;
        }

        const actionText = L.ActionCannotUndo || 'The chapter will be automatically re-indexed.';
        const multiMsg = (L.ConfirmDeleteMultiParasMsg || 'Are you sure you want to delete <strong>{0}</strong> selected paragraphs?')
            .replace('{0}', ids.length) + `<br><small class="text-red-400">${actionText}</small>`;
        const multiTitle = L.ConfirmDeleteMultiParasTitle || "Delete Multiple";

        const confirmed = await safeConfirm(multiMsg, multiTitle);
        if (!confirmed) return;

        const deletingText = L.Processing || 'Deleting...';
        const $btn = $(this).prop('disabled', true).html(`<i class="fas fa-spinner fa-spin mr-2"></i>${deletingText}`);

        $.ajax({
            url: '/Admin/tblParagraphs/DeleteMultiple',
            type: 'POST',
            traditional: true,
            data: { ids: ids },
            success: function (res) {
                if (res.success) {
                    const count = res.deleted ? res.deleted.length : ids.length;
                    const successMsg = (L.DeletedParasSuccess || 'Deleted {0} paragraph(s).').replace('{0}', count);
                    showToast(res.message || successMsg, 'success');
                    $('#selectAll').prop('checked', false);
                    updateDeleteButton();
                    loadParagraphList(currentPage);
                } else {
                    showToast(res.message || (L.ErrDeleteParasServer || 'Error occurred while deleting paragraphs.'), 'error');
                }
            },
            error: function (xhr) {
                const msg = xhr.responseJSON ? xhr.responseJSON.message : (L.ErrConnectServer || "Cannot connect to server!");
                showToast(msg, 'error');
            },
            complete: () => {
                $btn.prop('disabled', false);
                updateDeleteButton();
            }
        });
    });

    // 6. Submit form cập nhật câu đơn
    $('#paragraphForm').on('submit', function (e) {
        e.preventDefault();
        localStorage.setItem('paragraphPage', currentPage);

        const submitBtn = $(this).find('button[type="submit"]');

        if (quillEnglish && !$('#paragraphEnglishValue').prop('disabled'))
            $('#paragraphEnglishValue').val(normalizeQuillHtml(quillEnglish.root.innerHTML));
        if (quillVietnamese && !$('#paragraphVietnameseValue').prop('disabled'))
            $('#paragraphVietnameseValue').val(normalizeQuillHtml(quillVietnamese.root.innerHTML));
        if (quillChinese && !$('#paragraphChineseValue').prop('disabled'))
            $('#paragraphChineseValue').val(normalizeQuillHtml(quillChinese.root.innerHTML));
        if (quillJapanese && !$('#paragraphJapaneseValue').prop('disabled'))
            $('#paragraphJapaneseValue').val(normalizeQuillHtml(quillJapanese.root.innerHTML));
        if (quillFrench && !$('#paragraphFrenchValue').prop('disabled'))
            $('#paragraphFrenchValue').val(normalizeQuillHtml(quillFrench.root.innerHTML));

        const formData = new FormData(this);
        submitBtn.prop('disabled', true).html(`<i class="fas fa-spinner animate-spin"></i> ${L.Processing || 'Processing...'}`);

        $.ajax({
            url: '/Admin/tblParagraphs/Update',
            type: 'POST',
            data: formData,
            contentType: false,
            processData: false,
            success: function (res) {
                if (res.success) {
                    showToast(res.message, 'success');
                    closeModal();
                    loadParagraphList(currentPage);
                } else {
                    showToast(res.message, 'error');
                }
            },
            error: () => showToast(L.ErrConnectServer || 'Cannot connect to server', 'error'),
            complete: () => submitBtn.prop('disabled', false).html(L.SaveChanges || 'Save Changes')
        });
    });
});

function formatSelectState(state) {
    if (!state.id) return state.text;
    const gradient = $(state.element).data('color') || 'linear-gradient(135deg, #667eea, #764ba2)';
    return $(
        '<span style="display: flex; align-items: center;">' +
        '<span style="display:inline-block; width:10px; height:10px; border-radius:50%; background:' + gradient + '; margin-right:10px;"></span>' +
        '<span style="background:' + gradient + '; -webkit-background-clip: text; -webkit-text-fill-color: transparent; font-weight:700;">' + state.text + '</span>' +
        '</span>'
    );
}

function initModalQuillEditors() {
    const commonModules = {
        toolbar: quillToolbar,
        keyboard: {
            bindings: {
                enter: { key: 'Enter', handler: () => false },
                shiftEnter: { key: 'Enter', shiftKey: true, handler: () => false }
            }
        }
    };

    quillEnglish = new Quill('#paragraphEnglish', { theme: 'snow', modules: commonModules });
    quillVietnamese = new Quill('#paragraphVietnamese', { theme: 'snow', modules: commonModules });
    quillChinese = new Quill('#paragraphChinese', { theme: 'snow', modules: commonModules });
    quillJapanese = new Quill('#paragraphJapanese', { theme: 'snow', modules: commonModules });
    quillFrench = new Quill('#paragraphFrench', { theme: 'snow', modules: commonModules });
}

function switchParagraphLanguage(language) {
    $('.paragraph-language-content').addClass('hidden');
    $('#tabEnglish, #tabVietnamese, #tabChinese, #tabJapanese, #tabFrench')
        .removeClass('text-purple-600 border-purple-500').addClass('text-gray-400 border-transparent');

    const tab = $('#tab' + language);
    const content = $('#paragraphLanguage' + language);
    if (tab.hasClass('hidden')) return;

    content.removeClass('hidden');
    tab.removeClass('text-gray-400 border-transparent').addClass('text-purple-600 border-purple-500');
}

function setLanguageVisibility(language, hasContent) {
    const tab = $('#tab' + language);
    const content = $('#paragraphLanguage' + language);
    if (hasContent) {
        tab.removeClass('hidden');
        content.removeClass('hidden');
    } else {
        tab.addClass('hidden');
        content.addClass('hidden');
    }
}

function setLanguageInputState(language, exists) {
    const input = $('#paragraph' + language + 'Value');
    input.prop('disabled', !exists);
    if (!exists) input.val('');
}

function openModal(mode, id) {
    if (mode !== 'edit') return;

    const modal = $('#modalOverlay');
    const form = $('#paragraphForm')[0];
    if (form) form.reset();

    $('#paragraphBlockType').val('1').trigger('change');
    ['English', 'Vietnamese', 'Chinese', 'Japanese', 'French'].forEach(lang => setLanguageVisibility(lang, false));

    if (quillEnglish) quillEnglish.setText('');
    if (quillVietnamese) quillVietnamese.setText('');
    if (quillChinese) quillChinese.setText('');
    if (quillJapanese) quillJapanese.setText('');
    if (quillFrench) quillFrench.setText('');

    $('#paragraphEnglishValue, #paragraphVietnameseValue, #paragraphChineseValue, #paragraphJapaneseValue, #paragraphFrenchValue').val('');

    modal.removeClass('hidden').addClass('flex');
    $('.select2-custom').each(function () {
        if (!$(this).hasClass('select2-hidden-accessible')) {
            $(this).select2({ dropdownParent: modal });
        }
    });

    setTimeout(() => $('#modalContent').removeClass('translate-y-20 opacity-0').addClass('translate-y-0 opacity-100'), 10);

    $.get('/Admin/tblParagraphs/GetById/' + id)
        .done(function (data) {
            $('#paragraphId').val(data.paragraphId);
            $('#paragraphOrder').val(data.paragraphOrder);

            const rawBlockType = (data.blockType !== undefined && data.blockType !== null)
                ? data.blockType : (data.BlockType !== undefined && data.BlockType !== null ? data.BlockType : 1);

            $('#paragraphBlockType').val(String(rawBlockType)).trigger('change');
            $('#displayParagraphId').text(data.paragraphId);
            $('#displayChapterName').text((data.chapter && data.chapter.chapterTitle) || '-');
            $('#displayParagraphOrder').text(data.paragraphOrder);

            if (data.chapter && data.chapter.storyId) {
                $.get('/Admin/tblParagraphs/GetChaptersForSelect', { id: data.chapter.storyId })
                    .done(function (chapters) {
                        const select = $('#paragraphChapterId').empty();
                        chapters.forEach(x => select.append(`<option value="${x.id}">${x.name}</option>`));
                        select.val(data.chapter.chapterId).trigger('change');
                    });
            }

            const languages = [
                { name: 'English', value: data.english, quill: quillEnglish },
                { name: 'Vietnamese', value: data.vietnamese, quill: quillVietnamese },
                { name: 'Chinese', value: data.chinese, quill: quillChinese },
                { name: 'Japanese', value: data.japanese, quill: quillJapanese },
                { name: 'French', value: data.french, quill: quillFrench }
            ];

            languages.forEach(lang => {
                const exists = lang.value !== null;
                setLanguageVisibility(lang.name, exists);
                setLanguageInputState(lang.name, exists);

                if (exists && lang.quill) {
                    lang.quill.root.innerHTML = lang.value;
                    $('#paragraph' + lang.name + 'Value').val(lang.value);
                } else {
                    $('#paragraph' + lang.name + 'Value').val('');
                }
            });

            const firstLanguage = languages.find(lang => lang.value !== null);
            if (firstLanguage) switchParagraphLanguage(firstLanguage.name);
        })
        .fail(() => showToast(L.CannotFetchInfo || 'Cannot load paragraph information', 'error'));
}

function closeModal() {
    $('#modalContent').removeClass('translate-y-0 opacity-100 scale-100').addClass('translate-y-10 opacity-0 scale-95');
    setTimeout(() => $('#modalOverlay').removeClass('flex').addClass('hidden'), 300);
}

function loadFilterStories() {
    $.get('/Admin/tblParagraphs/GetStoriesForSelect', function (data) {
        const allText = L.FilterAllStories || 'Stories';
        const $story = $('#filterStory').html(`<option value="all">${allText}</option>`);
        data.forEach(s => $story.append(`<option value="${s.id}">${s.name}</option>`));
        $story.trigger('change.select2');
    });
}

function loadChaptersByStory(storyId) {
    const $chapter = $('#filterChapter');
    const allText = L.FilterAllChapters || 'Chapter';
    if (!storyId || storyId === 'all') {
        $chapter.html(`<option value="all">${allText}</option>`).trigger('change.select2');
        return;
    }

    $.get('/Admin/tblParagraphs/GetChaptersForSelect', { id: storyId }, function (data) {
        $chapter.html(`<option value="all">${allText}</option>`);
        data.forEach(c => $chapter.append(`<option value="${c.id}">${c.name}</option>`));
        $chapter.trigger('change.select2');
    });
}

$('#filterStory').on('change', function () {
    loadChaptersByStory($(this).val());
    currentPage = 1;
    loadParagraphList(1);
});

// Tải bảng danh sách qua AJAX
function loadParagraphList(page = 1) {
    currentPage = page;
    const $body = $('#user-list-body');

    const filters = {
        search: $('#filterSearch').val(),
        storyId: $('#filterStory').val(),
        chapterId: $('#filterChapter').val(),
        page: currentPage,
        pageSize: pageSize
    };

    $.ajax({
        url: '/Admin/tblParagraphs/List',
        type: 'GET',
        data: filters,
        success: function (data) {
            if (!data.paragraphs || data.paragraphs.length === 0) {
                const notFound = L.NotFoundParagraphs || 'Not found any results.';
                $body.html(`<tr><td colspan="10" class="text-center py-10 text-gray-500">${notFound}</td></tr>`);
                $('#pagination-container').html('');
                return;
            }

            let html = '';
            data.paragraphs.forEach(p => {
                let en = p.english || '', vn = p.vietnamese || '', zh = p.chinese || '', ja = p.japanese || '', fr = p.french || '';
                let bType = p.blockType !== undefined && p.blockType !== null ? parseInt(p.blockType) : 1;

                let blockBadge = '';
                switch (bType) {
                    case 0: blockBadge = `<span class="px-2.5 py-1 bg-amber-50 text-amber-600 border border-amber-200 rounded-lg text-xs font-semibold">0 - ${L.BlockTypeOpening || 'Opening'}</span>`; break;
                    case 2: blockBadge = `<span class="px-2.5 py-1 bg-blue-50 text-blue-600 border border-blue-200 rounded-lg text-xs font-semibold">2 - ${L.BlockTypeNew || 'New paragraph'}</span>`; break;
                    case 4: blockBadge = `<span class="px-2.5 py-1 bg-purple-50 text-purple-600 border border-purple-200 rounded-lg text-xs font-semibold">4 - ${L.BlockTypeDialogue || 'Dialogue'}</span>`; break;
                    default: blockBadge = `<span class="px-2.5 py-1 bg-gray-50 text-gray-600 border border-gray-200 rounded-lg text-xs font-medium">1 - ${L.BlockTypeContinuous || 'Continuous'}</span>`; break;
                }

                html += `
                <tr class="group hover:bg-indigo-50/30 transition-all">
                    <td class="px-6 py-5">
                        <input type="checkbox" class="user-checkbox w-5 h-5 rounded-md border-gray-300" value="${p.paragraphId}">
                    </td>
                    <td class="px-4 py-5">
                        <div class="text-xs text-gray-400 mb-1">${p.storyTitle || ''} → ${p.chapterTitle || ''}</div>
                    </td>
                    <td class="px-6 py-5 text-center">
                        <span class="px-3 py-1 bg-slate-100 rounded-lg text-xs font-bold">${p.paragraphOrder}</span>
                    </td>
                    <td class="px-6 py-5 max-w-xs truncate text-gray-600 text-sm" title="${en}">${en}</td>
                    <td class="px-6 py-5 max-w-xs truncate text-gray-600 text-sm" title="${vn}">${vn}</td>
                    <td class="px-6 py-5 max-w-xs truncate text-gray-600 text-sm" title="${zh}">${zh}</td>
                    <td class="px-6 py-5 max-w-xs truncate text-gray-600 text-sm" title="${ja}">${ja}</td>
                    <td class="px-6 py-5 max-w-xs truncate text-gray-600 text-sm" title="${fr}">${fr}</td>
                    <td class="px-6 py-5 whitespace-nowrap">${blockBadge}</td>
                    <td class="px-8 py-5 text-right">
                        <div class="flex justify-end gap-2">
                            <button type="button" title="${L.TitleInsertAfter || 'Insert paragraph after'}" onclick="openInsertParagraphModal(${p.chapterId}, ${p.paragraphOrder})" class="w-7 h-7 rounded-lg bg-teal-50 text-teal-600 hover:bg-teal-600 hover:text-white transition-all flex items-center justify-center">
                                <i class="fas fa-plus text-[11px]"></i>
                            </button>
                            <button type="button" title="${L.TitleEditPara || 'Edit paragraph'}" onclick="openModal('edit', ${p.paragraphId})" class="w-7 h-7 rounded-lg bg-blue-50 text-blue-600 hover:bg-blue-600 hover:text-white transition-all flex items-center justify-center">
                                <i class="fas fa-pencil-alt text-[11px]"></i>
                            </button>
                            <button type="button" title="${L.TitleReadChapter || 'Read & edit whole chapter'}" onclick="openChapterEditor(${p.chapterId})" class="w-7 h-7 rounded-lg bg-indigo-50 text-indigo-600 hover:bg-indigo-600 hover:text-white transition-all flex items-center justify-center">
                                <i class="fas fa-book-open text-[11px]"></i>
                            </button>
                            <button onclick="deleteParagraph(${p.paragraphId})" title="${L.TitleDeletePara || 'Delete paragraph'}" class="w-7 h-7 rounded-lg bg-red-50 text-red-600 hover:bg-red-600 hover:text-white transition-all flex items-center justify-center">
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
        error: () => {
            const errLoad = L.ErrLoadParagraphs || 'Error loading data.';
            $body.html(`<tr><td colspan="10" class="text-center py-10 text-red-500">${errLoad}</td></tr>`);
        }
    });
}

// Xóa đơn 1 câu
async function deleteParagraph(id) {
    if (!id || id <= 0) {
        showToast(L.DeleteParaInvalidId || "Invalid paragraph ID!", "error");
        return;
    }

    const actionText = L.ActionCannotUndo || 'The chapter will be automatically re-ordered.';
    const confirmMsg = (L.ConfirmDeleteSingleParaMsg || 'Are you sure you want to delete this paragraph?') + `<br><small class="text-red-400">${actionText}</small>`;
    const confirmTitle = L.ConfirmDeleteSingleParaTitle || "Delete Paragraph";

    const confirmed = await safeConfirm(confirmMsg, confirmTitle);
    if (!confirmed) return;

    const $row = $(`button[onclick*="deleteParagraph(${id})"]`).closest('tr');
    $row.addClass('opacity-50 pointer-events-none');

    $.ajax({
        url: '/Admin/tblParagraphs/Delete',
        type: 'POST',
        data: { id: id },
        success: function (res) {
            if (res.success) {
                $row.fadeOut(300, function () {
                    $(this).remove();
                    showToast(res.message || "Deleted successfully!", 'success');
                    loadParagraphList(currentPage);
                });
            } else {
                showToast("Error: " + res.message, 'error');
                $row.removeClass('opacity-50 pointer-events-none');
            }
        },
        error: function (xhr) {
            const msg = xhr.responseJSON ? xhr.responseJSON.message : (L.ErrConnectDelete || "Cannot connect to server to delete.");
            showToast(msg, 'error');
            $row.removeClass('opacity-50 pointer-events-none');
        }
    });
}

function exportExcel() {
    const search = $('#filterSearch').val();
    const chapterId = $('#filterChapter').val();
    window.location.href = `/Admin/tblParagraphs/ExportToExcel?search=${search}&chapterId=${chapterId}`;
}

function updateDeleteButton() {
    const count = $('.user-checkbox:checked').length;
    const $btn = $('#btnDeleteSelected');
    const deleteLabel = L.BtnDeleteText || 'Delete';

    if (count > 0) {
        $btn.html(`<i class="fas fa-trash-alt mr-2"></i>${deleteLabel}(${count})`).prop('disabled', false).removeClass('opacity-50 cursor-not-allowed');
    } else {
        $btn.html(`<i class="fas fa-trash-alt mr-2"></i>${deleteLabel}`).prop('disabled', true).addClass('opacity-50 cursor-not-allowed');
    }
}

function renderPagination(curr, total) {
    const container = $('#pagination-container').empty();
    if (total <= 1) return;

    const maxVisible = 2;
    let html = `<div class="flex items-center gap-1">`;
    html += `<button onclick="loadParagraphList(1)" class="px-2 py-1 border rounded ${curr === 1 ? 'opacity-40' : ''}" ${curr === 1 ? 'disabled' : ''}>⏮</button>`;
    html += `<button onclick="loadParagraphList(${curr - 1})" class="px-2 py-1 border rounded ${curr === 1 ? 'opacity-40' : ''}" ${curr === 1 ? 'disabled' : ''}>◀</button>`;

    if (curr > maxVisible + 1) html += pageBtn(1, curr) + `<span class="px-2">…</span>`;

    for (let i = Math.max(1, curr - maxVisible); i <= Math.min(total, curr + maxVisible); i++) {
        html += pageBtn(i, curr);
    }

    if (curr < total - maxVisible) html += `<span class="px-2">…</span>` + pageBtn(total, curr);

    html += `<button onclick="loadParagraphList(${curr + 1})" class="px-2 py-1 border rounded ${curr === total ? 'opacity-40' : ''}" ${curr === total ? 'disabled' : ''}>▶</button>`;
    html += `<button onclick="loadParagraphList(${total})" class="px-2 py-1 border rounded ${curr === total ? 'opacity-40' : ''}" ${curr === total ? 'disabled' : ''}>⏭</button>`;
    html += `<div class="flex items-center gap-1 ml-3"><span class="text-sm">Go:</span><input type="number" min="1" max="${total}" value="${curr}" class="w-16 px-2 py-1 border rounded text-center" onkeydown="if(event.key==='Enter') gotoPage(this, ${total})"></div>`;
    html += `</div>`;
    container.html(html);
}

function pageBtn(p, c) {
    return `<button onclick="loadParagraphList(${p})" class="px-3 py-1 border rounded ${p === c ? 'btn-grad text-white font-bold' : 'hover:bg-gray-100'}">${p}</button>`;
}

function gotoPage(input, total) {
    let p = parseInt(input.value);
    if (!isNaN(p)) loadParagraphList(Math.max(1, Math.min(p, total)));
}

async function safeConfirm(message, title = "Confirm") {
    if (typeof customConfirm === 'function') {
        try { return await customConfirm(message, title); } catch (e) { }
    }
    return window.confirm(message.replace(/<[^>]*>?/gm, ''));
}

function openImagePreview(src) {
    if (!src) return;
    $('#imageModalContent').attr('src', src);
    $('#imageModal').removeClass('hidden').addClass('flex');
}

function closeImagePreview() {
    $('#imageModal').addClass('hidden').removeClass('flex');
}