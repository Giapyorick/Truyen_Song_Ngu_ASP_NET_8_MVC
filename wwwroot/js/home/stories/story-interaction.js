/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/stories/story-interaction.js
 *  CHỨC NĂNG : Trang chi tiết truyện: đánh giá sao, thích, theo dõi truyện
 *  DÙNG TẠI  : Views/Stories/Detail.cshtml (@section Scripts)
 *  PHỤ THUỘC : jQuery, showToast() (_Layout.cshtml); đọc data-userrating / data-current của #starRatingGroup
 *  THỨ TỰ    : Không phụ thuộc file JS nào khác
 * ----------------------------------------------------------------------------
 *  MỤC LỤC
 *    1. BIẾN ĐÁNH GIÁ SAO (điểm của tôi / điểm trung bình)
 *    2. KHỞI TẠO & HIỆU ỨNG SAO (tô sao, rê chuột xem trước)
 *    3. THÍCH / ĐÁNH GIÁ / THEO DÕI TRUYỆN (gọi AJAX)
 * ============================================================================
 */

// ============================================================================
// 1. BIẾN ĐÁNH GIÁ SAO (điểm của tôi / điểm trung bình)
// ============================================================================
let myRating = parseInt($('#starRatingGroup').data('userrating')) || 0;
let avgRating = parseInt($('#starRatingGroup').data('current')) || 0;
let currentRatingValue = myRating > 0 ? myRating : avgRating;

// ============================================================================
// 2. KHỞI TẠO & HIỆU ỨNG SAO (tô sao, rê chuột xem trước)
// ============================================================================
$(document).ready(function () {
    // 1. Lắp đầy số sao đã lưu khi tải trang
    highlightStars(currentRatingValue);

    // 2. Hiệu ứng rê chuột xem thử
    $('#starRatingGroup .star-item').hover(
        function () {
            const hoverVal = parseInt($(this).data('value'));
            highlightStars(hoverVal);
        },
        function () {
            // Rê chuột ra ngoài: trả lại số sao đã lưu
            highlightStars(currentRatingValue);
        }
    );
});

function highlightStars(rating) {
    $('#starRatingGroup .star-item').each(function () {
        const starVal = parseInt($(this).data('value'));
        if (starVal <= rating) {
            $(this).removeClass('fa-regular').addClass('fa-solid');
        } else {
            $(this).removeClass('fa-solid').addClass('fa-regular');
        }
    });
}

// ============================================================================
// 3. THÍCH / ĐÁNH GIÁ / THEO DÕI TRUYỆN (gọi AJAX)
// ============================================================================
function handleLike(storyId) {
    $.ajax({
        url: '/Stories/ToggleLike',
        type: 'POST',
        data: { storyId: storyId },
        success: function (res) {
            if (res.requireLogin) {
                if (typeof openLoginModal === 'function') openLoginModal();
                else showToast(res.message, 'error');
                return;
            }

            if (res.success) {
                $('#likesCount').text(res.likesCount);

                if (res.isLiked) {
                    $('#cardLike').addClass('active');
                    if (window.storyIcons) window.storyIcons.setLike(true); // icon biến hình: story-icons.js
                    $('#labelLike').text($('#labelLike').data('on') || 'Liked');
                    $('#cardLike').attr('aria-pressed', 'true');
                } else {
                    $('#cardLike').removeClass('active');
                    if (window.storyIcons) window.storyIcons.setLike(false);
                    $('#labelLike').text($('#labelLike').data('off') || 'Like');
                    $('#cardLike').attr('aria-pressed', 'false');
                }
            } else {
                if (typeof showToast === 'function') showToast(res.message, 'error');
            }
        },
        error: function (xhr) {
            console.error("Error in ToggleLike:", xhr.responseText);
            const msg = xhr.responseJSON ? xhr.responseJSON.message : 'Error sending like!';
            if (typeof showToast === 'function') showToast(msg, 'error');
        }
    });
}

function handleRate(storyId, rating) {
    $.ajax({
        url: '/Stories/RateStory',
        type: 'POST',
        data: { storyId: storyId, rating: rating },
        success: function (res) {
            if (res.requireLogin) {
                if (typeof openLoginModal === 'function') openLoginModal();
                else showToast(res.message,'error');
                return;
            }

            if (res.success) {
                const roundedRate = Math.round(parseFloat(res.averageRate));
                currentRatingValue = res.userRating;

                $('#avgRate').text(roundedRate);
                $('#countRate').text(res.countRate);
                highlightStars(res.userRating);

                if (typeof showToast === 'function') showToast(res.message, 'success');
            } else {
                if (typeof showToast === 'function') showToast(res.message, 'error');
            }
        },
        error: function (xhr) {
            console.error("Lỗi RateStory:", xhr.responseText);
            const msg = xhr.responseJSON ? xhr.responseJSON.message : 'Lỗi khi gửi đánh giá!';
            if (typeof showToast === 'function') showToast(msg, 'error');
        }
    });
}

function handleFollow(storyId) {
    $.ajax({
        url: '/Stories/ToggleFollow',
        type: 'POST',
        data: { storyId: storyId },
        success: function (res) {
            if (res.requireLogin) {
                if (typeof openLoginModal === 'function') openLoginModal();
                else showToast(res.message, 'error');
                return;
            }

            if (res.success) {
                $('#followersCount').text(res.followersCount);

                if (res.isFollowed) {
                    $('#cardFollow').addClass('active');
                    if (window.storyIcons) window.storyIcons.setFollow(true);
                    $('#labelFollow').text($('#labelFollow').data('on') || 'Following');
                    $('#cardFollow').attr('aria-pressed', 'true');
                } else {
                    $('#cardFollow').removeClass('active');
                    if (window.storyIcons) window.storyIcons.setFollow(false);
                    $('#labelFollow').text($('#labelFollow').data('off') || 'Follow');
                    $('#cardFollow').attr('aria-pressed', 'false');
                }
            }
        },
        error: function () {
            if (typeof showToast === 'function') showToast('Lỗi khi cập nhật theo dõi!', 'error');
        }
    });
}
