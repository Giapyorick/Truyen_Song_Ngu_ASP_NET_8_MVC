/* auth-modal.js */
$(document).ready(function () {
    // 1. Cấu hình Flatpickr & Select2
    if ($(".datepicker").length) {
        $(".datepicker").flatpickr({
            dateFormat: "Y-m-d",
            allowInput: true
        });
    }

    if ($('.select2-custom').length) {
        $('.select2-custom').select2({
            width: '100%',
            minimumResultsForSearch: Infinity,
            templateResult: formatGenderBadge,
            templateSelection: formatGenderBadge
        });
    }

    // 2. Submit form Login bằng AJAX
    $('#loginForm').on('submit', function (e) {
        e.preventDefault();

        const $btn = $(this).find('button[type="submit"]');
        const $error = $('#loginError');

        $btn.prop('disabled', true).addClass('opacity-60'); $btn.find('.btn-text').text('Đang đăng nhập...');
        $error.hide();

        $.ajax({
            url: '/Login/Login',
            type: 'POST',
            data: {
                Email: $('#loginEmail').val(),
                Passwork: $('#loginPassword').val()
            },
            success: function (res) {
                if (res.success) {
                    closeLoginModal();
                    // Tải lại trang hiện tại để cập nhật header/session ngay lập tức
                    window.location.reload();
                } else {
                    $error.text(res.message).show();
                }
            },
            error: function () {
                $error.text('Có lỗi xảy ra, vui lòng thử lại sau!').show();
            },
            complete: function () {
                $btn.prop('disabled', false).removeClass('opacity-60'); $btn.find('.btn-text').text('Đăng Nhập');
            }
        });
    });

    // 3. Submit form Đăng ký bằng AJAX
    $('#registerUserForm').on('submit', function (e) {
        e.preventDefault();

        const $btn = $(this).find('button[type="submit"]');
        const formData = new FormData(this);

        $btn.prop('disabled', true).text('Đang tạo tài khoản...');

        $.ajax({
            url: '/Home/Register',
            type: 'POST',
            data: formData,
            contentType: false,
            processData: false,
            success: function (res) {
                if (res.success) {
                    alert('Đăng ký thành công! Hãy đăng nhập ngay.');
                    closeRegisterModal();
                    openLoginModal();
                } else {
                    alert(res.message || 'Lỗi đăng ký');
                }
            },
            error: function () {
                alert('Lỗi kết nối máy chủ!');
            },
            complete: function () {
                $btn.prop('disabled', false).text('Tạo Tài Khoản');
            }
        });
    });
});

// Format badge màu sắc cho Gender Select2
function formatGenderBadge(state) {
    if (!state.id) return state.text;
    const gradient = $(state.element).data('color') || 'linear-gradient(135deg, #667eea, #764ba2)';
    return $(`
        <span style="display: flex; align-items: center;">
            <span style="display:inline-block; width:8px; height:8px; border-radius:50%; background:${gradient}; margin-right:8px;"></span>
            <span style="background:${gradient}; -webkit-background-clip: text; -webkit-text-fill-color: transparent; font-weight:700;">${state.text}</span>
        </span>
    `);
}

// Bật/tắt Modal Login
function openLoginModal() {
    closeRegisterModal();
    const $overlay = $('#loginModalOverlay');
    const $content = $('#loginModalContent');
    $('#loginError').hide();

    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => {
        $content.removeClass('translate-y-12 opacity-0 scale-95').addClass('translate-y-0 opacity-100 scale-100');
    }, 10);
}

function closeLoginModal() {
    const $overlay = $('#loginModalOverlay');
    const $content = $('#loginModalContent');

    $content.removeClass('translate-y-0 opacity-100 scale-100').addClass('translate-y-12 opacity-0 scale-95');
    setTimeout(() => {
        $overlay.removeClass('flex').addClass('hidden');
    }, 250);
}

// Bật/tắt Modal Register
function openRegisterModal() {
    closeLoginModal();
    const $overlay = $('#registerModalOverlay');
    const $content = $('#registerModalContent');
    const $form = $('#registerUserForm');

    $form[0].reset(); $('#regUserId').val(0);
    $('#imgPreview').attr('src', '').addClass('hidden');
    $('#uploadIcon').removeClass('hidden');

    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => {
        $content.removeClass('translate-y-12 opacity-0 scale-95').addClass('translate-y-0 opacity-100 scale-100');
    }, 10);

    if ($('.select2-custom').length) {
        $('.select2-custom').val(null).trigger('change');
    }
}

function closeRegisterModal() {
    const $overlay = $('#registerModalOverlay');
    const $content = $('#registerModalContent');

    $content.removeClass('translate-y-0 opacity-100 scale-100').addClass('translate-y-12 opacity-0 scale-95');
    setTimeout(() => {
        $overlay.removeClass('flex').addClass('hidden');
    }, 250);
}

// Preview ảnh upload khi chọn file
function previewImage(input) {
    if (!input.files || !input.files[0]) return;
    const reader = new FileReader();
    reader.onload = function (e) {
        $('#imgPreview').attr('src', e.target.result).removeClass('hidden');
        $('#uploadIcon').addClass('hidden');
    };
    reader.readAsDataURL(input.files[0]);
}