

'use strict';

let currentEventSource = null;
let isStreaming        = false;
let totalTokensSession = 0;
let streamingSpeedMs   = 30;
let reconnectAttempts  = 0;
const MAX_RECONNECTS   = 3;
const sessionId        = document.getElementById('session-id')?.value ?? '';

const messagesEl    = document.getElementById('messages');
const inputEl       = document.getElementById('message-input');
const sendBtn       = document.getElementById('send-btn');
const stopBtn       = document.getElementById('stop-btn');
const statusDot     = document.getElementById('status-dot');
const statusText    = document.getElementById('status-text');
const tokenCountEl  = document.getElementById('token-count');
const typingEl      = document.getElementById('typing-indicator');
const probWrap      = document.getElementById('prob-bar-wrap');
const probBar       = document.getElementById('prob-bar');
const probValue     = document.getElementById('prob-value');
const currentTokenEl= document.getElementById('current-token-display');
const charCountEl   = document.getElementById('char-count');

function sendMessage() {
    const text = inputEl.value.trim();
    if (!text || isStreaming) return;

    appendUserMessage(text);
    inputEl.value = '';
    updateCharCount('');
    autoResize(inputEl);

    startStreaming(text);
}

function handleKey(e) {
    if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        sendMessage();
    }
}

function startStreaming(message) {
    setStreamingState(true);
    showTyping(true);

    const url = `/Chat/Stream?message=${encodeURIComponent(message)}`
              + `&sessionId=${encodeURIComponent(sessionId)}`
              + `&speedMs=${streamingSpeedMs}`;

    if (currentEventSource) {
        currentEventSource.close();
        currentEventSource = null;
    }

    const es = new EventSource(url);
    currentEventSource = es;

    let assistantBubble  = null;
    let contentDiv       = null;
    let msgTokenCount    = 0;

    es.addEventListener('metadata', (e) => {
        showTyping(false);
        const data = JSON.parse(e.data);
        assistantBubble = appendAssistantMessage('', data.totalTokens);
        contentDiv = assistantBubble.querySelector('.msg-content');
        contentDiv.classList.add('streaming-cursor');
        probWrap.style.display = 'block';
        updateStatus('streaming', `جاري البث... (${data.totalTokens} token متوقع)`);
        reconnectAttempts = 0;
    });

    es.addEventListener('token', (e) => {
        const data = JSON.parse(e.data);
        if (!contentDiv) return;

        contentDiv.textContent += data.data;
        totalTokensSession++;
        msgTokenCount++;
        tokenCountEl.textContent = totalTokensSession;

        updateProbBar(data.probability, data.data);

        scrollToBottom();
    });

    es.addEventListener('done', (e) => {
        finishStream(contentDiv, assistantBubble, msgTokenCount, 'completed');
        es.close();
        currentEventSource = null;
    });

    es.addEventListener('cancelled', (e) => {
        finishStream(contentDiv, assistantBubble, msgTokenCount, 'cancelled');
        es.close();
        currentEventSource = null;
        showSystemMessage('⏹️ تم إيقاف البث بنجاح');
    });

    es.addEventListener('error', (e) => {
        const data = e.data ? JSON.parse(e.data) : {};
        finishStream(contentDiv, assistantBubble, msgTokenCount, 'error');
        es.close();
        currentEventSource = null;
        showSystemMessage(`❌ خطأ: ${data.message ?? 'حدث خطأ في البث'}`);
    });

    es.onerror = () => {
        if (es.readyState === EventSource.CLOSED) {
            if (isStreaming && reconnectAttempts < MAX_RECONNECTS) {
                reconnectAttempts++;
                updateStatus('error', `🔄 إعادة الاتصال... (${reconnectAttempts}/${MAX_RECONNECTS})`);
                setTimeout(() => {
                    if (isStreaming) {
                        updateStatus('streaming', 'جاري إعادة الاتصال...');
                    }
                }, 1500);
            } else if (reconnectAttempts >= MAX_RECONNECTS) {
                finishStream(contentDiv, assistantBubble, msgTokenCount, 'error');
                showSystemMessage('⚠️ تعذّر إعادة الاتصال. حاول مرة أخرى.');
                es.close();
                currentEventSource = null;
            }
        }
    };
}


async function stopStream() {
    if (!isStreaming) return;

    if (currentEventSource) {
        currentEventSource.close();
        currentEventSource = null;
    }

    try {
        await fetch('/Chat/Cancel', { method: 'POST' });
    } catch (_) { /* silent */ }

    setStreamingState(false);
    showTyping(false);
    probWrap.style.display = 'none';
    updateStatus('idle', '⏹️ تم إيقاف البث');
    setTimeout(() => updateStatus('idle', 'جاهز للدردشة'), 2000);
}


async function clearHistory() {
    if (isStreaming) { alert('يرجى إيقاف البث أولاً'); return; }
    if (!confirm('هل تريد مسح المحادثة؟')) return;

    try {
        await fetch('/Chat/ClearHistory', { method: 'POST' });
    } catch (_) { /* silent */ }

    const rows = messagesEl.querySelectorAll('.msg-row');
    rows.forEach((r, i) => { if (i > 0) r.remove(); });

    totalTokensSession = 0;
    tokenCountEl.textContent = '0';
    showSystemMessage('🗑️ تم مسح المحادثة');
}


function setSpeed(ms) {
    streamingSpeedMs = ms;
    document.getElementById('speed-slider').value = ms;
    document.getElementById('speed-ms').textContent = `${ms}ms/token`;

    document.querySelectorAll('.speed-btn').forEach(b => {
        b.classList.toggle('active', +b.dataset.speed === ms);
    });
}

function onSpeedSlide(val) {
    streamingSpeedMs = +val;
    document.getElementById('speed-ms').textContent = `${val}ms/token`;
    document.querySelectorAll('.speed-btn').forEach(b => b.classList.remove('active'));
}

function appendUserMessage(text) {
    const time = new Date().toLocaleTimeString('ar-SY', { hour: '2-digit', minute: '2-digit' });
    const row = document.createElement('div');
    row.className = 'msg-row user';
    row.innerHTML = `
        <div class="msg-bubble user-bubble">
            <div class="msg-content">${escapeHtml(text)}</div>
            <div class="msg-meta">
                <span class="msg-time">${time}</span>
            </div>
        </div>
        <div class="avatar user-avatar">👤</div>`;
    messagesEl.appendChild(row);
    scrollToBottom();
    return row;
}

function appendAssistantMessage(content, totalTokens) {
    const time = new Date().toLocaleTimeString('ar-SY', { hour: '2-digit', minute: '2-digit' });
    const row = document.createElement('div');
    row.className = 'msg-row assistant';
    row.innerHTML = `
        <div class="avatar ai-avatar">🤖</div>
        <div class="msg-bubble assistant-bubble">
            <div class="msg-content">${escapeHtml(content)}</div>
            <div class="msg-meta">
                <span class="msg-time">${time}</span>
                <span class="token-badge" id="live-token-badge">0 / ${totalTokens} tokens</span>
            </div>
        </div>`;
    messagesEl.appendChild(row);
    scrollToBottom();
    return row;
}

function finishStream(contentDiv, bubble, tokenCount, status) {
    if (contentDiv) {
        contentDiv.classList.remove('streaming-cursor');
    }
    if (bubble) {
        const badge = bubble.querySelector('#live-token-badge');
        if (badge) {
            badge.id = '';
            badge.textContent = `${tokenCount} tokens`;
            if (status === 'cancelled') {
                badge.textContent += ' ⏹️';
                badge.style.borderColor = 'var(--yellow)';
                badge.style.color = 'var(--yellow)';
            }
        }
    }
    setStreamingState(false);
    showTyping(false);
    probWrap.style.display = 'none';
    if (status === 'completed') {
        updateStatus('idle', '✅ اكتمل البث');
        setTimeout(() => updateStatus('idle', 'جاهز للدردشة'), 2000);
    }
}

function showSystemMessage(text) {
    const div = document.createElement('div');
    div.style.cssText = `text-align:center;padding:6px;font-size:12px;
        color:var(--text-3);font-family:var(--font-mono);animation:fadeSlideIn .2s ease`;
    div.textContent = text;
    messagesEl.appendChild(div);
    scrollToBottom();
    setTimeout(() => div.remove(), 4000);
}

function updateProbBar(prob, token) {
    const pct = Math.round(prob * 100);
    probBar.style.width = pct + '%';
    probValue.textContent = pct + '%';
    if (token && token.trim()) {
        currentTokenEl.textContent = `Token: "${token.trim()}"`;
    }

    if (pct >= 85)      probBar.style.background = 'linear-gradient(90deg,#22c55e,#4ade80)';
    else if (pct >= 65) probBar.style.background = 'linear-gradient(90deg,#7c3aed,#a855f7)';
    else                probBar.style.background = 'linear-gradient(90deg,#f59e0b,#fbbf24)';
}

function setStreamingState(streaming) {
    isStreaming = streaming;
    sendBtn.disabled = streaming;
    inputEl.disabled = streaming;
    stopBtn.style.display  = streaming ? 'block' : 'none';
    sendBtn.style.display  = streaming ? 'none'  : 'block';
    if (!streaming) statusDot.className = 'status-dot idle';
    else            statusDot.className = 'status-dot streaming';
}

function updateStatus(type, text) {
    statusDot.className = `status-dot ${type}`;
    statusText.textContent = text;
}

function showTyping(show) {
    typingEl.style.display = show ? 'block' : 'none';
    if (show) scrollToBottom();
}

function scrollToBottom() {
    const container = document.getElementById('messages-container');
    requestAnimationFrame(() => {
        container.scrollTop = container.scrollHeight;
    });
}

function autoResize(el) {
    el.style.height = 'auto';
    el.style.height = Math.min(el.scrollHeight, 150) + 'px';
    updateCharCount(el.value);
}

function updateCharCount(val) {
    charCountEl.textContent = `${val.length} / 500`;
    charCountEl.style.color = val.length > 450 ? 'var(--red)' : 'var(--text-3)';
}

function escapeHtml(str) {
    return str
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

document.addEventListener('DOMContentLoaded', () => {
    inputEl?.focus();
    scrollToBottom();

    if (inputEl) {
        inputEl.addEventListener('input', () => updateCharCount(inputEl.value));
    }
});
