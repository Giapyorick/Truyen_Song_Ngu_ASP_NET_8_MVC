/* authors-ui.js - Quản lý giao diện, hiệu ứng và tương tác DOM */

$(document).ready(function () {
    // 1. Khởi tạo Flatpickr
    $(".datepicker").flatpickr({
        dateFormat: "Y-m-d",
        allowInput: true
    });

    // 2. Khởi tạo Select2 Custom màu gradient
    $('.select2-custom').each(function () {
        $(this).select2({
            width: '100%',
            minimumResultsForSearch: Infinity,
            templateResult: formatSelectState,
            templateSelection: formatSelectState
        });
    });

    // 3. Load danh sách quốc gia
    $.get('/data/countries.txt', function (data) {
        const lines = data.split('\n');
        let options = '<option value="Unknow">Select country</option>';
        lines.forEach(c => {
            c = c.trim();
            if (c !== '') options += `<option value="${c}">${c}</option>`;
        });
        $('#authorCountry').html(options).select2({
            allowClear: true,
            width: '100%'
        });
    });

    $('.select2-custom').not('#authorCountry').each(function () {
        $(this).select2({
            width: '100%',
            minimumResultsForSearch: Infinity,
            templateResult: formatSelectState,
            templateSelection: formatSelectState
        });
    });

    // 2. Nạp danh sách quốc gia bất đồng bộ
    loadCountries();
});
function loadCountries(selectedValue = "Unknow") {
    $.get('/data/countries.txt', function (data) {
        const lines = data.split('\n');
        let options = '<option value="Unknow">Select country</option>';

        lines.forEach(c => {
            const country = c.trim();
            if (country !== '') {
                options += `<option value="${country}">${country}</option>`;
            }
        });

        const $select = $('#authorCountry');
        $select.html(options);

        // Hủy khởi tạo cũ nếu đã có
        if ($select.hasClass("select2-hidden-accessible")) {
            $select.select2('destroy');
        }

        // Khởi tạo Select2 cho Country với dropdownParent để không bị modal che
        $select.select2({
            width: '100%',
            allowClear: false,
            dropdownParent: $('#modalOverlay'), // Cực kỳ quan trọng khi nằm trong Modal
            placeholder: 'Select country'
        });

        $select.val(selectedValue).trigger('change');
    }).fail(function () {
        console.error("Không thể tải file /data/countries.txt");
    });
}

function formatSelectState(state) {
    if (!state.id) return state.text;
    const gradient = $(state.element).data('color') || 'linear-gradient(135deg, #667eea, #764ba2)';
    return $(`
        <span style="display: flex; align-items: center;">
            <span style="display:inline-block; width:10px; height:10px; border-radius:50%; background:${gradient}; margin-right:10px;"></span>
            <span style="background:${gradient}; -webkit-background-clip: text; -webkit-text-fill-color: transparent; font-weight:700;">${state.text}</span>
        </span>
    `);
}

// Preview ảnh khi chọn file ở form Add/Edit
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

// Modal zoom ảnh lớn
function openImagePreview(src) {
    if (!src) return;
    $('#imageModalContent').attr('src', src);
    $('#imageModal').removeClass('hidden').addClass('flex');
}

function closeImagePreview() {
    $('#imageModal').addClass('hidden').removeClass('flex');
}

// Bật tắt Import Modal
function openImportModal() {
    const modal = $('#importModal');
    modal.removeClass('hidden').addClass('flex');
    setTimeout(() => $('#importContent').addClass('scale-100 opacity-100'), 10);
}

function closeImportModal() {
    $('#importContent').removeClass('scale-100 opacity-100');
    setTimeout(() => {
        $('#importModal').removeClass('flex').addClass('hidden');
        $('#excelFile').val('');
        $('#fileStatus').text('Kéo thả file vào đây hoặc click để chọn');
        $('#dropZone').removeClass('border-indigo-500 bg-indigo-100/50');
    }, 300);
}

// Cập nhật text nút Delete(N)
function updateDeleteButton() {
    const count = $('.user-checkbox:checked').length;
    const $btn = $('#btnDeleteSelected');
    if (count > 0) {
        $btn.html(`<i class="fas fa-trash-alt mr-2"></i>Delete(${count})`).prop('disabled', false).removeClass('opacity-50 cursor-not-allowed');
    } else {
        $btn.html(`<i class="fas fa-trash-alt mr-2"></i>Delete`).prop('disabled', true).addClass('opacity-50 cursor-not-allowed');
    }
}