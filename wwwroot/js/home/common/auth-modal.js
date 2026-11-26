/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/common/auth-modal.js
 *  CHỨC NĂNG : Modal Đăng nhập / Đăng ký / Quên mật khẩu / Đổi mật khẩu bắt buộc (song ngữ VI/EN)
 *  DÙNG TẠI  : Views/Shared/_Layout.cshtml (mọi trang) – HTML modal ở Views/Shared/_AuthModalsPartial.cshtml
 *  PHỤ THUỘC : jQuery, Flatpickr, showToast() (_Layout.cshtml), window.AUTH_LANG (_AuthModalsPartial), CURRENT_LANG (nếu có)
 *  THỨ TỰ    : Nạp trong _Layout sau jQuery / Flatpickr / Select2
 * ----------------------------------------------------------------------------
 *  MỤC LỤC
 *    1. KHỞI TẠO & GỬI FORM – đăng ký, đăng nhập, đổi mật khẩu bắt buộc, quên mật khẩu
 *    2. ĐÓNG / MỞ & CHUYỂN QUA LẠI GIỮA CÁC MODAL (gán vào window để gọi từ onclick)
 *    3. TIỆN ÍCH – xem trước avatar đăng ký, làm mới captcha
 * ============================================================================
 */

// ============================================================================
// 1. KHỞI TẠO & GỬI FORM – đăng ký, đăng nhập, đổi mật khẩu bắt buộc, quên mật khẩu
// ============================================================================
$(document).ready(function () {
    // Hàm trợ giúp kiểm tra ngôn ngữ hiện tại (Default: "vi")
    function isEnglish() {
        if (typeof CURRENT_LANG !== 'undefined') {
            return CURRENT_LANG === 'en';
        }
        return document.cookie.includes(".AspNetCore.Culture=c%3Den") || document.cookie.includes(".AspNetCore.Culture=c=en");
    }

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
        const isEn = isEnglish();

        const pass = $('#regPassword').val();
        const confirmPass = $('#regConfirmPassword').val();

        if (pass.length < 6) {
            showToast(isEn ? 'Password must be at least 6 characters!' : 'Mật khẩu phải có ít nhất 6 ký tự!', 'error');
            return;
        }

        if (pass !== confirmPass) {
            showToast(isEn ? 'Confirm password does not match!' : 'Mật khẩu xác nhận không trùng khớp!', 'error');
            return;
        }

        const formData = new FormData(this);
        const $btn =$('#btnRegisterSubmit');

        $btn.prop('disabled', true).html(`<i class="fas fa-spinner fa-spin mr-2"></i> ${isEn ? 'Registering...' : 'Đang đăng ký...'}`);

        $.ajax({
            url: '/Login/Register',
            type: 'POST',
            data: formData,
            contentType: false,
            processData: false,
            success: function (res) {
                if (res.success) {
                    showToast(res.message || (isEn ? 'Registration successful!' : 'Đăng ký tài khoản thành công!'), 'success');
                    closeRegisterModal();
                    setTimeout(() => openLoginModal(), 400);
                } else {
                    showToast(res.message || (isEn ? 'Registration failed!' : 'Đăng ký thất bại!'), 'error');
                    refreshRegisterCaptcha();
                }
            },
            error: function (xhr) {
                const msg = xhr.responseJSON ? xhr.responseJSON.message : (isEn ? 'Connection error!' : 'Lỗi kết nối máy chủ!');
                showToast(msg, 'error');
                refreshRegisterCaptcha();
            },
            complete: function () {
                $btn.prop('disabled', false).html(isEn ? 'Register Account' : 'Đăng ký tài khoản');
            }
        });
    });

    // 2. Submit Form Đăng nhập
    $('#loginForm').off('submit').on('submit', function (e) {
        e.preventDefault();
        const isEn = isEnglish();
        const $btn =$('#btnLoginSubmit');
        const formData = $(this).serialize();

        $btn.prop('disabled', true).html(`<i class="fas fa-spinner fa-spin mr-2"></i> ${isEn ? 'Signing in...' : 'Đang đăng nhập...'}`);

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

                    showToast(isEn ? 'Logged in successfully!' : 'Đăng nhập thành công!', 'success');
                    setTimeout(() => {
                        if (res.redirectUrl) {
                            window.location.href = res.redirectUrl;
                        } else {
                            window.location.reload();
                        }
                    }, 500);
                } else {
                    showToast(res.message || (isEn ? 'Invalid email or password!' : 'Email hoặc mật khẩu không chính xác!'), 'error');
                    refreshLoginCaptcha(); 
                }
            },
            error: function () {
                showToast(isEn ? 'Server error while authenticating!' : 'Lỗi máy chủ trong quá trình xác thực!', 'error');
                refreshLoginCaptcha();
            },
            complete: function () {
                $btn.prop('disabled', false).html(isEn ? 'Sign In' : 'Đăng nhập');
            }
        });
    });

    // 3. Submit Form Đổi mật khẩu bắt buộc
    $('#forceChangePasswordForm').off('submit').on('submit', function (e) {
        e.preventDefault();
        const isEn = isEnglish();

        const newPass = $('#forceNewPassword').val().trim();
        const confirmPass = $('#forceConfirmPassword').val().trim();
        const $err =$('#forceChangePasswordError');
        const $btn =$('#btnForceChangePasswordSubmit');

        if (newPass.length < 6) {
            $err.removeClass('hidden').text(isEn ? 'Password must be at least 6 characters!' : 'Mật khẩu phải có ít nhất 6 ký tự!');
            return;
        }

        if (newPass !== confirmPass) {
            $err.removeClass('hidden').text(isEn ? 'Confirm password does not match!' : 'Mật khẩu xác nhận không khớp!');
            return;
        }

        $btn.prop('disabled', true).html(`<i class="fa-solid fa-spinner fa-spin mr-1"></i> ${isEn ? 'Updating...' : 'Đang cập nhật...'}`);

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
                    showToast(isEn ? 'Password updated successfully! Welcome back.' : 'Đổi mật khẩu thành công! Chào mừng bạn quay lại.', 'success');
                    setTimeout(() => {
                        window.location.reload();
                    }, 800);
                } else {
                    $err.removeClass('hidden').text(res.message);$btn.prop('disabled', false).text(isEn ? 'Update Password & Proceed' : 'Cập nhật mật khẩu & Tiếp tục');
                }
            },
            error: function () {
                $err.removeClass('hidden').text(isEn ? 'System error occurred. Please try again!' : 'Xảy ra lỗi hệ thống. Vui lòng thử lại!');
                $btn.prop('disabled', false).text(isEn ? 'Update Password & Proceed' : 'Cập nhật mật khẩu & Tiếp tục');
            }
        });
    });

    // 4. Submit Form Quên mật khẩu
    $('#forgotForm').off('submit').on('submit', function (e) {
        e.preventDefault();
        const isEn = isEnglish();
        const $btn =$('#btnForgotSubmit');
        const email = $('#forgotEmail').val().trim();

        if (!email) {
            showToast(isEn ? 'Please enter your email!' : 'Vui lòng nhập địa chỉ email!', 'error');
            return;
        }

        $btn.prop('disabled', true).html(`<i class="fas fa-spinner fa-spin mr-2"></i> ${isEn ? 'Sending...' : 'Đang gửi...'}`);

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
                    showToast(res.message || (isEn ? 'Error processing request!' : 'Lỗi trong quá trình xử lý yêu cầu!'), 'error');
                }
            },
            error: function (xhr) {
                let errorMsg = isEn ? 'Server connection error!' : 'Lỗi kết nối tới máy chủ!';
                if (xhr.responseJSON && xhr.responseJSON.message) {
                    errorMsg = xhr.responseJSON.message;
                }
                showToast(errorMsg, 'error');
            },
            complete: function () {
                $btn.prop('disabled', false).html(isEn ? 'Send Temporary Password' : 'Gửi mật khẩu tạm thời');
            }
        });
    });
});

// ============================================================================
// 2. ĐÓNG / MỞ & CHUYỂN QUA LẠI GIỮA CÁC MODAL (gán vào window để gọi từ onclick)
// ============================================================================
// Điều khiển Modal Đăng nhập (Gán trực tiếp vào window để dùng toàn cục)
window.openLoginModal = function () {
    closeRegisterModal();
    closeForgotModal();
    refreshLoginCaptcha();
    const $overlay =$('#loginModalOverlay');
    const $content =$('#loginModalContent');

    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 10);
};

window.closeLoginModal = function () {
    const $overlay =$('#loginModalOverlay');
    const $content =$('#loginModalContent');

    $content.removeClass('translate-y-0 opacity-100').addClass('translate-y-10 opacity-0');
    setTimeout(() => $overlay.removeClass('flex').addClass('hidden'), 300);
};

// Điều khiển Modal Đăng ký
window.openRegisterModal = function () {
    closeLoginModal();
    closeForgotModal();
    refreshRegisterCaptcha();
    const $overlay =$('#registerModalOverlay');
    const $content =$('#registerModalContent');

    $('#registerForm')[0].reset();
    $('#regAvatarInput').val('');
    $('#regAvatarPreview').attr('src', '').addClass('hidden');
    $('#regAvatarPlaceholder').removeClass('hidden');

    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 10);
};

window.closeRegisterModal = function () {
    const $overlay =$('#registerModalOverlay');
    const $content =$('#registerModalContent');

    $content.removeClass('translate-y-0 opacity-100').addClass('translate-y-10 opacity-0');
    setTimeout(() => $overlay.removeClass('flex').addClass('hidden'), 300);
};

// Điều khiển Modal Quên mật khẩu
window.openForgotModal = function () {
    closeLoginModal();
    closeRegisterModal();
    const $overlay =$('#forgotModalOverlay');
    const $content =$('#forgotModalContent');

    $overlay.removeClass('hidden').addClass('flex');
    setTimeout(() => $content.removeClass('translate-y-10 opacity-0').addClass('translate-y-0 opacity-100'), 10);
};

window.closeForgotModal = function () {
    const $overlay =$('#forgotModalOverlay');
    const $content =$('#forgotModalContent');

    $content.removeClass('translate-y-0 opacity-100').addClass('translate-y-10 opacity-0');
    setTimeout(() => $overlay.removeClass('flex').addClass('hidden'), 300);
};

// Điều khiển Modal Buộc đổi mật khẩu tạm
window.openForceChangePasswordModal = function () {
    closeLoginModal();
    closeRegisterModal();
    closeForgotModal();

    const $overlay =$('#forceChangePasswordModalOverlay');
    const $content =$('#forceChangePasswordModalContent');

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

// ============================================================================
// 3. TIỆN ÍCH – xem trước avatar đăng ký, làm mới captcha
// ============================================================================
// Preview avatar đăng ký
window.previewRegisterAvatar = function (input) {
    const isEn = (typeof CURRENT_LANG !== 'undefined' && CURRENT_LANG === 'en') || document.cookie.includes(".AspNetCore.Culture=c%3Den");
    if (input.files && input.files[0]) {
        const file = input.files[0];
        if (file.size > 2 * 1024 * 1024) {
            showToast(isEn ? 'Avatar image size must be less than 2MB!' : 'Dung lượng ảnh đại diện phải nhỏ hơn 2MB!', 'error');
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
