/* stories-ui.js - Quản lý giao diện, Select2, Flatpickr, Preview ảnh & Modal chọn Category */

let selectedCategoryIds = new Set();
console.log('selectedCategoryIds', selectedCategoryIds);

$(document).ready(function () {
    // 1. Khởi tạo Flatpickr
    $(".datepicker").flatpickr({
        dateFormat: "Y-m-d",
        allowInput: true
    });

    // 2. Khởi tạo Select2 thông thường
    $('.select2-custom').not('#filterCategory').each(function () {
        $(this).select2({
            width: '100%',
            minimumResultsForSearch: Infinity,
            templateResult: formatState,
            templateSelection: formatState
        });
    });

    // 3. Khởi tạo Select2 Multi-select cho ô chọn Ngôn ngữ (#storyLang)
    $('#storyLang').select2({
        width: '100%',
        placeholder: 'Select languages...',
        closeOnSelect: false
    });

    // 4. Nạp dữ liệu dropdown
    loadAuthors();
    loadCategoryFilter();
    loadCategories();
});

// Định dạng chấm tròn và chữ gradient cho option Select2
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

// Nạp danh sách tác giả vào dropdown CRUD form
function loadAuthors(selectedId = null) {
    fetch('/Admin/tblStories/GetAuthorsForSelect')
        .then(res => res.json())
        .then(data => {
            console.log(data);
            const select = document.getElementById('storyAuthorName');
            if (!select) return;
            select.innerHTML = `<option value="Unknow">Select author</option>`;

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

// Nạp danh mục vào dropdown bộ lọc ở thanh công cụ và khởi tạo Select2 sạch sẽ
function loadCategoryFilter(selectedId = 'all') {
    fetch('/Admin/tblStories/GetCategoriesForSelect')
        .then(res => res.json())
        .then(data => {
            const $select = $('#filterCategory');
            $select.empty(); $select.append(`<option data-color="linear-gradient(to right, #50C9C3 0%, #96DEDA 51%, #50C9C3 100%)" value="all">Categories</option>`);

            data.forEach(c => {
                $select.append(`
                    <option data-color="linear-gradient(135deg, #667eea, #764ba2)" value="${c.id}">
                        ${c.name}
                    </option>
                `);
            });

            // Nếu Select2 đã khởi tạo trước đó thì hủy để áp dụng options mới
            if ($select.hasClass("select2-hidden-accessible")) {
                $select.select2('destroy');
            }

            // Khởi tạo Select2 với đầy đủ template gradient và ẩn ô tìm kiếm thừa
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

// Mở modal chọn nhiều danh mục
function openCategoryModal() {
    const modal = document.getElementById('categoryModal');
    const content = document.getElementById('categoryModalContent');

    modal.classList.remove('hidden');
    modal.classList.add('flex');

    setTimeout(() => {
        content.classList.remove('translate-y-20', 'opacity-0');
    }, 50);
}

// Đóng modal chọn danh mục
function closeCategoryModal() {
    const modal = document.getElementById('categoryModal');
    const content = document.getElementById('categoryModalContent');

    content.classList.add('translate-y-20', 'opacity-0');

    setTimeout(() => {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }, 300);
}

// Nạp danh sách checkbox trong modal danh mục
function loadCategories() {
    fetch('/Admin/tblStories/GetCategoriesForSelect')
        .then(res => res.json())
        .then(data => {
            const container = document.getElementById('categoryCheckboxList');
            if (!container) return;
            container.innerHTML = '';

            data.forEach(c => {
                const checked = selectedCategoryIds.has(String(c.id)) ? 'checked' : '';

                container.innerHTML += `
                    <label class="flex items-center gap-3 p-3 rounded-xl
                                border hover:border-indigo-500 cursor-pointer">
                        <input type="checkbox"
                            class="category-checkbox accent-indigo-600"
                            value="${c.id}"
                            data-name="${c.name}"
                            ${checked}>
                        <span class="font-semibold text-gray-700">${c.name}</span>
                    </label>
                `;
            });
        });
}

// Áp dụng các danh mục đã tick vào thẻ xem trước preview
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
        preview.innerHTML = `<span class="text-xs text-gray-400">No category selected</span>`;
    }

    closeCategoryModal();
}

// Xem trước ảnh khi chọn từ máy
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

// Phóng to ảnh
function openImagePreview(src) {
    if (!src) return;
    $('#imageModalContent').attr('src', src);
    $('#imageModal').removeClass('hidden').addClass('flex');
}

function closeImagePreview() {
    $('#imageModal').addClass('hidden').removeClass('flex');
}

// Mở modal Import Excel
function openImportModal() {
    const modal = $('#importModal');
    modal.removeClass('hidden').addClass('flex');
    setTimeout(() => $('#importContent').addClass('scale-100 opacity-100'), 10);
}

// Đóng modal Import Excel
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
    $('#fileStatus').text('Drag and drop the file here or click to select it.');
    $('#dropZone').removeClass('border-indigo-500 bg-indigo-100/50');
}

// Cập nhật số lượng trên nút Delete(N)
function updateDeleteButton() {
    const count = $('.user-checkbox:checked').length;
    const $btn = $('#btnDeleteSelected');

    if (count > 0) {
        $btn.html(`<i class="fas fa-trash-alt mr-2"></i>Delete(${count})`);
        $btn.prop('disabled', false); $btn.removeClass('opacity-50 cursor-not-allowed');
    } else {
        $btn.html(`<i class="fas fa-trash-alt mr-2"></i>Delete`);
        $btn.prop('disabled', true); $btn.addClass('opacity-50 cursor-not-allowed');
    }
}