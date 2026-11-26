/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/community/community.js
 *  CHỨC NĂNG : Trang Cộng đồng: bài viết, bình luận, trả lời, thích, spoiler, hashtag, realtime
 *  DÙNG TẠI  : Views/Community/Index.cshtml
 *  PHỤ THUỘC : SignalR, showToast(), CURRENT_USER_ID, CURRENT_LANG, STORIES_OPTIONS (khai báo trong view)
 *  THỨ TỰ    : Nạp SAU đoạn <script> khai báo biến trong Community/Index.cshtml
 * ----------------------------------------------------------------------------
 *  MỤC LỤC
 *    1. BIẾN TOÀN CỤC (trang, tab, trạng thái tải)
 *    2. HIỂN THỊ NỘI DUNG (đính kèm, hashtag, spoiler)
 *    3. KHỞI CHẠY KHI TẢI TRANG
 *    4. TAB, BỘ LỌC & TẢI THÊM BÀI VIẾT
 *    5. RENDER DANH SÁCH BÀI VIẾT
 *    6. ĐĂNG BÀI (chọn truyện → chương, gửi bài)
 *    7. BÌNH LUẬN & TRẢ LỜI
 *    8. XÓA BÀI / XÓA BÌNH LUẬN / THÍCH BÀI
 *    9. REALTIME (SignalR) – cập nhật khi bài / bình luận bị xóa
 * ============================================================================
 */

// ============================================================================
// 1. BIẾN TOÀN CỤC (trang, tab, trạng thái tải)
// ============================================================================
let replyMap = {};
let currentPage = 1;
const pageSize = 10;
let isLoading = false;
let hasMore = true;
let currentTab = "latest";
let searchDebounce = null;

// ============================================================================
// 2. HIỂN THỊ NỘI DUNG (đính kèm, hashtag, spoiler)
// ============================================================================
function toggleCommentAttachment(postId) {
    const attachBox = document.getElementById(`comment-attach-box-${postId}`);
    if (attachBox) attachBox.classList.toggle("hidden");
}

function renderHashtagText(text) {
    if (!text) return "";
    return text.replace(/#([\wàáảãạâầấẩẫậăằắẳẵặèéẻẽẹêềếểễệđìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ]+)/gi, function (match) {
        return `<span onclick="searchByHashtag('${match}')" class="text-teal-600 font-bold hover:underline cursor-pointer bg-teal-50 px-1.5 py-0.5 rounded-md border border-teal-200/50 transition">${match}</span>`;
    });
}

function searchByHashtag(tag) {
    const searchInput = document.getElementById("communitySearchInput");
    if (searchInput) {
        searchInput.value = tag;
        applyFilters();
    }
}

function toggleSpoiler(btn) {
    const container = btn.closest(".spoiler-container");
    if (!container) return;

    const overlay = container.querySelector(".spoiler-overlay-layer");
    const contentText = container.querySelector(".spoiler-text-node");
    const hideBtn = container.querySelector(".btn-hide-spoiler");

    const isOverlayVisible = overlay && (overlay.style.display !== "none" && !overlay.classList.contains("hidden"));

    if (isOverlayVisible) {
        if (overlay) {
            overlay.classList.add("hidden");
            overlay.style.display = "none";
        }
        if (contentText) {
            contentText.classList.remove("spoiler-content-hidden");
        }
        if (hideBtn) {
            hideBtn.classList.remove("hidden");
            hideBtn.style.display = "inline-flex";
        }
    } else {
        if (overlay) {
            overlay.classList.remove("hidden");
            overlay.style.display = "flex";
        }
        if (contentText) {
            contentText.classList.add("spoiler-content-hidden");
        }
        if (hideBtn) {
            hideBtn.classList.add("hidden");
            hideBtn.style.display = "none";
        }
    }
}

function renderSpoilerContent(content, isSpoiler, isComment = false) {
    const formattedText = renderHashtagText(content);
    const alertText = isComment 
        ? (CURRENT_LANG === 'en' ? 'Spoiler Comment' : 'Bình luận Spoiler')
        : (CURRENT_LANG === 'en' ? 'Spoiler Content' : 'Nội dung Spoiler');
    const viewText = CURRENT_LANG === 'en' ? 'View' : 'Xem nội dung';
    const hideText = CURRENT_LANG === 'en' ? 'Hide spoiler' : 'Ẩn nội dung';

    if (!isSpoiler) {
        return `<p class="${isComment ? 'comment-content-text' : 'post-content-text'} text-slate-700 ${isComment ? 'text-xs pl-8' : 'text-xs sm:text-[13px]'} leading-relaxed whitespace-pre-wrap break-words">${formattedText}</p>`;
    }

    return `
        <div class="spoiler-container my-1 p-3 ${isComment ? 'ml-8' : ''}">
            <div class="spoiler-text-node spoiler-content-hidden">
                <p class="${isComment ? 'comment-content-text' : 'post-content-text'} text-slate-700 ${isComment ? 'text-xs' : 'text-xs sm:text-[13px]'} leading-relaxed whitespace-pre-wrap break-words">
                    ${formattedText}
                </p>
            </div>

            <div class="spoiler-overlay-layer">
                <span class="text-slate-700 font-bold text-xs flex items-center gap-2">
                    <i class="fa-solid fa-eye-slash text-teal-600"></i>
                    <span>${alertText}</span>
                </span>
                <button type="button" onclick="toggleSpoiler(this)" class="btn-grad px-3 py-1 text-white rounded-lg text-[11px] font-bold shadow-xs transition cursor-pointer shrink-0">
                    ${viewText}
                </button>
            </div>

            <button type="button" onclick="toggleSpoiler(this)" class="btn-hide-spoiler hidden">
                <i class="fa-solid fa-eye-slash text-[10px]"></i>
                <span>${hideText}</span>
            </button>
        </div>
    `;
}

// ============================================================================
// 3. KHỞI CHẠY KHI TẢI TRANG
// ============================================================================
document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll(".post-content-text, .comment-content-text").forEach(el => {
        if (el.textContent.includes("#")) {
            el.innerHTML = renderHashtagText(el.textContent);
        }
    });

    const trigger = document.getElementById("infiniteScrollTrigger");
    if (trigger) {
        const observer = new IntersectionObserver((entries) => {
            if (entries[0].isIntersecting && !isLoading && hasMore) {
                loadMorePosts();
            }
        }, { threshold: 0.1 });

        observer.observe(trigger);
    }

    document.getElementById("communitySearchInput")?.addEventListener("input", function () {
        clearTimeout(searchDebounce);
        searchDebounce = setTimeout(() => {
            applyFilters();
        }, 400);
    });
});

// ============================================================================
// 4. TAB, BỘ LỌC & TẢI THÊM BÀI VIẾT
// ============================================================================
function switchTab(tabName) {
    if (currentTab === tabName) return;
    currentTab = tabName;

    document.querySelectorAll(".filter-tab").forEach(btn => {
        btn.className = "filter-tab px-3.5 py-1.5 rounded-xl text-xs font-bold transition flex items-center gap-1.5 whitespace-nowrap bg-slate-50 text-slate-600 hover:bg-slate-100 border border-slate-200/60";
    });

    const activeBtn = document.getElementById(`tab-${tabName}`);
    if (activeBtn) {
        activeBtn.className = "filter-tab active-tab px-3.5 py-1.5 rounded-xl text-xs font-bold transition flex items-center gap-1.5 whitespace-nowrap bg-teal-50 text-teal-700 border border-teal-200";
    }

    applyFilters();
}

function applyFilters() {
    currentPage = 1;
    hasMore = true;
    document.getElementById("noMorePosts")?.classList.add("hidden");

    const container = document.getElementById("communityPosts");
    if (container) container.innerHTML = "";

    loadMorePosts();
}

async function loadMorePosts() {
    if (isLoading || !hasMore) return;

    isLoading = true;
    const spinner = document.getElementById("loadingSpinner");
    if (spinner) spinner.classList.remove("hidden");

    const searchValue = document.getElementById("communitySearchInput")?.value.trim() || "";
    const filterStoryId = document.getElementById("filterStorySelect")?.value || "";

    try {
        const res = await fetch(`/Community/GetPosts?page=${currentPage}&pageSize=${pageSize}&filterTab=${currentTab}&search=${encodeURIComponent(searchValue)}&filterStoryId=${filterStoryId}`);
        if (!res.ok) throw new Error("Error loading posts");

        const posts = await res.json();

        if (Array.isArray(posts) && posts.length > 0) {
            renderPosts(posts);
            currentPage++;
            if (posts.length < pageSize) {
                hasMore = false;
                document.getElementById("noMorePosts")?.classList.remove("hidden");
            }
        } else {
            hasMore = false;
            document.getElementById("noMorePosts")?.classList.remove("hidden");
        }
    } catch (err) {
        console.error("Infinite scroll error:", err);
    } finally {
        isLoading = false;
        if (spinner) spinner.classList.add("hidden");
    }
}

// ============================================================================
// 5. RENDER DANH SÁCH BÀI VIẾT
// ============================================================================
function renderPosts(posts) {
    const container = document.getElementById("communityPosts");
    if (!container) return;

    const isEn = CURRENT_LANG === 'en';

    posts.forEach(post => {
        if (container.querySelector(`.post-card[data-post-id="${post.postId}"]`)) return;

        const avatarUrl = post.userImage ? (post.userImage.startsWith("/") ? post.userImage : "/" + post.userImage) : "/assets/image/placeholder.png";

        let storyBadge = post.storyTitle 
            ? `<a href="/Stories/Detail/${post.storyId}" target="_blank" class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-teal-50 border border-teal-200/60 text-teal-700 font-bold text-[11px] hover:bg-teal-100 transition shadow-2xs"><i class="fa-solid fa-book text-[10px] text-teal-500"></i><span class="truncate max-w-[160px] sm:max-w-[220px]">${post.storyTitle}</span></a>` 
            : "";
        let chapterBadge = post.chapterTitle 
            ? `<a href="/Stories/Detail/${post.storyId}" target="_blank" class="inline-flex items-center gap-1 px-2.5 py-1 rounded-full bg-sky-50 border border-sky-200/60 text-sky-700 font-bold text-[11px] hover:bg-sky-100 transition shadow-2xs"><i class="fa-solid fa-bookmark text-[10px] text-sky-500"></i><span>${post.chapterTitle}</span></a>` 
            : "";

        // Bài của mình: nút xóa. Bài người khác: nút báo cáo vi phạm (community-report.js)
        let deleteBtnHtml = post.userId === CURRENT_USER_ID 
            ? `<button type="button" onclick="deletePost(${post.postId})" class="text-slate-300 hover:text-rose-500 text-xs transition" title="${isEn ? "Delete post" : "Xóa bài viết"}"><i class="fa-regular fa-trash-can"></i></button>` 
            : `<button type="button" onclick="reportContent('post', ${post.postId})" class="btn-report text-slate-300 text-xs transition" title="${isEn ? "Report post" : "Báo cáo bài viết"}" aria-label="${isEn ? "Report post" : "Báo cáo bài viết"}"><i class="fa-regular fa-flag"></i></button>`;

        const postElement = document.createElement("article");
        postElement.className = "bg-white rounded-3xl p-5 border border-slate-100 shadow-sm space-y-3.5 post-card";
        postElement.setAttribute("data-post-id", post.postId);

        postElement.innerHTML = `
            <div class="flex items-start justify-between gap-3">
                <div class="flex items-center gap-3">
                    <img src="${avatarUrl}" class="w-10 h-10 rounded-2xl object-cover border border-teal-200 shrink-0 shadow-sm" onerror="this.src='/assets/image/placeholder.png';" />
                    <div>
                        <div class="flex items-center gap-2">
                            <h4 class="font-bold text-slate-800 text-xs sm:text-sm">${post.userName}</h4>
                            ${deleteBtnHtml}
                        </div>
                        <p class="text-[10px] text-slate-400 font-medium flex items-center gap-1 mt-0.5"><i class="fa-regular fa-clock"></i><span>${post.createdAt}</span></p>
                    </div>
                </div>
                <div class="flex flex-wrap items-center gap-1.5 justify-end">${storyBadge}${chapterBadge}</div>
            </div>

            ${renderSpoilerContent(post.content, post.isSpoiler, false)}

            <div class="flex items-center gap-4 pt-2.5 border-t border-slate-100 text-xs font-bold text-slate-500 select-none">
                <button type="button" onclick="toggleLikePost(${post.postId})" class="btn-like inline-flex items-center gap-1.5 hover:text-rose-500 transition active:scale-95 ${post.isLikedByMe ? "text-rose-500 font-extrabold" : ""}">
                    <i class="${post.isLikedByMe ? "fa-solid text-rose-500" : "fa-regular"} fa-heart text-sm like-icon"></i> <span>${isEn ? "Like" : "Thích"}</span> <span class="like-count text-slate-700">${post.totalLikes}</span>
                </button>
                <button type="button" onclick="document.getElementById('input-comment-${post.postId}').focus()" class="inline-flex items-center gap-1.5 hover:text-teal-600 transition active:scale-95">
                    <i class="fa-regular fa-comment text-sm"></i> <span>${isEn ? "Comment" : "Bình luận"}</span> <span class="comment-count text-slate-700">${post.totalComments}</span>
                </button>
            </div>

            <div class="comments-section pt-3 border-t border-slate-100 space-y-3">
                <div class="comments-list space-y-2.5">
                    <div class="no-comments text-center py-4 text-xs font-semibold text-slate-400 italic">${isEn ? "No comments yet. Be the first to start the discussion!" : "Chưa có bình luận nào. Hãy là người đầu tiên thảo luận!"}</div>
                </div>

                <div class="replying-to hidden bg-teal-50 border border-teal-100 text-teal-700 text-xs px-3 py-1.5 rounded-xl flex items-center justify-between" data-post-id="${post.postId}">
                    <span class="font-medium">${isEn ? "Replying to" : "Đang phản hồi"} <strong class="reply-user-name font-bold"></strong></span>
                    <button type="button" onclick="cancelReplyStatus(${post.postId})" class="text-rose-500 font-bold text-xs ml-2"><i class="fa-solid fa-xmark mr-1"></i> ${isEn ? "Cancel" : "Hủy"}</button>
                </div>

                <div id="comment-attach-box-${post.postId}" class="hidden p-3 bg-slate-50 border border-slate-200 rounded-2xl space-y-2.5">
                    <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
                        <select id="comment-story-${post.postId}" onchange="onStoryChange(this.value, 'comment-chapter-${post.postId}')"
                                class="p-2 bg-white border border-slate-200 rounded-xl text-xs font-semibold text-slate-700 outline-none focus:ring-2 focus:ring-teal-400 transition-all">
                            <option value="">-- ${isEn ? "Attach Story" : "Đính kèm truyện"} --</option>
                            ${STORIES_OPTIONS}
                        </select>
                        <select id="comment-chapter-${post.postId}" disabled
                                class="p-2 bg-white border border-slate-200 rounded-xl text-xs font-semibold text-slate-700 outline-none focus:ring-2 focus:ring-teal-400 transition-all disabled:opacity-50">
                            <option value="">-- ${isEn ? "Select chapter" : "Chọn chương"} --</option>
                        </select>
                    </div>
                    <label class="inline-flex items-center gap-2 cursor-pointer select-none pt-1">
                        <input type="checkbox" id="comment-spoiler-${post.postId}" class="w-3.5 h-3.5 rounded text-teal-500 focus:ring-teal-400 border-slate-300">
                        <span class="text-[11px] font-bold text-slate-600 flex items-center gap-1">
                            <i class="fa-solid fa-eye-slash text-slate-400"></i> ${isEn ? "Contains Spoiler" : "Chứa nội dung Spoiler"}
                        </span>
                    </label>
                </div>

                <div class="flex items-center gap-2 pt-1">
                    <button type="button" onclick="toggleCommentAttachment(${post.postId})" class="p-2.5 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-xl text-xs transition" title="${isEn ? "Attachment & Spoiler options" : "Tùy chọn đính kèm & Spoiler"}">
                        <i class="fa-solid fa-paperclip"></i>
                    </button>
                    <input type="text" id="input-comment-${post.postId}" data-post-id="${post.postId}" class="comment-input flex-1 px-3.5 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-800 outline-none focus:ring-2 focus:ring-teal-400 focus:bg-white transition" placeholder="${isEn ? "Write a comment..." : "Viết bình luận..."}" maxlength="2000" />
                    <button type="button" onclick="sendComment(${post.postId})" class="btn-grad p-2.5 text-white rounded-xl cursor-pointer hover:scale-105 active:scale-95 transition shadow-sm">
                        <i class="fa-solid fa-paper-plane text-xs"></i>
                    </button>
                </div>
            </div>
        `;

        container.appendChild(postElement);
    });
}

// ============================================================================
// 6. ĐĂNG BÀI (chọn truyện → chương, gửi bài)
// ============================================================================
async function onStoryChange(storyId, targetChapterSelectId = "chapterSelect") {
    const chapterSelect = document.getElementById(targetChapterSelectId);
    if (!chapterSelect) return;

    const defaultText = CURRENT_LANG === 'en' ? '-- Select chapter --' : '-- Chọn chương --';
    chapterSelect.innerHTML = `<option value="">${defaultText}</option>`;
    chapterSelect.disabled = true;

    if (!storyId || storyId === "0") return;

    try {
        const res = await fetch(`/Community/GetChaptersByStory?storyId=${storyId}`);
        if (!res.ok) throw new Error("Error loading chapters!");

        const chapters = await res.json();
        if (Array.isArray(chapters) && chapters.length > 0) {
            const chapterPrefix = CURRENT_LANG === 'en' ? 'Chapter' : 'Chương';
            chapters.forEach(c => {
                const opt = document.createElement("option");
                opt.value = c.chapterId;
                opt.textContent = c.displayTitle || `${chapterPrefix} ${c.chapterNumber}`;
                chapterSelect.appendChild(opt);
            });
            chapterSelect.disabled = false;
        }
    } catch (err) {
        console.warn("Error loading chapters:", err.message);
    }
}

function clearPostForm() {
    document.getElementById("postContent").value = "";
    document.getElementById("storySelect").value = "";
    const chapterSelect = document.getElementById("chapterSelect");
    chapterSelect.innerHTML = `<option value="">-- ${CURRENT_LANG === 'en' ? 'Select story first' : 'Chọn truyện trước'} --</option>`;
    chapterSelect.disabled = true;
    const chk = document.getElementById("postIsSpoiler");
    if (chk) chk.checked = false;
}

async function submitPost() {
    const btn = document.getElementById("btnCreatePost");
    const content = document.getElementById("postContent").value.trim();
    const storySelect = document.getElementById("storySelect");
    const chapterSelect = document.getElementById("chapterSelect");
    const isSpoiler = document.getElementById("postIsSpoiler")?.checked || false;

    const storyId = storySelect.value ? parseInt(storySelect.value) : null;
    const chapterId = chapterSelect.value ? parseInt(chapterSelect.value) : null;

    if (!content) {
        if (typeof showToast === "function") {
            showToast(CURRENT_LANG === 'en' ? "Please enter post content!" : "Vui lòng nhập nội dung bài viết!", 'error');
        }
        return;
    }

    btn.disabled = true;

    try {
        const res = await fetch("/Community/CreatePost", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ content, storyId, chapterId, isSpoiler })
        });

        const data = await res.json();
        if (data.requireLogin || !data.success) {
            if (typeof showToast === "function") {
                showToast(data.message || (CURRENT_LANG === 'en' ? "Please sign in to create a post!" : "Vui lòng đăng nhập để đăng bài!"), 'error');
            }
            return;
        }

        if (data.success) {
            clearPostForm();
        }
    } catch (err) {
        console.error(err);
    } finally {
        btn.disabled = false;
    }
}

// ============================================================================
// 7. BÌNH LUẬN & TRẢ LỜI
// ============================================================================
function showReplyStatus(postId, commentId, userName) {
    replyMap[postId] = { commentId, userName };
    const replyBox = document.querySelector(`.replying-to[data-post-id="${postId}"]`);
    if (replyBox) {
        replyBox.querySelector(".reply-user-name").textContent = "@" + userName;
        replyBox.classList.remove("hidden");
    }
    const input = document.getElementById(`input-comment-${postId}`);
    if (input) {
        const replyPrefix = CURRENT_LANG === 'en' ? 'Replying to @' : 'Phản hồi @';
        input.placeholder = replyPrefix + userName + '...';
        input.focus();
    }
    toggleCommentAttachment(postId);
}

function cancelReplyStatus(postId) {
    delete replyMap[postId];
    const replyBox = document.querySelector(`.replying-to[data-post-id="${postId}"]`);
    if (replyBox) replyBox.classList.add("hidden");
    const input = document.getElementById(`input-comment-${postId}`);
    if (input) input.placeholder = CURRENT_LANG === 'en' ? "Write a comment..." : "Viết bình luận...";
}

async function sendComment(postId) {
    const input = document.getElementById(`input-comment-${postId}`);
    const content = input ? input.value.trim() : "";
    if (!content) return;

    const activeReply = replyMap[postId] || null;
    const parentCommentId = activeReply ? activeReply.commentId : null;
    const storySelect = document.getElementById(`comment-story-${postId}`);
    const chapterSelect = document.getElementById(`comment-chapter-${postId}`);
    const isSpoiler = document.getElementById(`comment-spoiler-${postId}`)?.checked || false;

    const storyId = storySelect && storySelect.value ? parseInt(storySelect.value) : null;
    const chapterId = chapterSelect && chapterSelect.value ? parseInt(chapterSelect.value) : null;

    try {
        const res = await fetch("/Community/AddComment", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ postId: parseInt(postId), content, parentCommentId, storyId, chapterId, isSpoiler })
        });

        const data = await res.json();
        if (data.requireLogin || !data.success) {
            if (typeof showToast === "function") {
                showToast(data.message || (CURRENT_LANG === 'en' ? "Please sign in to comment!" : "Vui lòng đăng nhập để bình luận!"), 'error');
            }
            return;
        }

        if (data.success) {
            input.value = "";
            cancelReplyStatus(postId);
            if (storySelect) storySelect.value = "";
            if (chapterSelect) { chapterSelect.value = ""; chapterSelect.disabled = true; }
            const spoilerChk = document.getElementById(`comment-spoiler-${postId}`);
            if (spoilerChk) spoilerChk.checked = false;
            const attachBox = document.getElementById(`comment-attach-box-${postId}`);
            if (attachBox) attachBox.classList.add("hidden");
        }
    } catch (err) {
        console.error(err);
    }
}

// ============================================================================
// 8. XÓA BÀI / XÓA BÌNH LUẬN / THÍCH BÀI
// ============================================================================
async function deletePost(postId) {
    if (!confirm(CURRENT_LANG === 'en' ? "Are you sure you want to delete this post?" : "Bạn có chắc chắn muốn xóa bài viết này không?")) return;

    try {
        const res = await fetch(`/Community/DeletePost?postId=${postId}`, { method: "POST" });
        const data = await res.json();
        if (!data.success) {
            if (typeof showToast === "function") {
                showToast(data.message || (CURRENT_LANG === 'en' ? "Error deleting post!" : "Lỗi khi xóa bài viết!"), 'error');
            }
        }
    } catch (err) {
        console.error(err);
    }
}

async function deleteComment(commentId) {
    if (!confirm(CURRENT_LANG === 'en' ? "Are you sure you want to delete this comment?" : "Bạn có chắc muốn xóa bình luận này?")) return;

    try {
        const res = await fetch(`/Community/DeleteComment?commentId=${commentId}`, { method: "POST" });
        const data = await res.json();
        if (!data.success) {
            if (typeof showToast === "function") {
                showToast(data.message || (CURRENT_LANG === 'en' ? "Error deleting comment!" : "Lỗi khi xóa bình luận!"), 'error');
            }
        }
    } catch (err) {
        console.error(err);
    }
}

async function toggleLikePost(postId) {
    const postCard = document.querySelector(`.post-card[data-post-id="${postId}"]`);
    if (!postCard) return;

    const likeBtn = postCard.querySelector(".btn-like");
    const countSpan = postCard.querySelector(".like-count");
    const currentlyLiked = likeBtn.classList.contains("text-rose-500");
    let currentCount = parseInt(countSpan.textContent) || 0;

    if (currentlyLiked) {
        likeBtn.classList.remove("text-rose-500", "font-extrabold");
        countSpan.textContent = Math.max(0, currentCount - 1);
    } else {
        likeBtn.classList.add("text-rose-500", "font-extrabold");
        countSpan.textContent = currentCount + 1;
    }

    try {
        const res = await fetch("/Community/ToggleLike", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ postId: parseInt(postId) })
        });

        const data = await res.json();

        if (data.requireLogin || !data.success) {
            if (currentlyLiked) {
                likeBtn.classList.add("text-rose-500", "font-extrabold");
                countSpan.textContent = currentCount;
            } else {
                likeBtn.classList.remove("text-rose-500", "font-extrabold");
                countSpan.textContent = currentCount;
            }

            if (typeof showToast === "function") {
                showToast(data.message || (CURRENT_LANG === 'en' ? "Please sign in to like posts!" : "Vui lòng đăng nhập để thích bài viết!"), 'error');
            }
        }
    } catch (err) {
        console.error(err);
    }
}

// ============================================================================
// 9. REALTIME (SignalR) – cập nhật khi bài / bình luận bị xóa
// ============================================================================
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/communityHub")
    .withAutomaticReconnect()
    .build();

connection.on("PostDeleted", function (postId) {
    const postCard = document.querySelector(`.post-card[data-post-id="${postId}"]`);
    if (postCard) {
        postCard.style.transition = "all 0.3s ease";
        postCard.style.opacity = "0";
        postCard.style.transform = "scale(0.95)";
        setTimeout(() => postCard.remove(), 300);
    }
});

connection.on("CommentDeleted", function (payload) {
    const pId = payload.postId || payload.PostId;
    const deletedIds = payload.deletedCommentIds || payload.DeletedCommentIds || [];
    const totalComments = payload.totalComments || payload.TotalComments;

    deletedIds.forEach(cId => {
        const cItem = document.querySelector(`.comment-item[data-comment-id="${cId}"]`);
        if (cItem) cItem.remove();
    });

    const postCard = document.querySelector(`.post-card[data-post-id="${pId}"]`);
    if (postCard) {
        const commentCountSpan = postCard.querySelector(".comment-count");
        if (commentCountSpan) commentCountSpan.textContent = totalComments;
    }
});

connection.start().catch(err => console.error("SignalR Error: ", err));
