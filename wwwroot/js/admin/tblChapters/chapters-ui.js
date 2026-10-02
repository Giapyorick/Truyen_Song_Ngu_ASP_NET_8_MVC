/* chapters-ui.js - Quản lý giao diện, Select2, Flatpickr, nạp danh sách truyện cho bộ lọc */

const UI_LANG = window.ADMIN_LANG || {};

$(document).ready(function () {
    $(".datepicker").flatpickr({
        dateFormat: "Y-m-d",
        allowInput: true
    });

    $('.select2-custom').not('#filterStory').each(function () {
        $(this).select2({
            width: '100%',
            minimumResultsForSearch: Infinity,
            templateResult: formatState,
            templateSelection: formatState
        });
    });

    loadStoryFilter();
    loadStories();
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

function loadStoryFilter(selectedId = 'all') {
    fetch('/Admin/tblChapters/GetStoriesForSelect')
        .then(res => res.json())
        .then(data => {
            const $select = $('#filterStory');
            $select.empty();
            const allStoriesText = UI_LANG.FilterAllStories || 'Stories';
            $select.append(`<option data-color="linear-gradient(to right, #50C9C3 0%, #96DEDA 51%, #50C9C3 100%)" value="all">${allStoriesText}</option>`);

            data.forEach(c => {
                $select.append(`
                    <option data-color="linear-gradient(135deg, #667eea, #764ba2)" value="${c.id}">
                        ${c.name}
                    </option>
                `);
            });

            if ($select.hasClass("select2-hidden-accessible")) {
                $select.select2('destroy');
            }

            $select.select2({
                width: '100%',
                minimumResultsForSearch: Infinity,
                allowClear: false,
                templateResult: formatState,
                templateSelection: formatState
            });

            $select.val(selectedId).trigger('change');
        });
}

function loadStories(selectedId = null) {
    fetch('/Admin/tblChapters/GetStoriesForSelect')
        .then(res => res.json())
        .then(data => {
            const select = document.getElementById('chapterStoryId');
            if (!select) return;
            const selectStoryText = UI_LANG.SelectStory || 'Select story';
            select.innerHTML = `<option value="Unknow">${selectStoryText}</option>`;

            data.forEach(a => {
                const selected = selectedId == a.id ? 'selected' : '';
                select.innerHTML += `
                    <option value="${a.id}" ${selected}>
                        ${a.name}
                    </option>
                `;
            });
        });
}

function previewImage(input) {
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = function (e) {
            $('#imgPreview').attr('src', e.target.result).removeClass('hidden');
            $('#uploadIcon').addClass('hidden');
        };
        reader.readAsDataURL(input.files[0]);
    }
}

function openImagePreview(src) {
    if (!src) return;
    $('#imageModalContent').attr('src', src);
    $('#imageModal').removeClass('hidden').addClass('flex');
}

function closeImagePreview() {
    $('#imageModal').addClass('hidden').removeClass('flex');
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

$('#excelFile').on('change', function (e) {
    const file = e.target.files[0];
    if (file) {
        $('#fileStatus').html(`<span class="text-indigo-600 font-bold italic">${file.name}</span>`);
        $('#dropZone').addClass('border-indigo-500 bg-indigo-100/50');
    }
});

function resetImportForm() {
    $('#excelFile').val('');
    const dragDropText = UI_LANG.DragDropExcelChapter || 'Drag and drop or click to import file';
    $('#fileStatus').text(dragDropText);
    $('#dropZone').removeClass('border-indigo-500 bg-indigo-100/50');
}

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