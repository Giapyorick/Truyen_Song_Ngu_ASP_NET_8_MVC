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
    appendMessage('ai', '...', loadingId);

    try {
        const response = await fetch(`/Ai/${endpoint}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ text: text }) 
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

        const data = await response.json();
        const resultText = data.translatedText || data.result || chatBox.dataset.noResponse || "No response from AI";
        updateMessage(loadingId, resultText);

    } catch (error) {
        console.error("Fetch Exception:", error);
        updateMessage(loadingId, ` ${chatBox.dataset.connectionError || 'Connection error'}: ${error.message}`);
    } finally {
        inputElement.disabled = false;
        inputElement.focus();
    }
}

function appendMessage(sender, text, id = null) {
    const history = document.getElementById('chat-history');
    const wrapper = document.createElement('div');
    wrapper.className = sender === 'user' ? "flex justify-end" : "flex justify-start";

    const div = document.createElement('div');
    div.id = id;
    div.className = sender === 'user'
        ? "btn-grad text-left text-white p-3 rounded-2xl rounded-tr-none max-w-[85%] text-sm shadow-md transition-all"
        : "btn-grad-gray text-left border border-gray-200 text-gray-800 p-3 rounded-2xl rounded-tl-none max-w-[85%] text-sm shadow-sm whitespace-pre-line line-height-relaxed";

    div.innerText = text;
    wrapper.appendChild(div);
    history.appendChild(wrapper);
    history.scrollTop = history.scrollHeight;
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

document.addEventListener('DOMContentLoaded', function () {
    const chatBtn = document.getElementById('chat-button');
    if (chatBtn) {
        chatBtn.removeEventListener('click', toggleChat);
        chatBtn.addEventListener('click', toggleChat);
    }
});