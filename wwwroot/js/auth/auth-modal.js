/* auth-modal.js - Xử lý đóng/mở và gửi dữ liệu Modal Đăng nhập / Đăng ký / Quên MK / Đổi MK */

$(document).ready(function () {
    // Khởi tạo Flatpickr cho ô chọn ngày sinh
    if (typeof flatpickr !== 'undefined') {
        $(".datepicker").flatpickr({
            dateFormat: "Y-m-d",
            allowInput: true
        });
    }

    // 1. Submit Form Đăng ký
    $('#registerForm').off('submit').on('submit', function (e) {
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
            url: '/Login/Register',
            type: 'POST',
            data: formData,
            contentType: false,
            processData: false,
            success: function (res) {
                if (res.success) {
                    showToast(res.message || 'Registration successful!', 'success');
                    closeRegisterModal();
                    setTimeout(() => openLoginModal(), 400);
                } else {
                    showToast(res.message || 'Registration failed!', 'error');
                    refreshRegisterCaptcha();
                }
            },
            error: function (xhr) {
                const msg = xhr.responseJSON ? xhr.responseJSON.message : 'Connection error!';
                showToast(msg, 'error');
                refreshRegisterCaptcha();
            },
            complete: function () {
                $btn.prop('disabled', false).html('Register Account');
            }
        });
    });

    // 2. Submit Form Đăng nhập
    $('#loginForm').off('submit').on('submit', function (e) {
        e.preventDefault();
        const $btn = $('#btnLoginSubmit');
        const formData = $(this).serialize();

        $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-2"></i> Signing in...');

        $.ajax({
            url: '/Login/CheckLogin',
            type: 'POST',
            xhrFields: { withCredentials: true },
            data: formData,
            success: function (res) {
                if (res.success) {
                    if (res.requireChangePassword === true || res.requireChangePassword === "true") {
                        closeLoginModal();
                        openForceChangePasswordModal();
                        return;
                    }

                    showToast('Logged in successfully!', 'success');
                    setTimeout(() => {
                        if (res.redirectUrl) {
                            window.location.href = res.redirectUrl;
                        } else {
                            window.location.reload();
                        }
                    }, 500);
                } else {
                    showToast(res.message || 'Invalid email or password!', 'error');
                    refreshLoginCaptcha(); 
                }
            },
            error: function () {
                showToast('Server error while authenticating!', 'error');
                refreshLoginCaptcha();
            },
            complete: function () {
                $btn.prop('disabled', false).html('Sign In');
            }
        });
    });

    // 3. Submit Form Đổi mật khẩu bắt buộc
    $('#forceChangePasswordForm').off('submit').on('submit', function (e) {
        e.preventDefault();

        const newPass = $('#forceNewPassword').val().trim();
        const confirmPass = $('#forceConfirmPassword').val().trim();
        const $err = $('#forceChangePasswordError');
        const $btn = $('#btnForceChangePasswordSubmit');

        if (newPass.length < 6) {
            $err.removeClass('hidden').text('Password must be at least 6 characters!');
            return;
        }

        if (newPass !== confirmPass) {
            $err.removeClass('hidden').text('Confirm password does not match!');
            return;
        }

        $btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin mr-1"></i> Updating...');

        $.ajax({
            url: '/Login/ChangePassword',
            type: 'POST',
            xhrFields: { withCredentials: true },
            data: {
                newPassword: newPass,
                confirmPassword: confirmPass
            },
            success: function (res) {
                if (res.success) {
                    showToast('Password updated successfully! Welcome back.', 'success');
                    setTimeout(() => {
                        window.location.reload();
                    }, 800);
                } else {
                    $err.removeClass('hidden').text(res.message);
                    $btn.prop('disabled', false).text('Update Password & Proceed');
                }
            },
            error: function () {
                $err.removeClass('hidden').text('System error occurred. Please try again!');
                $btn.prop('disabled', false).text('Update Password & Proceed');
            }
        });
    });

    // 4. Submit Form Quên mật khẩu
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
                }
                showToast(errorMsg, 'error');
            },
            complete: function () {
                $btn.prop('disabled', false).html('Send Temporary Password');
            }
        });
    });
});

// Điều khiển Modal Đăng nhập (Gán trực tiếp vào window để dùng toàn cục)
window.openLoginModal = function () {
    closeRegisterModal();
    closeForgotModal();
    refreshLoginCaptcha();
    const $overlay = $('#loginModalOverlay');
    const $content = $('#loginModalContent');

    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 10);
};

window.closeLoginModal = function () {
    const $overlay = $('#loginModalOverlay');
    const $content = $('#loginModalContent');

    $content.removeClass('translate-y-0 opacity-100').addClass('translate-y-10 opacity-0');
    setTimeout(() => $overlay.removeClass('flex').addClass('hidden'), 300);
};

// Điều khiển Modal Đăng ký
window.openRegisterModal = function () {
    closeLoginModal();
    closeForgotModal();
    refreshRegisterCaptcha();
    const $overlay = $('#registerModalOverlay');
    const $content = $('#registerModalContent');

    $('#registerForm')[0].reset();
    $('#regAvatarInput').val('');
    $('#regAvatarPreview').attr('src', '').addClass('hidden');
    $('#regAvatarPlaceholder').removeClass('hidden');

    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 10);
};

window.closeRegisterModal = function () {
    const $overlay = $('#registerModalOverlay');
    const $content = $('#registerModalContent');

    $content.removeClass('translate-y-0 opacity-100').addClass('translate-y-10 opacity-0');
    setTimeout(() => $overlay.removeClass('flex').addClass('hidden'), 300);
};

// Điều khiển Modal Quên mật khẩu
window.openForgotModal = function () {
    closeLoginModal();
    closeRegisterModal();
    const $overlay = $('#forgotModalOverlay');
    const $content = $('#forgotModalContent');

    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 10);
};

window.closeForgotModal = function () {
    const $overlay = $('#forgotModalOverlay');
    const $content = $('#forgotModalContent');

    $content.removeClass('translate-y-0 opacity-100').addClass('translate-y-10 opacity-0');
    setTimeout(() => $overlay.removeClass('flex').addClass('hidden'), 300);
};

// Điều khiển Modal Buộc đổi mật khẩu tạm
window.openForceChangePasswordModal = function () {
    closeLoginModal();
    closeRegisterModal();
    closeForgotModal();

    const $overlay = $('#forceChangePasswordModalOverlay');
    const $content = $('#forceChangePasswordModalContent');

    if ($('#forceChangePasswordForm').length) {
        $('#forceChangePasswordForm')[0].reset();
    }
    $('#forceChangePasswordError').addClass('hidden').text('');

    $overlay.off('click').on('click', function (e) {
        if (e.target === this) {
            e.stopPropagation();
        }
    });

    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 10);
};

// Chuyển đổi qua lại giữa các Modal
window.switchToRegisterModal = function () {
    closeLoginModal();
    setTimeout(() => openRegisterModal(), 200);
};

window.switchToLoginModal = function () {
    closeRegisterModal();
    closeForgotModal();
    setTimeout(() => openLoginModal(), 200);
};

window.switchToForgotModal = function () {
    closeLoginModal();
    setTimeout(() => openForgotModal(), 200);
};

// Preview avatar đăng ký
window.previewRegisterAvatar = function (input) {
    if (input.files && input.files[0]) {
        const file = input.files[0];
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
};

window.refreshLoginCaptcha = function () {
    $.get('/Login/GetLoginCaptcha', function (res) {
        $('#loginCaptchaQuestion').text(res.question);
        $('#loginCaptchaInput').val('');
    });
};

window.refreshRegisterCaptcha = function () {
    $.get('/Login/GetRegisterCaptcha', function (res) {
        $('#registerCaptchaQuestion').text(res.question);
        $('#registerCaptchaInput').val('');
    });
};