/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/reading/ai-chatbox.js
 *  CHỨC NĂNG : Khung chat AI khi đọc truyện: giải thích / dịch đoạn bôi đen, phân tích chương, lưu từ vựng
 *  DÙNG TẠI  : Views/Paragraphs/Content.cshtml (HTML ở Views/Shared/_AiChatBoxPartial.cshtml)
 *  PHỤ THUỘC : marked (render Markdown), showToast(), window.AI_LANG, currentChapterId, localStorage (lịch sử chat)
 *  THỨ TỰ    : Nạp SAU reading-viewer.js
 * ----------------------------------------------------------------------------
 *  MỤC LỤC
 *    1. BIẾN TOÀN CỤC & KHỞI TẠO (nút chat, lấy đoạn văn bôi đen)
 *    2. ĐÓNG / MỞ KHUNG CHAT
 *    3. GỬI YÊU CẦU TỚI AI & HIỂN THỊ / CẬP NHẬT TIN NHẮN
 *    4. Ô NHẬP CHAT & CHẾ ĐỘ AI (giải thích, dịch, phân tích chương)
 *    5. HÀNH ĐỘNG TRÊN TIN NHẮN AI (lưu từ vựng, sao chép)
 *    6. LƯU / KHÔI PHỤC / XÓA LỊCH SỬ CHAT (localStorage)
 * ============================================================================
 */

// ============================================================================
// 1. BIẾN TOÀN CỤC & KHỞI TẠO (nút chat, lấy đoạn văn bôi đen)
// ============================================================================
let selectedReadingText = "";

document.addEventListener('DOMContentLoaded', function () {
    const chatBtn = document.getElementById('chat-button');
    if (chatBtn) {
        chatBtn.removeEventListener('click', toggleChat);
        chatBtn.addEventListener('click', toggleChat);
    }

    const tooltip = document.getElementById('ai-selection-tooltip');
    if (!tooltip) return;

    document.addEventListener('mouseup', function (e) {
        if (e.target.closest('#ai-selection-tooltip') || e.target.closest('#chat-box') || e.target.closest('#chat-button')) {
            return;
        }

        setTimeout(() => {
            const selection = window.getSelection();
            const text = selection.toString().trim();

            if (text.length > 1) {
                selectedReadingText = text;
                const range = selection.getRangeAt(0);
                const rect = range.getBoundingClientRect();

                tooltip.style.left = `${rect.left + rect.width / 2}px`;
                tooltip.style.top = `${rect.top - 8}px`;
                tooltip.classList.remove('hidden');
            } else {
                tooltip.classList.add('hidden');
                selectedReadingText = "";
            }
        }, 10);
    });

    document.addEventListener('mousedown', function (e) {
        if (!e.target.closest('#ai-selection-tooltip')) {
            tooltip.classList.add('hidden');
        }
    });

    window.addEventListener('scroll', function () {
        tooltip.classList.add('hidden');
    }, { passive: true });

    tooltip.addEventListener('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
        tooltip.classList.add('hidden');

        if (!selectedReadingText) return;

        const chatBox = document.getElementById('chat-box');
        if (chatBox && (chatBox.classList.contains('hidden') || chatBox.classList.contains('opacity-0'))) {
            toggleChat();
        }

        const inputElement = document.getElementById('user-input');
        if (inputElement) {
            inputElement.value = selectedReadingText;
            handleAction('Explain');
        }
    });
});

// ============================================================================
// 2. ĐÓNG / MỞ KHUNG CHAT
// ============================================================================
function toggleChat(e) {
    if (e) {
        e.preventDefault();
        e.stopPropagation();
    }

    const chatBox = document.getElementById('chat-box');
    if (!chatBox) return;

    const isHidden = chatBox.classList.contains('hidden') || chatBox.classList.contains('opacity-0');

    if (isHidden) {
        chatBox.classList.remove('hidden');
        requestAnimationFrame(() => {
            chatBox.classList.remove('opacity-0', 'translate-y-10', 'pointer-events-none');
        });
    } else {
        chatBox.classList.add('opacity-0', 'translate-y-10', 'pointer-events-none');
        setTimeout(() => {
            chatBox.classList.add('hidden');
        }, 300);
    }
}

// ============================================================================
// 3. GỬI YÊU CẦU TỚI AI & HIỂN THỊ / CẬP NHẬT TIN NHẮN
// ============================================================================
async function handleAction(endpoint) {
    const inputElement = document.getElementById('user-input');
    const chatBox = document.getElementById('chat-box');
    const text = inputElement.value.trim();
    if (!text) return;

    inputElement.value = '';
    inputElement.disabled = true;

    appendMessage('user', text);

    const loadingId = 'loading-' + Date.now();
    appendMessage('ai', '', loadingId);

    const currentCulture = (typeof getCurrentCulture === 'function')
        ? getCurrentCulture()
        : (document.cookie.includes('vi-VN') ? 'vi-VN' : 'en-US');

    const urlParams = new URLSearchParams(window.location.search);
    const currentChapterId = parseInt(urlParams.get('chapterId')) || window.CURRENT_CHAPTER_ID || 0;

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
                buffer = lines.pop();

                for (const line of lines) {
                    const trimmedLine = line.trim();
                    if (!trimmedLine || !trimmedLine.startsWith("data:")) continue;

                    const rawData = trimmedLine.replace(/^data:\s*/, "");
                    if (rawData === '"[DONE]"' || rawData === '[DONE]') {
                        const aiBubble = document.getElementById(loadingId);
                        if (aiBubble) {
                            saveMessageToStorage('ai', accumulatedMarkdown, aiBubble.innerHTML, text);
                        }
                        attachAiMessageActions(loadingId, text);
                        break;
                    }

                    try {
                        const token = JSON.parse(rawData);
                        accumulatedMarkdown += token;
                        updateMessage(loadingId, accumulatedMarkdown);
                    } catch (e) {
                        accumulatedMarkdown += rawData;
                        updateMessage(loadingId, accumulatedMarkdown);
                    }
                }
            }
        } else {
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

// ============================================================================
// 4. Ô NHẬP CHAT & CHẾ ĐỘ AI (giải thích, dịch, phân tích chương)
// ============================================================================
document.addEventListener('DOMContentLoaded', function () {
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

let currentAiMode = 'Explain';

function setModeAndTrigger(mode) {
    currentAiMode = mode;

    const chips = document.querySelectorAll('.ai-action-chip');
    chips.forEach(chip => {
        chip.className = 'ai-action-chip';
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

function triggerChapterAnalysis(mode, btnElem) {
    currentAiMode = mode;

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

    let actionDesc = "";
    if (mode === 'Summary') actionDesc = window.AI_LANG.ReqSummary;
    else if (mode === 'Lore') actionDesc = window.AI_LANG.ReqLore;
    else actionDesc = window.AI_LANG.ReqImpression;

    const inputElement = document.getElementById('user-input');
    if (inputElement) {
        inputElement.value = actionDesc;
    }

    handleAction('StreamExplain');
}

// ============================================================================
// 5. HÀNH ĐỘNG TRÊN TIN NHẮN AI (lưu từ vựng, sao chép)
// ============================================================================
function attachAiMessageActions(messageId, originalText) {
    const el = document.getElementById(messageId);
    if (!el || el.querySelector('.ai-msg-actions')) return;

    const saveLabel = window.AI_LANG.SaveQuote;
    const copyLabel = window.AI_LANG.Copy;

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

async function saveVocabFromMessage(messageId, originalText) {
    const el = document.getElementById(messageId);
    if (!el) return;

    const clone = el.cloneNode(true);
    const actions = clone.querySelector('.ai-msg-actions');
    if (actions) actions.remove();

    const explanationContent = clone.innerHTML.trim();

    const urlParams = new URLSearchParams(window.location.search);
    const currentChapterId = parseInt(urlParams.get('chapterId')) || window.CURRENT_CHAPTER_ID || 0;

    try {
        const response = await fetch('/Ai/SaveVocabulary', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                word: originalText.substring(0, 100),
                explanation: explanationContent,
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
        console.error("Save error:", err);
        if (typeof showToast === 'function') {
            showToast(window.AI_LANG.NetworkErrorSave, 'error');
        } else {
            alert(window.AI_LANG.NetworkErrorSave);
        }
    }
}

function copyAiMessage(messageId) {
    const el = document.getElementById(messageId);
    if (!el) return;
    const textToCopy = el.innerText.replace(/Lưu trích dẫn|Save Quote|Sao chép|Copy/g, '').trim();
    navigator.clipboard.writeText(textToCopy).then(() => {
        if (typeof showToast === 'function') {
            showToast(window.AI_LANG.CopiedToClipboard, 'success');
        } else {
            alert(window.AI_LANG.CopiedToClipboard);
        }
    });
}

// ============================================================================
// 6. LƯU / KHÔI PHỤC / XÓA LỊCH SỬ CHAT (localStorage)
// ============================================================================
const CHAT_STORAGE_KEY = 'user_ai_chat_history';

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
        originalText: originalText,
        time: new Date().toISOString()
    });

    if (history.length > 50) {
        history = history.slice(history.length - 50);
    }

    localStorage.setItem(CHAT_STORAGE_KEY, JSON.stringify(history));
}

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
                if (msg.htmlContent) {
                    el.innerHTML = msg.htmlContent;
                    const oldActions = el.querySelector('.ai-msg-actions');
                    if (oldActions) oldActions.remove();
                }

                const originalText = msg.originalText || msg.text || "";
                attachAiMessageActions(msgId, originalText);
            }
        }
    });

    historyContainer.scrollTop = historyContainer.scrollHeight;
}

function clearChatHistory() {
    localStorage.removeItem(CHAT_STORAGE_KEY);
    const historyContainer = document.getElementById('chat-history');
    if (historyContainer) {
        historyContainer.innerHTML = '';
    }
}
