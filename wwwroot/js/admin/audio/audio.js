/**
 * ============================================================================
 *  FILE      : wwwroot/js/admin/audio/audio.js
 *  CHỨC NĂNG : Admin > Audio truyện – tạo audio ElevenLabs THỦ CÔNG theo truyện/chương
 *              1. Chọn truyện → ngôn ngữ → model → giọng
 *              2. Chọn chương (xem số câu đã có audio / câu đã sửa / ký tự sẽ tạo)
 *              3. Ước tính credit, xác nhận, chạy job Hangfire, theo dõi tiến độ, dừng
 *              Xóa audio chương đã chọn, dọn file mp3 thừa
 *  DÙNG TẠI  : Areas/Admin/Views/Audio/Index.cshtml (nhãn: window.AUDIO_LANG)
 *  API       : /Admin/Audio/{Setup, Voices, ChapterStatus, Start, Status, Stop, Delete, CleanupOrphans}
 *              /Admin/tblParagraphs/{GetStoriesForSelect, GetLanguagesByStory}
 * ============================================================================
 */
(function () {
    'use strict';

    const L = window.AUDIO_LANG || {};
    const STORE_KEY = 'admin.audio';
    const LANG_INFO = {
        en: { name: 'English', flag: 'us' },
        vi: { name: 'Tiếng Việt', flag: 'vn' },
        zh: { name: '中文', flag: 'cn' },
        ja: { name: '日本語', flag: 'jp' },
        fr: { name: 'Français', flag: 'fr' }
    };
    const LANG_KEY_TO_CODE = { english: 'en', vietnamese: 'vi', chinese: 'zh', japanese: 'ja', french: 'fr' };

    const state = {
        models: [],
        voices: [],
        quota: null,
        storyId: 0,
        lang: '',
        modelId: '',
        voiceId: '',
        chapters: [],
        selected: new Set(),
        job: null,          // { jobId, progressKey }
        pollTimer: null,
        statusXhr: null,
        previewAudio: null
    };

    let saved = {};
    try { saved = JSON.parse(localStorage.getItem(STORE_KEY) || '{}') || {}; } catch (e) { saved = {}; }
    const remember = () => {
        try {
            localStorage.setItem(STORE_KEY, JSON.stringify({
                storyId: state.storyId, lang: state.lang, modelId: state.modelId, voiceId: state.voiceId
            }));
        } catch (e) { }
    };

    const $ = (id) => document.getElementById(id);
    const fmt = (n) => Number(n || 0).toLocaleString(L.isVi ? 'vi-VN' : 'en-US');
    const tpl = (s, ...args) => String(s || '').replace(/\{(\d+)\}/g, (_, i) => args[i] ?? '');
    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const toast = (msg, type) => (typeof window.showToast === 'function' ? window.showToast(msg, type || 'info') : alert(msg));

    const api = {
        get: (url, data) => window.jQuery.ajax({ url, type: 'GET', data, cache: false }),
        post: (url, body) => window.jQuery.ajax({ url, type: 'POST', data: JSON.stringify(body || {}), contentType: 'application/json' }),
        postForm: (url, data) => window.jQuery.ajax({ url, type: 'POST', data })
    };

    // ------------------------------------------------------------------
    // 1. KHỞI TẠO
    // ------------------------------------------------------------------
    function init() {
        bindEvents();
        loadSetup();
        loadStories();
        loadVoices(false);
    }

    function loadSetup() {
        return api.get('/Admin/Audio/Setup').done(res => {
            if (!res || !res.success) return;
            state.models = res.models || [];
            state.quota = res.quota || null;
            renderQuota(res.quotaError);
            renderModels();
            if (res.runningJob && !state.job) {
                state.job = { jobId: res.runningJob.jobId, progressKey: res.runningJob.progressKey };
                showJob();
                poll();
            }
        });
    }

    function loadStories() {
        const culture = L.isVi ? 'vi-VN' : 'en-US';
        api.get('/Admin/tblParagraphs/GetStoriesForSelect', { culture }).done(list => {
            const sel = $('audStory');
            sel.innerHTML = `<option value="">${esc(L.selectStory)}</option>` +
                (list || []).map(s => `<option value="${s.id}">${esc(s.name)}</option>`).join('');
            if (saved.storyId && (list || []).some(s => String(s.id) === String(saved.storyId))) {
                sel.value = String(saved.storyId);
                onStoryChange();
            }
        });
    }

    function loadVoices(refresh) {
        const sel = $('audVoice');
        sel.disabled = true;
        sel.innerHTML = `<option value="">${esc(L.loadingVoices)}</option>`;
        api.get('/Admin/Audio/Voices', { refresh: !!refresh }).done(res => {
            if (!res || !res.success) {
                sel.innerHTML = `<option value="">${esc(L.noVoices)}</option>`;
                if (res && res.message) $('audVoiceDesc').textContent = res.message;
                return;
            }
            state.voices = res.voices || [];
            const groups = {};
            state.voices.forEach(v => {
                const cat = L.categories?.[v.category] ? v.category : 'other';
                (groups[cat] = groups[cat] || []).push(v);
            });
            const order = ['cloned', 'professional', 'generated', 'premade', 'famous', 'other'];
            sel.innerHTML = `<option value="">${esc(L.selectVoice)}</option>` + order
                .filter(c => groups[c])
                .map(c => `<optgroup label="${esc(L.categories[c])}">` +
                    groups[c].map(v => `<option value="${esc(v.id)}">${esc(v.name)}</option>`).join('') +
                    '</optgroup>').join('');
            sel.disabled = false;

            const want = state.voiceId || saved.voiceId;
            if (want && state.voices.some(v => v.id === want)) sel.value = want;
            onVoiceChange();
        }).fail(() => {
            sel.innerHTML = `<option value="">${esc(L.noVoices)}</option>`;
        });
    }

    // ------------------------------------------------------------------
    // 2. HẠN MỨC & MODEL
    // ------------------------------------------------------------------
    function renderQuota(error) {
        const box = $('audQuota');
        if (!state.quota) {
            box.hidden = !error;
            if (error) {
                box.querySelector('small').textContent = error;
                box.querySelector('.aud-quota-bar span').style.width = '0';
            }
            return;
        }
        const q = state.quota;
        const pct = q.limit > 0 ? Math.max(0, Math.min(100, (q.remaining / q.limit) * 100)) : 0;
        box.hidden = false;
        box.classList.toggle('is-low', pct < 15);
        box.querySelector('.aud-quota-bar span').style.width = pct + '%';
        box.querySelector('small').textContent = tpl(L.quotaText, fmt(q.remaining), fmt(q.limit)) +
            (q.resetAt ? ' · ' + tpl(L.quotaReset, q.resetAt) : '');
    }

    function currentModel() {
        return state.models.find(m => m.id === state.modelId) || null;
    }

    function renderModels() {
        const box = $('audModels');
        if (!state.models.length) { box.innerHTML = ''; return; }

        // Model đã chọn không đọc được ngôn ngữ hiện tại -> chọn model đầu tiên đọc được
        const supports = (m) => !state.lang || (m.languages || []).includes(state.lang);
        if (!state.modelId) state.modelId = saved.modelId || '';
        const cur = currentModel();
        if (!cur || !supports(cur)) {
            const first = state.models.find(supports);
            state.modelId = first ? first.id : '';
        }

        box.innerHTML = state.models.map(m => {
            const ok = supports(m);
            return `<button type="button" class="aud-model ${m.id === state.modelId ? 'is-active' : ''}" data-model="${esc(m.id)}" ${ok ? '' : 'disabled'}>
                        <span class="aud-model-dot"></span>
                        <span>
                            <b>${esc(m.name)}</b>
                            <small>${esc(m.note)}</small>
                            ${ok ? '' : `<small class="aud-model-warn">${esc(L.notForLang)}</small>`}
                        </span>
                    </button>`;
        }).join('');
        updateSummary();
    }

    // ------------------------------------------------------------------
    // 3. TRUYỆN / NGÔN NGỮ / GIỌNG
    // ------------------------------------------------------------------
    function onStoryChange() {
        state.storyId = parseInt($('audStory').value, 10) || 0;
        state.selected.clear();
        state.chapters = [];
        renderChapters();
        remember();

        const box = $('audLangs');
        if (!state.storyId) {
            box.innerHTML = '';
            state.lang = '';
            return;
        }
        box.innerHTML = `<small class="aud-muted">${esc(L.loading)}</small>`;

        api.get('/Admin/tblParagraphs/GetLanguagesByStory', { storyId: state.storyId }).done(res => {
            const codes = [...new Set(((res && res.languages) || [])
                .map(l => LANG_KEY_TO_CODE[String(l.code || '').toLowerCase()])
                .filter(Boolean))];
            if (!codes.length) {
                box.innerHTML = `<small class="aud-muted">${esc(L.noLang)}</small>`;
                state.lang = '';
                renderModels();
                return;
            }
            box.innerHTML = codes.map(c => `
                <button type="button" class="aud-lang" data-lang="${c}">
                    <img src="/assets/flags/${LANG_INFO[c].flag}.svg" alt="" /> ${esc(LANG_INFO[c].name)}
                </button>`).join('');

            const want = codes.includes(state.lang) ? state.lang : (codes.includes(saved.lang) ? saved.lang : codes[0]);
            setLang(want);
        });
    }

    function setLang(code) {
        state.lang = code;
        document.querySelectorAll('#audLangs .aud-lang').forEach(b => b.classList.toggle('is-active', b.dataset.lang === code));
        remember();
        renderModels();
        loadChapters();
    }

    function onVoiceChange() {
        state.voiceId = $('audVoice').value || '';
        const v = state.voices.find(x => x.id === state.voiceId);
        $('audVoicePreview').disabled = !v;
        $('audVoiceDesc').textContent = v ? [v.description, v.category].filter(Boolean).join(' · ') : '';
        remember();
        updateSummary();
    }

    function previewVoice() {
        const v = state.voices.find(x => x.id === state.voiceId);
        if (!v) return;
        if (!v.previewUrl) { toast(L.previewNone, 'info'); return; }

        const btn = $('audVoicePreview');
        if (state.previewAudio && !state.previewAudio.paused) {
            state.previewAudio.pause();
            btn.innerHTML = '<i class="fa-solid fa-play"></i>';
            return;
        }
        state.previewAudio = new Audio(v.previewUrl);
        state.previewAudio.onended = () => { btn.innerHTML = '<i class="fa-solid fa-play"></i>'; };
        state.previewAudio.play().catch(() => toast(L.previewNone, 'info'));
        btn.innerHTML = '<i class="fa-solid fa-pause"></i>';
    }

    // ------------------------------------------------------------------
    // 4. CHƯƠNG
    // ------------------------------------------------------------------
    function loadChapters() {
        const list = $('audChapterList');
        if (!state.storyId || !state.lang) { state.chapters = []; renderChapters(); return; }

        if (state.statusXhr) state.statusXhr.abort();
        list.classList.add('is-loading');
        state.statusXhr = api.get('/Admin/Audio/ChapterStatus', {
            storyId: state.storyId, lang: state.lang, overwrite: $('audOverwrite').checked
        }).done(res => {
            state.chapters = (res && res.success && res.chapters) || [];
            // Bỏ chọn chương không còn tồn tại
            const ids = new Set(state.chapters.map(c => c.chapterId));
            [...state.selected].forEach(id => { if (!ids.has(id)) state.selected.delete(id); });
            renderChapters();
        }).always(() => {
            list.classList.remove('is-loading');
            state.statusXhr = null;
        });
    }

    function voiceName(id) {
        const v = state.voices.find(x => x.id === id);
        return v ? v.name : id.slice(0, 8) + '…';
    }

    function renderChapters() {
        const list = $('audChapterList');
        if (!state.storyId || !state.lang) {
            list.innerHTML = `<div class="mod-empty"><i class="fa-solid fa-book-open"></i><p>${esc(L.pickStory)}</p></div>`;
            syncSelectAll();
            updateSummary();
            return;
        }
        if (!state.chapters.length) {
            list.innerHTML = `<div class="mod-empty"><i class="fa-solid fa-book-open"></i><p>${esc(L.noChapters)}</p></div>`;
            updateSummary();
            return;
        }

        list.innerHTML = state.chapters.map(c => {
            const empty = c.total === 0;
            const sel = state.selected.has(c.chapterId);
            const donePct = c.total ? (c.done / c.total) * 100 : 0;
            const oldPct = c.total ? (c.outdated / c.total) * 100 : 0;
            const name = `${L.chapter} ${c.chapterNumber}${c.title ? ': ' + c.title : ''}`;

            const tags = [];
            if (!empty && c.done === c.total) tags.push(`<span class="aud-tag aud-tag--ok"><i class="fa-solid fa-check"></i> ${esc(L.complete)}</span>`);
            if (c.outdated > 0) tags.push(`<span class="aud-tag aud-tag--old"><i class="fa-solid fa-pen"></i> ${c.outdated} ${esc(L.outdated)}</span>`);
            (c.voices || []).slice(0, 2).forEach(v => {
                const [model, voice] = v.split('|');
                const m = state.models.find(x => x.id === model);
                tags.push(`<span class="aud-tag aud-tag--voice" title="${esc(model)}"><i class="fa-solid fa-microphone"></i> ${esc(voiceName(voice || ''))}${m ? ' · ' + esc(m.name) : ''}</span>`);
            });

            return `
            <label class="aud-chapter ${sel ? 'is-selected' : ''} ${empty ? 'is-empty' : ''}" data-id="${c.chapterId}">
                <input type="checkbox" ${sel ? 'checked' : ''} ${empty ? 'disabled' : ''} />
                <span class="aud-chapter-main">
                    <span class="aud-chapter-name" title="${esc(name)}">${esc(name)}</span>
                    <span class="aud-chapter-bar"><span class="is-done" style="width:${donePct}%"></span><span class="is-old" style="left:${donePct}%;width:${oldPct}%"></span></span>
                    <span class="aud-chapter-meta">
                        ${empty ? esc(L.noText) : `<span><b>${c.done}</b>/${c.total} ${esc(L.sentences)}</span>`}
                        ${tags.join('')}
                    </span>
                </span>
                <span class="aud-chapter-cost">
                    ${c.toGenerate > 0 ? `<b>${fmt(c.toGenerate)}</b>${esc(L.toGenerate)} · ${fmt(c.charsToGenerate)} ${esc(L.chars)}` : ''}
                </span>
            </label>`;
        }).join('');

        syncSelectAll();
        updateSummary();
    }

    function syncSelectAll() {
        const selectable = state.chapters.filter(c => c.total > 0);
        const all = $('audSelectAll');
        all.checked = selectable.length > 0 && selectable.every(c => state.selected.has(c.chapterId));
        all.indeterminate = !all.checked && selectable.some(c => state.selected.has(c.chapterId));
    }

    // ------------------------------------------------------------------
    // 5. TÓM TẮT / ƯỚC TÍNH
    // ------------------------------------------------------------------
    function selection() {
        const chosen = state.chapters.filter(c => state.selected.has(c.chapterId));
        const sentences = chosen.reduce((s, c) => s + c.toGenerate, 0);
        const chars = chosen.reduce((s, c) => s + c.charsToGenerate, 0);
        const audioCount = chosen.reduce((s, c) => s + c.done + c.outdated, 0);
        const m = currentModel();
        const credits = Math.ceil(chars * (m ? m.creditPerChar : 1));
        return { chosen, sentences, chars, credits, audioCount };
    }

    function updateSummary() {
        const s = selection();
        const text = $('audSummaryText');
        const running = !!state.job;

        if (!s.chosen.length) {
            text.textContent = L.noneSelected;
        } else {
            let html = tpl(L.summary, `<b>${s.chosen.length}</b>`, `<b>${fmt(s.sentences)}</b>`, fmt(s.chars), `<b>${fmt(s.credits)}</b>`);
            if (state.quota) {
                html += ' ' + tpl(L.summaryQuota, fmt(state.quota.remaining));
                if (state.quota.limit > 0 && s.credits > state.quota.remaining) html += `<span class="aud-summary-warn">${esc(L.overQuota)}</span>`;
            }
            if (s.sentences === 0) html += ` <span class="aud-muted">— ${esc(L.nothingToDo)}</span>`;
            text.innerHTML = html;
        }

        $('audStart').disabled = running || !s.chosen.length || s.sentences === 0 || !state.modelId || !state.voiceId || !state.lang;
        $('audDelete').disabled = running || !s.chosen.length || s.audioCount === 0;
        $('audCleanup').disabled = running;
    }

    // ------------------------------------------------------------------
    // 6. XÁC NHẬN
    // ------------------------------------------------------------------
    function confirmBox({ title, body, okText, danger }) {
        return new Promise(resolve => {
            const modal = $('audConfirm');
            const box = modal.querySelector('.aud-confirm-box');
            const ok = $('audConfirmOk');
            $('audConfirmTitle').textContent = title;
            $('audConfirmBody').innerHTML = body;
            ok.textContent = okText || 'OK';
            ok.classList.toggle('btn-grad-deny', !!danger);
            ok.classList.toggle('btn-grad-cus', !danger);
            box.classList.toggle('is-danger', !!danger);

            modal.hidden = false;
            requestAnimationFrame(() => modal.classList.add('is-open'));
            setTimeout(() => ok.focus(), 50);

            const close = (val) => {
                modal.classList.remove('is-open');
                setTimeout(() => { modal.hidden = true; }, 180);
                modal.removeEventListener('click', onClick);
                document.removeEventListener('keydown', onKey);
                resolve(val);
            };
            const onClick = (e) => {
                const b = e.target.closest('[data-confirm]');
                if (b) close(b.dataset.confirm === 'ok');
            };
            const onKey = (e) => { if (e.key === 'Escape') close(false); };
            modal.addEventListener('click', onClick);
            document.addEventListener('keydown', onKey);
        });
    }

    // ------------------------------------------------------------------
    // 7. TẠO / XÓA / DỌN
    // ------------------------------------------------------------------
    async function start() {
        const s = selection();
        const m = currentModel();
        const v = state.voices.find(x => x.id === state.voiceId);
        if (!s.sentences || !m || !v) return;

        const yes = await confirmBox({
            title: L.confirmStartTitle,
            body: tpl(L.confirmStartBody, fmt(s.sentences), fmt(s.chars), fmt(s.credits), esc(v.name), esc(m.name)) +
                `<small>${esc(L.confirmStartNote)}</small>`,
            okText: L.confirmStartOk
        });
        if (!yes) return;

        $('audStart').disabled = true;
        api.post('/Admin/Audio/Start', {
            storyId: state.storyId,
            chapterIds: s.chosen.map(c => c.chapterId),
            lang: state.lang,
            modelId: m.id,
            voiceId: v.id,
            voiceName: v.name,
            overwrite: $('audOverwrite').checked
        }).done(res => {
            if (!res || !res.success) { toast(res?.message || L.errConnect, 'error'); updateSummary(); return; }
            toast(res.message, 'success');
            state.job = { jobId: res.jobId, progressKey: res.progressKey };
            showJob();
            poll();
        }).fail(() => { toast(L.errConnect, 'error'); updateSummary(); });
    }

    async function removeAudio() {
        const s = selection();
        if (!s.audioCount) return;
        const yes = await confirmBox({
            title: L.confirmDeleteTitle,
            body: tpl(L.confirmDeleteBody, LANG_INFO[state.lang]?.name || state.lang, s.chosen.length),
            okText: L.confirmDeleteOk,
            danger: true
        });
        if (!yes) return;

        api.post('/Admin/Audio/Delete', { storyId: state.storyId, chapterIds: s.chosen.map(c => c.chapterId), lang: state.lang })
            .done(res => {
                toast(res?.message || L.errConnect, res?.success ? 'success' : 'error');
                if (res?.success) loadChapters();
            }).fail(() => toast(L.errConnect, 'error'));
    }

    async function cleanup() {
        const yes = await confirmBox({ title: L.confirmCleanupTitle, body: esc(L.confirmCleanupBody), okText: L.confirmCleanupOk, danger: true });
        if (!yes) return;
        api.postForm('/Admin/Audio/CleanupOrphans').done(res => {
            toast(res?.message || L.errConnect, res?.success ? 'success' : 'error');
        }).fail(() => toast(L.errConnect, 'error'));
    }

    // ------------------------------------------------------------------
    // 8. TIẾN ĐỘ JOB
    // ------------------------------------------------------------------
    function showJob() {
        const box = $('audJob');
        box.hidden = false;
        box.className = 'aud-job is-running';
        $('audJobTitle').textContent = L.jobQueued;
        $('audJobBar').style.width = '0';
        $('audJobCount').textContent = '';
        $('audJobChapter').textContent = '';
        $('audJobFailed').hidden = true;
        $('audJobError').hidden = true;
        $('audStop').hidden = false;
        $('audStop').disabled = false;
        updateSummary();
    }

    function poll() {
        clearTimeout(state.pollTimer);
        if (!state.job) return;

        api.get('/Admin/Audio/Status', state.job).done(res => {
            if (!res || !res.success || !state.job) return;
            const p = res.progress;

            if (p) {
                const pct = p.total ? (p.done / p.total) * 100 : (res.completed ? 100 : 0);
                $('audJobBar').style.width = pct + '%';
                $('audJobCount').innerHTML = `<b>${fmt(p.done)}</b>/${fmt(p.total)} ${esc(L.sentences)} · ${fmt(p.chars)} ${esc(L.chars)}`;
                $('audJobChapter').textContent = p.currentChapter || '';
                $('audJobFailed').hidden = !p.failed;
                $('audJobFailed').textContent = tpl(L.failedCount, p.failed);
                if (p.lastError && !res.completed) {
                    $('audJobError').hidden = false;
                    $('audJobError').textContent = p.lastError;
                }
            }

            if (!res.completed) {
                $('audJobTitle').textContent = res.state === 'Processing' ? L.jobRunning : L.jobQueued;
                state.pollTimer = setTimeout(poll, 2000);
                return;
            }

            // Kết thúc
            const box = $('audJob');
            const cancelled = (p && p.cancelled) || res.state === 'Deleted';
            const failed = res.state === 'Failed';
            box.className = 'aud-job ' + (failed ? 'is-failed' : cancelled ? 'is-cancelled' : 'is-done');
            $('audJobTitle').textContent = failed ? L.jobFailed : cancelled ? L.jobCancelled : L.jobDone;
            $('audStop').hidden = true;
            if (failed && res.error) {
                $('audJobError').hidden = false;
                $('audJobError').textContent = res.error;
            }
            toast($('audJobTitle').textContent, failed ? 'error' : cancelled ? 'warning' : 'success');

            state.job = null;
            loadSetup();      // cập nhật hạn mức
            loadChapters();   // cập nhật số câu đã có audio
            updateSummary();
        }).fail(() => {
            state.pollTimer = setTimeout(poll, 5000);
        });
    }

    function stop() {
        if (!state.job) return;
        $('audStop').disabled = true;
        api.postForm('/Admin/Audio/Stop', { jobId: state.job.jobId }).done(res => {
            toast(res?.message || L.errConnect, res?.success ? 'info' : 'error');
            if (!res?.success) $('audStop').disabled = false;
        }).fail(() => { $('audStop').disabled = false; toast(L.errConnect, 'error'); });
    }

    // ------------------------------------------------------------------
    // 9. SỰ KIỆN
    // ------------------------------------------------------------------
    function bindEvents() {
        $('audStory').addEventListener('change', onStoryChange);
        $('audVoice').addEventListener('change', onVoiceChange);
        $('audVoicePreview').addEventListener('click', previewVoice);
        $('audVoiceReload').addEventListener('click', () => loadVoices(true));
        $('audOverwrite').addEventListener('change', loadChapters);
        $('audStart').addEventListener('click', start);
        $('audDelete').addEventListener('click', removeAudio);
        $('audCleanup').addEventListener('click', cleanup);
        $('audStop').addEventListener('click', stop);
        $('audRefresh').addEventListener('click', () => { loadSetup(); loadChapters(); });

        $('audLangs').addEventListener('click', (e) => {
            const b = e.target.closest('.aud-lang');
            if (b && b.dataset.lang !== state.lang) setLang(b.dataset.lang);
        });

        $('audModels').addEventListener('click', (e) => {
            const b = e.target.closest('.aud-model');
            if (!b || b.disabled) return;
            state.modelId = b.dataset.model;
            document.querySelectorAll('#audModels .aud-model').forEach(x => x.classList.toggle('is-active', x === b));
            remember();
            updateSummary();
        });

        $('audChapterList').addEventListener('change', (e) => {
            const row = e.target.closest('.aud-chapter');
            if (!row || e.target.type !== 'checkbox') return;
            const id = parseInt(row.dataset.id, 10);
            if (e.target.checked) state.selected.add(id); else state.selected.delete(id);
            row.classList.toggle('is-selected', e.target.checked);
            syncSelectAll();
            updateSummary();
        });

        $('audSelectAll').addEventListener('change', (e) => {
            state.chapters.filter(c => c.total > 0).forEach(c => {
                if (e.target.checked) state.selected.add(c.chapterId); else state.selected.delete(c.chapterId);
            });
            renderChapters();
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
