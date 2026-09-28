let myRating = parseInt($('#starRatingGroup').data('userrating')) || 0;
let avgRating = parseInt($('#starRatingGroup').data('current')) || 0;
let currentRatingValue = myRating > 0 ? myRating : avgRating;

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

function handleLike(storyId) {
    $.ajax({
        url: '/Stories/ToggleLike',
        type: 'POST',
        data: { storyId: storyId },
        success: function (res) {
            if (res.requireLogin) {
                if (typeof openLoginModal === 'function') openLoginModal();
                else alert(res.message);
                return;
            }

            if (res.success) {
                $('#likesCount').text(res.likesCount);

                if (res.isLiked) {
                    $('#cardLike').addClass('active');
                    $('#iconLike').removeClass('fa-regular').addClass('fa-solid');
                    $('#labelLike').text('Liked');
                } else {
                    $('#cardLike').removeClass('active');
                    $('#iconLike').removeClass('fa-solid').addClass('fa-regular');
                    $('#labelLike').text('Like');
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
                else alert(res.message);
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
                else alert(res.message);
                return;
            }

            if (res.success) {
                $('#followersCount').text(res.followersCount);

                if (res.isFollowed) {
                    $('#cardFollow').addClass('active');
                    $('#iconFollow').removeClass('fa-regular').addClass('fa-solid');
                    $('#labelFollow').text('Đang theo dõi');
                } else {
                    $('#cardFollow').removeClass('active');
                    $('#iconFollow').removeClass('fa-solid').addClass('fa-regular');
                    $('#labelFollow').text('Theo dõi');
                }
            }
        },
        error: function () {
            if (typeof showToast === 'function') showToast('Lỗi khi cập nhật theo dõi!', 'error');
        }
    });
}