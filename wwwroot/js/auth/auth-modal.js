/* auth-modal.js - Xử lý đóng/mở và gửi dữ liệu Modal Đăng nhập / Đăng ký */

$(document).ready(function () {
    // Khởi tạo Flatpickr cho ô chọn ngày sinh
    if (typeof flatpickr !== 'undefined') {
        $(".datepicker").flatpickr({
            dateFormat: "Y-m-d",
            allowInput: true
        });
    }

    // Submit Form Đăng ký
    $('#registerForm').on('submit', function (e) {
        e.preventDefault();

        const pass = $('#regPassword').val();
        const confirmPass = $('#regConfirmPassword').val();

        if (pass.length < 6) {
            showToast('Password must be at least 6 characters!', 'error');
            return;
        }

        if (pass !== confirmPass) {
            showToast('Confirm password does not match!', 'error');
            return;
        }

        const formData = new FormData(this);
        const $btn = $('#btnRegisterSubmit');

        $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-2"></i> Registering...');

        $.ajax({
            url: '/Login/Register', // Action xử lý đăng ký trong LoginController
            type: 'POST',
            data: formData,
            contentType: false,
            processData: false,
            success: function (res) {
                if (res.success) {
                    showToast(res.message || 'Registration successful!', 'success');
                    closeRegisterModal();
                    // Chuyển sang mở modal đăng nhập sau khi đăng ký thành công
                    setTimeout(() => openLoginModal(), 400);
                } else {
                    showToast(res.message || 'Registration failed!', 'error');
                }
            },
            error: function (xhr) {
                const msg = xhr.responseJSON ? xhr.responseJSON.message : 'Connection error!';
                showToast(msg, 'error');
            },
            complete: function () {
                $btn.prop('disabled', false).html('Register Account');
            }
        });
    });

    // Submit Form Đăng nhập
    $('#loginForm').on('submit', function (e) {
        e.preventDefault();
        const $btn = $('#btnLoginSubmit');
        const formData = $(this).serialize();

        $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-2"></i> Signing in...');

        $.ajax({
            url: '/Login/CheckLogin',
            type: 'POST',
            data: formData,
            success: function (res) {
                if (res.success) {
                    showToast('Logged in successfully!', 'success');
                    window.location.reload();
                } else {
                    showToast(res.message || 'Invalid email or password!', 'error');
                }
            },
            error: function () {
                showToast('Server error while authenticating!', 'error');
            },
            complete: function () {
                $btn.prop('disabled', false).html('Sign In');
            }
        });
    });
});

// Điều khiển Modal Đăng nhập
function openLoginModal() {
    closeRegisterModal();
    const $overlay = $('#loginModalOverlay');
    const $content = $('#loginModalContent');

    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 10);
}

function closeLoginModal() {
    const $overlay = $('#loginModalOverlay');
    const $content = $('#loginModalContent');

    $content.removeClass('translate-y-0 opacity-100').addClass('translate-y-10 opacity-0');
    setTimeout(() => $overlay.removeClass('flex').addClass('hidden'), 300);
}

// Điều khiển Modal Đăng ký
function openRegisterModal() {
    closeLoginModal();
    const $overlay = $('#registerModalOverlay');
    const $content = $('#registerModalContent');

    $('#registerForm')[0].reset();
    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 10);
}

function closeRegisterModal() {
    const $overlay = $('#registerModalOverlay');
    const $content = $('#registerModalContent');

    $content.removeClass('translate-y-0 opacity-100').addClass('translate-y-10 opacity-0');
    setTimeout(() => $overlay.removeClass('flex').addClass('hidden'), 300);
}

// Chuyển đổi qua lại giữa 2 modal
function switchToRegisterModal() {
    closeLoginModal();
    setTimeout(() => openRegisterModal(), 200);
}

function switchToLoginModal() {
    closeRegisterModal();
    setTimeout(() => openLoginModal(), 200);
}
// Hàm xem trước Avatar khi người dùng chọn file
function previewRegisterAvatar(input) {
    if (input.files && input.files[0]) {
        const file = input.files[0];

        // Kiểm tra dung lượng (tối đa 2MB)
        if (file.size > 2 * 1024 * 1024) {
            showToast('Avatar image size must be less than 2MB!', 'error');
            input.value = '';
            return;
        }

        const reader = new FileReader();
        reader.onload = function (e) {
            $('#regAvatarPreview').attr('src', e.target.result).removeClass('hidden');
            $('#regAvatarPlaceholder').addClass('hidden');
        };
        reader.readAsDataURL(file);
    }
}

// Cập nhật hàm mở Modal Đăng ký để reset ảnh đại diện
function openRegisterModal() {
    closeLoginModal();
    const $overlay = $('#registerModalOverlay');
    const $content = $('#registerModalContent');

    // Reset sạch dữ liệu form và ảnh preview cũ
    $('#registerForm')[0].reset();
    $('#regAvatarInput').val('');
    $('#regAvatarPreview').attr('src', '').addClass('hidden');
    $('#regAvatarPlaceholder').removeClass('hidden');

    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 10);
}
function openForgotModal() {
    closeLoginModal();
    closeRegisterModal();
    const $overlay = $('#forgotModalOverlay');
    const $content = $('#forgotModalContent');
    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 10);
}

function closeForgotModal() {
    const $overlay = $('#forgotModalOverlay');
    const $content = $('#forgotModalContent');
    $content.removeClass('translate-y-0 opacity-100').addClass('translate-y-10 opacity-0');
    setTimeout(() => $overlay.removeClass('flex').addClass('hidden'), 300);
}

function switchToForgotModal() {
    closeLoginModal();
    setTimeout(() => openForgotModal(), 200);
}

// Xử lý gửi Form Quên mật khẩu
$('#forgotForm').off('submit').on('submit', function (e) {
    e.preventDefault();
    const $btn = $('#btnForgotSubmit');
    const email = $('#forgotEmail').val().trim();

    if (!email) {
        showToast('Please enter your email!', 'error');
        return;
    }

    $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-2"></i> Sending...');

    $.ajax({
        url: '/Login/ForgotPassword',
        type: 'POST',
        data: { email: email },
        success: function (res) {
            if (res.success) {
                showToast(res.message, 'success', 6000);
                closeForgotModal();
                setTimeout(() => openLoginModal(), 400);
            } else {
                showToast(res.message || 'Error processing request!', 'error');
            }
        },
        error: function (xhr) {
            let errorMsg = 'Server connection error!';
            if (xhr.responseJSON && xhr.responseJSON.message) {
                errorMsg = xhr.responseJSON.message;
            } else if (xhr.responseText) {
                console.error("Server crash details:", xhr.responseText);
            }
            showToast(errorMsg, 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).html('Send Temporary Password');
        }
    });
});