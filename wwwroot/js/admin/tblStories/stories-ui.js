/* stories-ui.js - Quản lý giao diện, Select2, Flatpickr, Preview ảnh & Modal chọn Category */

const UI_LANG = window.ADMIN_LANG || {};

let selectedCategoryIds = new Set();

$(document).ready(function () {
    $(".datepicker").flatpickr({
        dateFormat: "Y-m-d",
        allowInput: true
    });

    $('.select2-custom').not('#filterCategory').each(function () {
        $(this).select2({
            width: '100%',
            minimumResultsForSearch: Infinity,
            templateResult: formatState,
            templateSelection: formatState
        });
    });

    $('#storyLang').select2({
        width: '100%',
        placeholder: UI_LANG.SelectLanguagesPlaceholder || 'Select languages...',
        closeOnSelect: false
    });

    loadAuthors();
    loadCategoryFilter();
    loadCategories();
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

function loadAuthors(selectedId = null) {
    fetch('/Admin/tblStories/GetAuthorsForSelect')
        .then(res => res.json())
        .then(data => {
            const select = document.getElementById('storyAuthorName');
            if (!select) return;
            const selectAuthorText = UI_LANG.SelectAuthor || 'Select author';
            select.innerHTML = `<option value="Unknow">${selectAuthorText}</option>`;

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

// 1. Tải bộ lọc dropdown Thể loại theo ngôn ngữ
function loadCategoryFilter(selectedId = 'all') {
    const currentCulture = (typeof getCurrentCulture === 'function') ? getCurrentCulture() : (document.documentElement.lang || 'en-US');

    fetch(`/Admin/tblStories/GetCategoriesForSelect?culture=${currentCulture}`)
        .then(res => res.json())
        .then(data => {
            const $select =$('#filterCategory');
            $select.empty();
            const allCatText = UI_LANG.FilterAllCategories || 'Categories';
            $select.append(`<option data-color="linear-gradient(to right, #50C9C3 0%, #96DEDA 51%, #50C9C3 100%)" value="all">${allCatText}</option>`);

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
function openCategoryModal() {
    const modal = document.getElementById('categoryModal');
    const content = document.getElementById('categoryModalContent');

    modal.classList.remove('hidden');
    modal.classList.add('flex');

    setTimeout(() => {
        content.classList.remove('translate-y-20', 'opacity-0');
    }, 50);
}

function closeCategoryModal() {
    const modal = document.getElementById('categoryModal');
    const content = document.getElementById('categoryModalContent');

    content.classList.add('translate-y-20', 'opacity-0');

    setTimeout(() => {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }, 300);
}

function loadCategories() {
    const currentCulture = (typeof getCurrentCulture === 'function') ? getCurrentCulture().trim() : 'vi-VN';

    $.get('/Admin/tblStories/GetCategoriesForSelect?culture=' + currentCulture, function (data) {
        if (!data || !Array.isArray(data)) return;

        let html = '';
        data.forEach(cat => {
            const isChecked = (typeof selectedCategoryIds !== 'undefined' && selectedCategoryIds.has(String(cat.id))) ? 'checked' : '';
            const activeClass = isChecked ? 'border-teal-500 bg-teal-50/60 text-teal-800 shadow-sm ring-1 ring-teal-400' : 'border-slate-200 bg-white text-slate-700 hover:border-teal-300 hover:bg-slate-50';

            html += `
                <label class="group relative flex items-center gap-3 px-3.5 py-2.5 rounded-xl border transition-all duration-200 cursor-pointer select-none ${activeClass}">
                    <div class="relative flex items-center justify-center">
                        <input type="checkbox" 
                               class="cat-checkbox peer w-4 h-4 rounded-md border-slate-300 text-teal-600 focus:ring-teal-500/30 focus:ring-2 cursor-pointer transition-all" 
                               value="${cat.id}" 
                               data-name="${cat.name}" 
                               ${isChecked} 
                               onchange="onCategoryChange(this)">
                    </div>
                    <span class="text-sm font-semibold tracking-tight transition-colors group-hover:text-teal-700">
                        ${cat.name}
                    </span>
                </label>
            `;
        });

        // Bọc vào khung lưới 2 hoặc 3 cột tùy kích thước modal
        $('#categoryCheckboxList')
            .removeClass('space-y-1')
            .addClass('grid grid-cols-2 sm:grid-cols-3 gap-2.5 max-h-60 overflow-y-auto p-1 pr-2')
            .html(html);
    });
}

function applyCategories() {
    selectedCategoryIds.clear();

    const preview = document.getElementById('selectedCategories');
    preview.innerHTML = '';

    document.querySelectorAll('.category-checkbox:checked')
        .forEach(cb => {
            const id = cb.value;
            const name = cb.dataset.name;

            selectedCategoryIds.add(id);

            preview.innerHTML += `
                <span class="px-3 py-1 text-xs font-semibold
                            bg-indigo-100 text-indigo-600
                            rounded-full">
                    ${name}
                </span>
                <input type="hidden" name="CategoryIds[]" value="${id}">
            `;
        });

    if (selectedCategoryIds.size === 0) {
        const noCatText = UI_LANG.NoCategoriesSelected || 'No category selected';
        preview.innerHTML = `<span class="text-xs text-gray-400">${noCatText}</span>`;
    }

    closeCategoryModal();
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
    const dragDropText = UI_LANG.DragDropExcelStory || 'Drag and drop the file here or click to select it.';
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