// --- BẮT VĂN BẢN KHI BÔI ĐEN VÀ HIỂN THỊ TOOLTIP NỔI ---
let selectedReadingText = "";

document.addEventListener('DOMContentLoaded', function () {
    const chatBtn = document.getElementById('chat-button');
    if (chatBtn) {
        chatBtn.removeEventListener('click', toggleChat);
        chatBtn.addEventListener('click', toggleChat);
    }

    const tooltip = document.getElementById('ai-selection-tooltip');
    if (!tooltip) return;

    // Lắng nghe thao tác bôi đen chuột
    document.addEventListener('mouseup', function (e) {
        // Nếu click trúng tooltip hoặc bên trong khung chat thì bỏ qua
        if (e.target.closest('#ai-selection-tooltip') || e.target.closest('#chat-box') || e.target.closest('#chat-button')) {
            return;
        }

        setTimeout(() => {
            const selection = window.getSelection();
            const text = selection.toString().trim();

            if (text.length > 1) { // Bôi đen từ 2 ký tự trở lên
                selectedReadingText = text;
                const range = selection.getRangeAt(0);
                const rect = range.getBoundingClientRect();

                // Đặt vị trí tooltip ngay phía trên vùng chọn văn bản
                tooltip.style.left = `${rect.left + rect.width / 2}px`;
                tooltip.style.top = `${rect.top - 8}px`; // Cách chữ 8px lên trên
                tooltip.classList.remove('hidden');
            } else {
                tooltip.classList.add('hidden');
                selectedReadingText = "";
            }
        }, 10);
    });

    // Ẩn tooltip khi người dùng click ra vùng khác hoặc cuộn trang
    document.addEventListener('mousedown', function (e) {
        if (!e.target.closest('#ai-selection-tooltip')) {
            tooltip.classList.add('hidden');
        }
    });

    window.addEventListener('scroll', function () {
        tooltip.classList.add('hidden');
    }, { passive: true });

    // Khi người dùng click vào nút "Hỏi AI" trên Tooltip
    tooltip.addEventListener('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
        tooltip.classList.add('hidden');

        if (!selectedReadingText) return;

        // 1. Mở Chatbox nếu đang đóng
        const chatBox = document.getElementById('chat-box');
        if (chatBox && (chatBox.classList.contains('hidden') || chatBox.classList.contains('opacity-0'))) {
            toggleChat();
        }

        // 2. Điền văn bản vào ô nhập
        const inputElement = document.getElementById('user-input');
        if (inputElement) {
            inputElement.value = selectedReadingText;

            // 3. Tự động kích hoạt giải thích ngữ cảnh
            handleAction('Explain');
        }
    });
});
function toggleChat(e) {
    // Chặn sự kiện nổi bọt lên các thẻ cha (tránh kích hoạt 2 lần)
    if (e) {
        e.preventDefault();
        e.stopPropagation();
    }

    const chatBox = document.getElementById('chat-box');
    if (!chatBox) return;

    const isHidden = chatBox.classList.contains('hidden') || chatBox.classList.contains('opacity-0');

    if (isHidden) {
        // Mở khung chat
        chatBox.classList.remove('hidden');
        // Delay 1 frame để CSS transition animation nhận diện
        requestAnimationFrame(() => {
            chatBox.classList.remove('opacity-0', 'translate-y-10', 'pointer-events-none');
        });
    } else {
        // Đóng khung chat
        chatBox.classList.add('opacity-0', 'translate-y-10', 'pointer-events-none');
        setTimeout(() => {
            chatBox.classList.add('hidden');
        }, 300);
    }
}

async function handleAction(endpoint) {
    const inputElement = document.getElementById('user-input');
    const chatBox = document.getElementById('chat-box');
    const text = inputElement.value.trim();
    if (!text) return;

    inputElement.value = '';
    inputElement.disabled = true;

    appendMessage('user', text);

    const loadingId = 'loading-' + Date.now();
    // Tạo bong bóng tin nhắn trống để hứng từng ký tự đổ về
    appendMessage('ai', '', loadingId);

    const currentCulture = (typeof getCurrentCulture === 'function')
        ? getCurrentCulture()
        : (document.cookie.includes('vi-VN') ? 'vi-VN' : 'en-US');

    const urlParams = new URLSearchParams(window.location.search);
    const currentChapterId = parseInt(urlParams.get('chapterId')) || window.CURRENT_CHAPTER_ID || 0;

    // Chuyển hướng: Nếu là 'Explain' thì gọi qua endpoint Stream, còn lại giữ nguyên
    const targetEndpoint = endpoint === 'Explain' ? 'StreamExplain' : endpoint;

    try {
        const response = await fetch(`/Ai/${targetEndpoint}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                text: text,
                chapterId: currentChapterId,
                culture: currentCulture,
                mode: currentAiMode 
            })
        });

        if (!response.ok) {
            let errorDetail = `${chatBox.dataset.httpError || 'Error'} (${response.status}: ${response.statusText})`;
            const rawText = await response.text();

            if (rawText) {
                try {
                    const errData = JSON.parse(rawText);
                    errorDetail = errData.message || errData.title || errData.result || rawText;
                } catch {
                    errorDetail = rawText.substring(0, 150);
                }
            }

            updateMessage(loadingId, ` Error [HTTP ${response.status}]: ${errorDetail}`);
            return;
        }

        // =========================================================================
        // VỊ TRÍ ĐOẠN if (targetEndpoint === 'StreamExplain') NẰM Ở ĐÂY:
        // =========================================================================
        if (targetEndpoint === 'StreamExplain') {
            const reader = response.body.getReader();
            const decoder = new TextDecoder("utf-8");
            let accumulatedMarkdown = "";
            let buffer = "";

            while (true) {
                const { value, done } = await reader.read();
                if (done) break;

                buffer += decoder.decode(value, { stream: true });
                const lines = buffer.split("\n\n");
                buffer = lines.pop(); // Giữ lại phần chưa hoàn tất

                for (const line of lines) {
                    const trimmedLine = line.trim();
                    if (!trimmedLine || !trimmedLine.startsWith("data:")) continue;

                    const rawData = trimmedLine.replace(/^data:\s*/, "");
                    if (rawData === '"[DONE]"' || rawData === '[DONE]') {
                        const aiBubble = document.getElementById(loadingId);
                        if (aiBubble) {
                            // Lưu kèm text gốc vào tham số thứ 4:
                            saveMessageToStorage('ai', accumulatedMarkdown, aiBubble.innerHTML, text);
                        }
                        attachAiMessageActions(loadingId, text);
                        break;
                    }

                    try {
                        // Giải mã chuỗi JSON để giữ chuẩn 100% space và dòng mới
                        const token = JSON.parse(rawData);
                        accumulatedMarkdown += token;
                        updateMessage(loadingId, accumulatedMarkdown);
                    } catch (e) {
                        // Dự phòng nếu không phải JSON
                        accumulatedMarkdown += rawData;
                        updateMessage(loadingId, accumulatedMarkdown);
                    }
                }
            }
        } else {
            // Trường hợp Translate thông thường
            const data = await response.json();
            const resultText = data.translatedText || data.result || chatBox.dataset.noResponse || "No response from AI";
            updateMessage(loadingId, resultText);
        }

    } catch (error) {
        console.error("Fetch Exception:", error);
        updateMessage(loadingId, ` ${chatBox.dataset.connectionError || 'Connection error'}: ${error.message}`);
    } finally {
        inputElement.disabled = false;
        inputElement.focus();
    }
}

function appendMessage(sender, text, id = null, shouldSave = true) {
    const history = document.getElementById('chat-history');
    if (!history) return;

    const wrapper = document.createElement('div');
    wrapper.className = sender === 'user' ? "flex justify-end animate-fade-in" : "flex justify-start animate-fade-in";

    const div = document.createElement('div');
    div.id = id;

    // TÁI SỬ DỤNG CHUẨN: btn-grad cho User và btn-grad-gray cho AI
    div.className = sender === 'user'
        ? "btn-grad text-left text-white px-4 py-2.5 rounded-2xl rounded-tr-none max-w-[85%] text-xs sm:text-[13px] font-medium shadow-md transition-all leading-relaxed break-words"
        : "btn-grad-gray text-left border border-gray-200 text-gray-800 p-3.5 rounded-2xl rounded-tl-none max-w-[90%] text-xs sm:text-[13px] shadow-sm leading-relaxed break-words";
    div.innerText = text;
    wrapper.appendChild(div);
    history.appendChild(wrapper);
    history.scrollTop = history.scrollHeight;

    if (shouldSave && sender === 'user') {
        saveMessageToStorage('user', text);
    }
}

function updateMessage(id, newText) {
    const el = document.getElementById(id);
    if (!el) return;

    if (typeof marked !== 'undefined' && marked.parse) {
        // Cấu hình marked nếu cần
        el.innerHTML = marked.parse(newText);
    } else {
        el.innerText = newText;
    }

    el.classList.add('ai-content');
    const history = document.getElementById('chat-history');
    if (history) {
        history.scrollTop = history.scrollHeight;
    }
}

document.addEventListener('DOMContentLoaded', function () {
    // Khôi phục dữ liệu chat cũ ngay khi mở trang
    restoreChatHistory();

    const chatBtn = document.getElementById('chat-button');
    if (chatBtn) {
        chatBtn.removeEventListener('click', toggleChat);
        chatBtn.addEventListener('click', toggleChat);
    }
});


function submitCurrentChat() {
    handleAction('StreamExplain');
}
// Đổi style khi bấm chọn Action Chip
let currentAiMode = 'Explain';

function setModeAndTrigger(mode) {
    currentAiMode = mode;

    const chips = document.querySelectorAll('.ai-action-chip');
    chips.forEach(chip => {
        chip.className = 'ai-action-chip'; // Quay về dạng xám thường
    });

    const activeBtn = event.currentTarget;
    if (activeBtn) {
        if (mode === 'Summary') {
            activeBtn.className = 'ai-action-chip active badge-pill bg-info';
        } else if (mode === 'Lore') {
            activeBtn.className = 'ai-action-chip active badge-pill bg-cus';
        } else if (mode === 'Smooth') {
            activeBtn.className = 'ai-action-chip active badge-pill bg-accepted';
        } else {
            activeBtn.className = 'ai-action-chip active badge-pill bg-main';
        }
    }

    const input = document.getElementById('user-input');
    if (input && input.value.trim().length > 0) {
        handleAction('StreamExplain');
    }
}
// Phân tích toàn chương khi bấm nút (không cần người dùng bôi đen văn bản)
function triggerChapterAnalysis(mode, btnElem) {
    currentAiMode = mode;

    // Đổi style active cho các nút chip
    document.querySelectorAll('.ai-action-chip').forEach(chip => {
        chip.className = 'ai-action-chip';
    });

    if (btnElem) {
        if (mode === 'Summary') {
            btnElem.className = 'ai-action-chip active badge-pill bg-info';
        } else if (mode === 'Lore') {
            btnElem.className = 'ai-action-chip active badge-pill bg-wait';
        } else {
            btnElem.className = 'ai-action-chip active badge-pill bg-main';
        }
    }

    // Hiển thị thông báo vào khung chat cho độc giả biết đang phân tích cả chương
    const isVi = document.cookie.includes('vi-VN') || (!document.cookie.includes('en-US'));
    let actionDesc = "";
    if (mode === 'Summary') actionDesc = isVi ? "Yêu cầu tóm tắt diễn biến chương này..." : "Summarizing current chapter...";
    else if (mode === 'Lore') actionDesc = isVi ? "Yêu cầu giải mã cốt truyện & lore của chương..." : "Decoding chapter lore & subtext...";
    else actionDesc = isVi ? "Đang cảm thụ & phân tích diễn biến chương này..." : "Analyzing chapter impression...";

    // Gửi yêu cầu với text rỗng -> Backend sẽ tự động lấy Chapter Content từ DB
    const inputElement = document.getElementById('user-input');
    if (inputElement) {
        inputElement.value = actionDesc;
    }

    handleAction('StreamExplain');
}
// Cập nhật 2 nút Lưu từ & Sao chép dưới tin nhắn AI thành kiểu Badge Pill
function attachAiMessageActions(messageId, originalText) {
    const el = document.getElementById(messageId);
    if (!el || el.querySelector('.ai-msg-actions')) return;

    const isVi = document.cookie.includes('vi-VN') || (!document.cookie.includes('en-US'));
    const saveLabel = isVi ? "Lưu trích dẫn" : "Save Quote";
    const copyLabel = isVi ? "Sao chép" : "Copy";

    const actionsDiv = document.createElement('div');
    actionsDiv.className = 'ai-msg-actions flex items-center gap-2 mt-3 pt-2.5 border-t border-slate-100 select-none';

    actionsDiv.innerHTML = `
        <button type="button" 
                onclick="saveVocabFromMessage('${messageId}', decodeURIComponent('${encodeURIComponent(originalText)}'))" 
                class="badge-pill bg-wait cursor-pointer hover:scale-105 active:scale-95">
            <i class="fa-solid fa-bookmark"></i>
            <span>${saveLabel}</span>
        </button>
        <button type="button" 
                onclick="copyAiMessage('${messageId}')" 
                class="badge-pill bg-main cursor-pointer hover:scale-105 active:scale-95">
            <i class="fa-solid fa-copy"></i>
            <span>${copyLabel}</span>
        </button>
    `;
    el.appendChild(actionsDiv);
}
// Xử lý gửi API lưu từ
async function saveVocabFromMessage(messageId, originalText) {
    const el = document.getElementById(messageId);
    if (!el) return;

    // Nhân bản thẻ tin nhắn và loại bỏ 2 nút Lưu/Sao chép
    const clone = el.cloneNode(true);
    const actions = clone.querySelector('.ai-msg-actions');
    if (actions) actions.remove();

    // SỬA TẠI ĐÂY: Dùng innerHTML để giữ nguyên thẻ badge, icon, class màu sắc
    const explanationContent = clone.innerHTML.trim();

    const urlParams = new URLSearchParams(window.location.search);
    const currentChapterId = parseInt(urlParams.get('chapterId')) || window.CURRENT_CHAPTER_ID || 0;

    try {
        const response = await fetch('/Ai/SaveVocabulary', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                word: originalText.substring(0, 100),
                explanation: explanationContent, // Lưu đầy đủ HTML và badge
                contextSentence: originalText,
                chapterId: currentChapterId
            })
        });

        const res = await response.json();
        if (res.success) {
            if (typeof showToast === 'function') {
                showToast(res.message, 'success');
            } else {
                alert(res.message);
            }
        } else {
            if (typeof showToast === 'function') {
                showToast(res.message, 'error');
            } else {
                alert(res.message);
            }
        }
    } catch (err) {
        console.error("Lỗi lưu từ:", err);
        if (typeof showToast === 'function') {
            showToast('Lỗi kết nối khi lưu từ vựng!', 'error');
        } else {
            alert('Lỗi kết nối khi lưu từ vựng!');
        }
    }
}

// Xử lý copy text
function copyAiMessage(messageId) {
    const el = document.getElementById(messageId);
    if (!el) return;
    const textToCopy = el.innerText.replace(/Lưu từ|Sao chép/g, '').trim();
    navigator.clipboard.writeText(textToCopy).then(() => {
        showToast('Đã sao chép vào bộ nhớ tạm!', 'success');
    });
}
const CHAT_STORAGE_KEY = 'user_ai_chat_history';

// 1. Hàm lưu tin nhắn vào localStorage
// Lưu tin nhắn kèm câu gốc bôi đen để phục hồi nút Lưu từ
function saveMessageToStorage(sender, text, htmlContent = null, originalText = null) {
    let history = [];
    try {
        history = JSON.parse(localStorage.getItem(CHAT_STORAGE_KEY)) || [];
    } catch {
        history = [];
    }

    history.push({
        sender: sender,
        text: text,
        htmlContent: htmlContent,
        originalText: originalText, // Lưu lại câu gốc để phục hồi nút bấm
        time: new Date().toISOString()
    });

    if (history.length > 50) {
        history = history.slice(history.length - 50);
    }

    localStorage.setItem(CHAT_STORAGE_KEY, JSON.stringify(history));
}

// 2. Khôi phục toàn bộ tin nhắn khi tải trang
function restoreChatHistory() {
    const historyContainer = document.getElementById('chat-history');
    if (!historyContainer) return;

    let savedMessages = [];
    try {
        savedMessages = JSON.parse(localStorage.getItem(CHAT_STORAGE_KEY)) || [];
    } catch {
        savedMessages = [];
    }

    if (savedMessages.length === 0) return;

    savedMessages.forEach((msg, idx) => {
        const msgId = `restored-msg-${idx}`;
        appendMessage(msg.sender, msg.text, msgId, false);

        if (msg.sender === 'ai') {
            const el = document.getElementById(msgId);
            if (el) {
                // Nếu có htmlContent thì nạp lại Markdown HTML
                if (msg.htmlContent) {
                    el.innerHTML = msg.htmlContent;
                    // Loại bỏ thanh actions cũ nếu bị lưu dính vào htmlContent để tránh trùng lặp
                    const oldActions = el.querySelector('.ai-msg-actions');
                    if (oldActions) oldActions.remove();
                }

                // GẮN LẠI NÚT "LƯU TỪ" VÀ "SAO CHÉP"
                const originalText = msg.originalText || msg.text || "";
                attachAiMessageActions(msgId, originalText);
            }
        }
    });

    historyContainer.scrollTop = historyContainer.scrollHeight;
}

// 3. Nút xóa lịch sử nếu người dùng muốn làm sạch đoạn chat (Tùy chọn)
function clearChatHistory() {
    localStorage.removeItem(CHAT_STORAGE_KEY);
    const historyContainer = document.getElementById('chat-history');
    if (historyContainer) {
        historyContainer.innerHTML = '';
    }
}