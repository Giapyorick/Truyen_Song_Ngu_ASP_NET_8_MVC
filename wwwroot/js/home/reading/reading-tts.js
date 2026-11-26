/**
 * ============================================================================
 *  FILE      : wwwroot/js/home/reading/reading-tts.js
 *  CHỨC NĂNG : Đọc to truyện (Text-to-Speech).
 *              - Câu nào admin đã tạo audio ElevenLabs (Admin > Audio truyện) thì phát file mp3
 *                (/Paragraphs/GetAudioMap), câu chưa có audio đọc bằng Web Speech API của
 *                trình duyệt – MIỄN PHÍ, không tốn token.
 *              - Đọc lần lượt từng câu/đoạn (.text-segment), câu đang đọc được tô sáng
 *              - Bấm vào một câu (khi trình phát đang mở) để đọc từ câu đó
 *              - Chế độ: đọc ngôn ngữ đang hiển thị, hoặc song ngữ (câu gốc rồi câu dịch)
 *              - Tốc độ 0.75x – 1.5x, tự đọc chương tiếp theo
 *              - Nhớ lựa chọn (localStorage)
 *  DÙNG TẠI  : Views/Paragraphs/Content.cshtml
 *              (nút #btnTtsToggle ở Views/Shared/_ReadingViewPartial.cshtml)
 *  PHỤ THUỘC : reading-viewer.js (window.readingViewer / window.readerModule),
 *              window.TTS_LANG (nhãn giao diện), showToast() nếu có
 * ============================================================================
 */
(function () {
    'use strict';

    const synth = window.speechSynthesis;
    const L = Object.assign({
        listen: 'Nghe', title: 'Nghe truyện',
        play: 'Phát', pause: 'Tạm dừng', stop: 'Dừng', prev: 'Câu trước', next: 'Câu sau', close: 'Đóng',
        speed: 'Tốc độ', mode: 'Chế độ', modeShown: 'Ngôn ngữ đang hiện', modeBoth: 'Song ngữ',
        autoNext: 'Tự đọc chương sau',
        hint: 'Bấm vào câu bất kỳ để đọc từ câu đó',
        noSupport: 'Trình duyệt này không hỗ trợ đọc to.',
        noVoice: 'Thiết bị chưa có giọng đọc cho ngôn ngữ: ',
        ended: 'Đã đọc hết chương.',
        ready: 'Sẵn sàng', reading: 'Đang đọc',
        aiVoice: 'Giọng AI', aiVoiceTitle: 'Chương này có audio giọng AI (ElevenLabs)'
    }, window.TTS_LANG || {});

    const LANG_BCP47 = { en: 'en-US', vi: 'vi-VN', zh: 'zh-CN', ja: 'ja-JP', fr: 'fr-FR' };
    const LANG_LABEL = { en: 'English', vi: 'Tiếng Việt', zh: '中文', ja: '日本語', fr: 'Français' };
    const STORE_KEY = 'reader.tts';

    // Nút "Nghe" nằm trên thanh tiêu đề chương; Focus mode sao chép thanh này nên có thể có 2 nút
    const toggles = () => document.querySelectorAll('.tts-toggle');
    if (!toggles().length) return;

    // Không có Web Speech API vẫn phát được câu có audio ElevenLabs
    const hasSynth = !!synth && typeof SpeechSynthesisUtterance !== 'undefined';
    const cancelSynth = () => { if (hasSynth) synth.cancel(); };

    // ---------- Audio ElevenLabs (file mp3 admin đã tạo) ----------
    const player = new Audio();
    player.preload = 'auto';
    const audioMaps = {};      // chapterId -> { lang: { order: url } }
    let audioMapLoading = {};  // chapterId -> Promise

    function loadAudioMap(chapterId) {
        if (!chapterId) return Promise.resolve({});
        if (audioMaps[chapterId]) return Promise.resolve(audioMaps[chapterId]);
        if (audioMapLoading[chapterId]) return audioMapLoading[chapterId];

        audioMapLoading[chapterId] = fetch('/Paragraphs/GetAudioMap?chapterId=' + encodeURIComponent(chapterId), { credentials: 'same-origin' })
            .then(r => (r.ok ? r.json() : {}))
            .catch(() => ({}))
            .then(map => {
                audioMaps[chapterId] = map || {};
                delete audioMapLoading[chapterId];
                updatePanel();
                return audioMaps[chapterId];
            });
        return audioMapLoading[chapterId];
    }

    const currentChapterId = () => viewer()?.chapterId || null;
    const audioUrl = (lang, order) => {
        const map = audioMaps[currentChapterId()];
        return (map && map[lang] && order != null) ? map[lang][String(order)] || null : null;
    };
    const chapterHasAnyAudio = () => {
        const map = audioMaps[currentChapterId()];
        return !!map && Object.values(map).some(m => m && Object.keys(m).length);
    };
    const chapterHasAudio = (lang) => {
        const map = audioMaps[currentChapterId()];
        return !!(map && map[lang] && Object.keys(map[lang]).length);
    };

    // ---------- Trạng thái ----------
    const state = {
        open: false,
        playing: false,
        index: 0,          // vị trí câu đang đọc trong danh sách .text-segment
        token: 0,          // tăng mỗi lần dừng/đổi câu để bỏ qua sự kiện onend cũ
        pendingAutoPlay: false,
        warnedLangs: {},
        settings: { rate: 1, mode: 'shown', autoNext: true }
    };

    try {
        const saved = JSON.parse(localStorage.getItem(STORE_KEY) || 'null');
        if (saved) Object.assign(state.settings, saved);
    } catch (e) { /* localStorage có thể bị chặn */ }

    const saveSettings = () => {
        try { localStorage.setItem(STORE_KEY, JSON.stringify(state.settings)); } catch (e) { }
    };

    const toast = (msg, type) => {
        if (typeof window.showToast === 'function') window.showToast(msg, type || 'info');
        else console.info('[TTS]', msg);
    };

    // ---------- Giọng đọc ----------
    let voices = [];
    const loadVoices = () => { voices = hasSynth ? (synth.getVoices() || []) : []; };
    loadVoices();
    if (hasSynth && 'onvoiceschanged' in synth) synth.addEventListener('voiceschanged', loadVoices);

    function pickVoice(code) {
        const bcp = (LANG_BCP47[code] || code).toLowerCase();
        const prefix = bcp.split('-')[0];
        const cands = voices.filter(v => (v.lang || '').toLowerCase().replace('_', '-').startsWith(prefix));
        if (!cands.length) return null;
        // Ưu tiên giọng chất lượng cao (Google / Natural / Online), rồi tới đúng vùng
        const score = v => {
            const n = (v.name || '').toLowerCase();
            let s = 0;
            if ((v.lang || '').toLowerCase().replace('_', '-') === bcp) s += 4;
            if (/natural|online|neural/.test(n)) s += 3;
            if (/google/.test(n)) s += 2;
            if (v.localService === false) s += 1;
            return s;
        };
        return cands.sort((a, b) => score(b) - score(a))[0];
    }

    // ---------- Truy cập nội dung ----------
    const viewer = () => window.readingViewer || window.readerModule || null;
    const focusOn = () => {
        const ov = document.getElementById('focusModeOverlay');
        return !!ov && !ov.classList.contains('hidden');
    };
    const segsIn = (root) => root ? Array.from(root.querySelectorAll('.text-segment')) : [];
    const mainSegs = () => segsIn(document.getElementById('paragraphList'));
    const focusSegs = () => segsIn(document.getElementById('focusContentContainer'));
    // Câu người dùng đang nhìn thấy (bản trong Focus nếu đang bật Focus)
    const visibleSeg = (i) => (focusOn() ? focusSegs()[i] : null) || mainSegs()[i] || null;

    const htmlToText = (html) => {
        if (!html) return '';
        const d = document.createElement('div');
        d.innerHTML = html;
        return (d.textContent || '').replace(/\s+/g, ' ').trim();
    };

    // Danh sách các phần cần đọc cho câu thứ i: [{ text, lang }]
    function partsFor(i) {
        const el = visibleSeg(i);
        if (!el) return [];
        const shownLang = el.getAttribute('data-lang') || viewer()?.currentSrcLang || 'en';
        const shownText = (el.textContent || '').replace(/\s+/g, ' ').trim();
        const order = el.getAttribute('data-order');
        const parts = [];
        if (shownText) parts.push({ text: shownText, lang: shownLang, audio: audioUrl(shownLang, order) });

        if (state.settings.mode === 'both') {
            const v = viewer();
            const src = v?.currentSrcLang, tgt = v?.currentTargetLang;
            if (src && tgt) {
                const otherLang = shownLang === src ? tgt : src;
                const otherText = htmlToText(el.getAttribute(shownLang === src ? 'data-target' : 'data-src'));
                if (otherText && otherText !== shownText) parts.push({ text: otherText, lang: otherLang, audio: audioUrl(otherLang, order) });
            }
        }
        return parts;
    }

    // ---------- Tô sáng & cuộn ----------
    function clearHighlight() {
        document.querySelectorAll('.text-segment.tts-active').forEach(el => el.classList.remove('tts-active'));
    }

    function highlight(i) {
        clearHighlight();
        [mainSegs()[i], focusSegs()[i]].forEach(el => el && el.classList.add('tts-active'));
        const el = visibleSeg(i);
        if (!el) return;

        // Chỉ cuộn khi câu nằm ngoài vùng nhìn thấy hoặc bị trình phát che (tránh giật màn hình)
        const r = el.getBoundingClientRect();
        const vh = window.innerHeight || document.documentElement.clientHeight;
        const panelTop = state.open && !panel.hidden ? panel.getBoundingClientRect().top : vh - 120;
        const limit = Math.min(vh - 120, panelTop - 16);
        if (r.top < 90 || r.bottom > limit) {
            // Đưa câu lên khoảng 1/3 màn hình để không nằm dưới trình phát
            const scroller = focusOn() ? document.querySelector('#focusModeOverlay > div') : null;
            const offset = r.top - Math.max(90, (limit - r.height) / 3);
            if (scroller) scroller.scrollBy({ top: offset, behavior: 'smooth' });
            else window.scrollBy({ top: offset, behavior: 'smooth' });
        }
    }

    // ---------- Phát ----------
    function speakIndex(i) {
        const total = mainSegs().length;
        if (i >= total) return finishChapter();

        state.index = i;
        const myToken = ++state.token;
        const parts = partsFor(i);
        highlight(i);
        updatePanel();

        if (!parts.length) return speakIndex(i + 1);

        let p = 0;
        const speakPart = () => {
            if (myToken !== state.token || !state.playing) return;
            if (p >= parts.length) return speakIndex(i + 1);

            const part = parts[p++];

            // Có audio ElevenLabs -> phát mp3; lỗi tải file thì đọc bằng giọng trình duyệt
            if (part.audio) {
                player.onended = () => { if (myToken === state.token) speakPart(); };
                player.onerror = () => {
                    if (myToken !== state.token) return;
                    console.warn('[TTS] audio error:', part.audio);
                    part.audio = null;
                    p--;
                    speakPart();
                };
                player.src = part.audio;
                player.playbackRate = state.settings.rate;
                const pr = player.play();
                if (pr && pr.catch) pr.catch(err => {
                    if (myToken !== state.token || err?.name === 'AbortError') return;
                    console.warn('[TTS] play() bị chặn:', err);
                    pause();
                });
                return;
            }

            if (!hasSynth) return speakPart();   // trình duyệt không đọc được và câu chưa có audio
            const voice = pickVoice(part.lang);
            if (!voice && voices.length) {
                // Không có giọng cho ngôn ngữ này -> báo một lần rồi bỏ qua phần đó
                if (!state.warnedLangs[part.lang]) {
                    state.warnedLangs[part.lang] = true;
                    toast(L.noVoice + (LANG_LABEL[part.lang] || part.lang), 'warning');
                }
                return speakPart();
            }

            const u = new SpeechSynthesisUtterance(part.text);
            u.lang = voice ? voice.lang : (LANG_BCP47[part.lang] || part.lang);
            if (voice) u.voice = voice;
            u.rate = state.settings.rate;
            u.pitch = 1;
            u.onend = () => { if (myToken === state.token) speakPart(); };
            u.onerror = (e) => {
                if (myToken !== state.token) return;
                if (e.error === 'interrupted' || e.error === 'canceled') return;
                console.warn('[TTS] error:', e.error);
                speakPart();
            };
            synth.speak(u);
        };

        player.pause();
        cancelSynth();
        // Chrome cần một nhịp ngắn sau cancel() mới nhận câu mới ổn định
        setTimeout(speakPart, 60);
    }

    function play(from) {
        if (!mainSegs().length) return;
        state.playing = true;
        const idx = typeof from === 'number' ? from : state.index;
        // Chờ tải danh sách audio của chương (nếu chưa có) rồi mới đọc
        const chap = currentChapterId();
        const startAt = () => {
            // Trình duyệt không đọc được và chương không có audio ElevenLabs -> báo, không chạy
            if (!hasSynth && !chapterHasAnyAudio()) {
                state.playing = false;
                toast(L.noSupport, 'warning');
                updatePanel();
                return;
            }
            speakIndex(idx);
        };
        if (chap && !audioMaps[chap]) {
            const myToken = ++state.token;
            loadAudioMap(chap).then(() => { if (state.playing && myToken === state.token) startAt(); });
            updatePanel();
            return;
        }
        startAt();
    }

    function pause() {
        state.playing = false;
        state.token++;
        player.pause();
        cancelSynth();
        updatePanel();
    }

    function stop() {
        pause();
        state.index = 0;
        clearHighlight();
        updatePanel();
    }

    function step(delta) {
        const total = mainSegs().length;
        if (!total) return;
        const target = Math.min(Math.max(state.index + delta, 0), total - 1);
        if (state.playing) speakIndex(target);
        else { state.index = target; highlight(target); updatePanel(); }
    }

    function finishChapter() {
        state.playing = false;
        state.token++;
        clearHighlight();
        state.index = 0;
        updatePanel();

        const next = document.getElementById('btnChapterNext');
        const v = viewer();
        const list = v?.chapterList || [];
        const idx = list.findIndex(c => c.chapterId == v?.chapterId);
        const hasNext = idx >= 0 && idx < list.length - 1;

        if (state.settings.autoNext && hasNext && next) {
            state.pendingAutoPlay = true;
            next.click();
        } else {
            toast(L.ended, 'success');
        }
    }

    // ---------- Giao diện trình phát ----------
    const panel = document.createElement('div');
    panel.id = 'ttsPanel';
    panel.className = 'tts-panel';
    panel.hidden = true;
    panel.setAttribute('role', 'region');
    panel.setAttribute('aria-label', L.title);
    panel.innerHTML = `
        <div class="tts-top">
            <span class="tts-title"><i class="fa-solid fa-headphones"></i> ${L.title}</span>
            <span class="tts-ai" hidden title="${L.aiVoiceTitle}"><i class="fa-solid fa-wand-magic-sparkles"></i> ${L.aiVoice}</span>
            <span class="tts-status" aria-live="polite"></span>
            <button type="button" class="tts-icon" data-act="close" title="${L.close}" aria-label="${L.close}"><i class="fa-solid fa-xmark"></i></button>
        </div>
        <div class="tts-controls">
            <button type="button" class="tts-icon" data-act="prev" title="${L.prev}" aria-label="${L.prev}"><i class="fa-solid fa-backward-step"></i></button>
            <button type="button" class="tts-play btn-grad-cus" data-act="play" title="${L.play}" aria-label="${L.play}"><i class="fa-solid fa-play"></i></button>
            <button type="button" class="tts-icon" data-act="next" title="${L.next}" aria-label="${L.next}"><i class="fa-solid fa-forward-step"></i></button>
            <button type="button" class="tts-icon" data-act="stop" title="${L.stop}" aria-label="${L.stop}"><i class="fa-solid fa-stop"></i></button>
        </div>
        <div class="tts-progress"><span></span></div>
        <div class="tts-options">
            <label>${L.speed}
                <select data-opt="rate">
                    <option value="0.75">0.75x</option>
                    <option value="1">1x</option>
                    <option value="1.25">1.25x</option>
                    <option value="1.5">1.5x</option>
                </select>
            </label>
            <label>${L.mode}
                <select data-opt="mode">
                    <option value="shown">${L.modeShown}</option>
                    <option value="both">${L.modeBoth}</option>
                </select>
            </label>
            <label class="tts-check"><input type="checkbox" data-opt="autoNext"> ${L.autoNext}</label>
        </div>
        <p class="tts-hint"><i class="fa-regular fa-hand-pointer"></i> ${L.hint}</p>`;
    document.body.appendChild(panel);

    const $ = (sel) => panel.querySelector(sel);
    $('[data-opt="rate"]').value = String(state.settings.rate);
    $('[data-opt="mode"]').value = state.settings.mode;
    $('[data-opt="autoNext"]').checked = !!state.settings.autoNext;

    function updatePanel() {
        const total = mainSegs().length;
        const playBtn = $('[data-act="play"]');
        playBtn.innerHTML = `<i class="fa-solid ${state.playing ? 'fa-pause' : 'fa-play'}"></i>`;
        playBtn.title = state.playing ? L.pause : L.play;
        playBtn.setAttribute('aria-label', playBtn.title);

        const cur = total ? Math.min(state.index + 1, total) : 0;
        $('.tts-status').textContent = state.playing ? `${L.reading} ${cur}/${total}` : (total ? `${cur}/${total}` : L.ready);
        $('.tts-progress span').style.width = total ? `${(cur / total) * 100}%` : '0';
        const shown = visibleSeg(state.index)?.getAttribute('data-lang') || viewer()?.currentSrcLang || '';
        $('.tts-ai').hidden = !chapterHasAudio(shown);

        toggles().forEach(b => {
            b.classList.toggle('is-on', state.open);
            b.classList.toggle('is-playing', state.playing);
            b.setAttribute('aria-pressed', state.open ? 'true' : 'false');
        });
        document.body.classList.toggle('tts-open', state.open);
    }

    function syncTheme() {
        const w = document.getElementById('mainWrapper');
        const dark = (w && w.classList.contains('theme-dark')) ||
            (focusOn() && document.getElementById('focusModeOverlay').classList.contains('theme-dark'));
        panel.classList.toggle('tts-dark', !!dark);
    }

    function openPanel() {
        state.open = true;
        panel.hidden = false;
        syncTheme();
        requestAnimationFrame(() => panel.classList.add('is-open'));
        updatePanel();
    }

    function closePanel() {
        stop();
        state.open = false;
        panel.classList.remove('is-open');
        setTimeout(() => { if (!state.open) panel.hidden = true; }, 200);
        updatePanel();
    }

    document.addEventListener('click', (e) => {
        if (!e.target.closest('.tts-toggle')) return;
        e.preventDefault();
        if (!state.open) { openPanel(); play(state.index); }
        else closePanel();
    });

    panel.addEventListener('click', (e) => {
        const b = e.target.closest('[data-act]');
        if (!b) return;
        const act = b.getAttribute('data-act');
        if (act === 'play') state.playing ? pause() : play();
        else if (act === 'stop') stop();
        else if (act === 'prev') step(-1);
        else if (act === 'next') step(1);
        else if (act === 'close') closePanel();
    });

    panel.addEventListener('change', (e) => {
        const opt = e.target.getAttribute('data-opt');
        if (!opt) return;
        if (opt === 'rate') state.settings.rate = parseFloat(e.target.value) || 1;
        else if (opt === 'mode') state.settings.mode = e.target.value;
        else if (opt === 'autoNext') state.settings.autoNext = e.target.checked;
        saveSettings();
        // Đổi tốc độ khi đang phát mp3: áp dụng ngay, không đọc lại từ đầu câu
        if (opt === 'rate' && state.playing && !player.paused) { player.playbackRate = state.settings.rate; return; }
        // Áp dụng ngay cho câu đang đọc
        if (state.playing && (opt === 'rate' || opt === 'mode')) speakIndex(state.index);
    });

    // ---------- Bấm vào câu để đọc từ câu đó ----------
    // Chờ một nhịp để không đụng thao tác NHẤP ĐÚP (dịch câu) của reading-viewer.js
    let clickTimer = null;
    document.addEventListener('click', (e) => {
        if (!state.open) return;
        const seg = e.target.closest('.text-segment');
        if (!seg) return;
        const inFocus = !!seg.closest('#focusContentContainer');
        const list = inFocus ? focusSegs() : mainSegs();
        const i = list.indexOf(seg);
        if (i < 0) return;
        clearTimeout(clickTimer);
        clickTimer = setTimeout(() => play(i), 280);
    });
    document.addEventListener('dblclick', (e) => {
        if (e.target.closest('.text-segment')) clearTimeout(clickTimer);
    });

    // ---------- Theo dõi thay đổi của trang đọc ----------
    // Chương mới / đổi ngôn ngữ -> nội dung được render lại
    const list = document.getElementById('paragraphList');
    let renderTimer = null;
    if (list) {
        new MutationObserver(() => {
            clearTimeout(renderTimer);
            renderTimer = setTimeout(() => {
                loadAudioMap(currentChapterId());
                if (state.pendingAutoPlay && mainSegs().length) {
                    state.pendingAutoPlay = false;
                    play(0);
                } else if (state.playing) {
                    stop();
                } else {
                    state.index = 0;
                    clearHighlight();
                    updatePanel();
                }
            }, 450);
        }).observe(list, { childList: true });
    }

    // Đổi theme sáng / tối
    ['mainWrapper', 'focusModeOverlay'].forEach(id => {
        const el = document.getElementById(id);
        if (el) new MutationObserver(syncTheme).observe(el, { attributes: true, attributeFilter: ['class'] });
    });

    // Bật / tắt Focus: tô sáng lại câu đang đọc trong bản Focus
    const overlay = document.getElementById('focusModeOverlay');
    if (overlay) {
        new MutationObserver(() => setTimeout(() => {
            if (state.playing || state.index > 0) highlight(state.index);
            syncTheme();
            updatePanel();
        }, 120)).observe(document.getElementById('focusContentContainer') || overlay, { childList: true });
    }

    // Phím tắt khi trình phát mở: Space = phát/tạm dừng, ←/→ = câu trước/sau
    document.addEventListener('keydown', (e) => {
        if (!state.open || e.ctrlKey || e.metaKey || e.altKey) return;
        const tag = (e.target.tagName || '').toLowerCase();
        if (['input', 'textarea', 'select'].includes(tag) || e.target.isContentEditable) return;
        if (e.code === 'Space') { e.preventDefault(); state.playing ? pause() : play(); }
        else if (e.key === 'ArrowRight') { e.preventDefault(); step(1); }
        else if (e.key === 'ArrowLeft') { e.preventDefault(); step(-1); }
    });

    // Rời trang thì dừng đọc
    window.addEventListener('pagehide', () => { player.pause(); cancelSynth(); });
    window.addEventListener('beforeunload', () => { player.pause(); cancelSynth(); });
    cancelSynth();
    loadAudioMap(currentChapterId());

    updatePanel();
})();
