// ──────────────────────────────────────────────────────────────
// Thermalview — Frontend Application
// Real-time ESC/POS ticket renderer via WebSocket
// ──────────────────────────────────────────────────────────────

(() => {
    'use strict';

    // ── State ──
    const state = {
        ws: null,
        connected: false,
        tickets: [],
        activeTicketId: null,
        paperWidth: 80,
        reconnectAttempts: 0,
        maxReconnectAttempts: 50,
        reconnectDelay: 1000,
    };

    // ── DOM References ──
    const dom = {
        statusBar: document.getElementById('status-bar'),
        statusText: document.getElementById('status-text'),
        statusClients: document.getElementById('status-clients'),
        ticketPaper: document.getElementById('ticket-paper'),
        ticketContent: document.getElementById('ticket-content'),
        ticketList: document.getElementById('ticket-list'),
        ticketMeta: document.getElementById('ticket-meta'),
        metaId: document.getElementById('meta-id'),
        metaTime: document.getElementById('meta-time'),
        metaSize: document.getElementById('meta-size'),
        metaElements: document.getElementById('meta-elements'),
        welcomeState: document.getElementById('welcome-state'),
        widthSelector: document.getElementById('width-selector'),
        btnTestPrint: document.getElementById('btn-test-print'),
        btnClearHistory: document.getElementById('clear-history'),
    };

    // ── WebSocket Connection ──
    function connectWebSocket() {
        const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
        const wsUrl = `${protocol}//${location.host}/ws/tickets`;

        console.log(`[WS] Connecting to ${wsUrl}...`);
        dom.statusText.textContent = 'Connecting...';

        try {
            state.ws = new WebSocket(wsUrl);
        } catch (err) {
            console.error('[WS] Failed to create WebSocket:', err);
            scheduleReconnect();
            return;
        }

        state.ws.onopen = () => {
            console.log('[WS] Connected');
            state.connected = true;
            state.reconnectAttempts = 0;
            dom.statusBar.className = 'status-bar status-connected';
            dom.statusText.textContent = 'Connected';
            showToast('Connected to Thermalview', 'success');
        };

        state.ws.onmessage = (event) => {
            try {
                const msg = JSON.parse(event.data);
                handleMessage(msg);
            } catch (err) {
                console.error('[WS] Failed to parse message:', err);
            }
        };

        state.ws.onclose = (event) => {
            console.log(`[WS] Disconnected (code: ${event.code})`);
            state.connected = false;
            dom.statusBar.className = 'status-bar status-disconnected';
            dom.statusText.textContent = 'Disconnected';
            scheduleReconnect();
        };

        state.ws.onerror = (err) => {
            console.error('[WS] Error:', err);
        };
    }

    function scheduleReconnect() {
        if (state.reconnectAttempts >= state.maxReconnectAttempts) {
            dom.statusText.textContent = 'Connection failed';
            return;
        }

        state.reconnectAttempts++;
        const delay = Math.min(
            state.reconnectDelay * Math.pow(1.5, state.reconnectAttempts - 1),
            10000
        );

        dom.statusText.textContent = `Reconnecting (${state.reconnectAttempts})...`;
        setTimeout(connectWebSocket, delay);
    }

    // ── Message Handling ──
    function handleMessage(msg) {
        switch (msg.type) {
            case 'connected':
                console.log(`[WS] Server says: ${msg.message} (id: ${msg.clientId})`);
                break;

            case 'ticket':
                handleNewTicket(msg.data);
                break;

            default:
                console.log('[WS] Unknown message type:', msg.type);
        }
    }

    function handleNewTicket(ticket) {
        console.log(`[Ticket] Received: ${ticket.id} (${ticket.elements.length} elements)`);

        // Add to history
        state.tickets.unshift(ticket);
        if (state.tickets.length > 100) state.tickets.pop();

        // Render the ticket
        renderTicket(ticket);
        updateTicketList();
        updateMeta(ticket);

        // Set as active
        state.activeTicketId = ticket.id;
        highlightActiveTicket();
    }

    // ── Ticket Rendering ──
    function renderTicket(ticket) {
        // Hide welcome state
        if (dom.welcomeState) {
            dom.welcomeState.classList.add('hidden');
        }

        // Clear previous content
        const content = dom.ticketContent;
        // Remove all children except welcome state
        while (content.firstChild) {
            if (content.firstChild === dom.welcomeState) {
                content.firstChild.classList.add('hidden');
                break;
            }
            content.removeChild(content.firstChild);
        }

        // Build ticket content
        const fragment = document.createDocumentFragment();

        for (const element of ticket.elements) {
            const el = renderElement(element);
            if (el) fragment.appendChild(el);
        }

        content.insertBefore(fragment, dom.welcomeState);

        // Animate
        dom.ticketPaper.classList.remove('ticket-new');
        void dom.ticketPaper.offsetWidth; // force reflow
        dom.ticketPaper.classList.add('ticket-new');

        // Show meta
        dom.ticketMeta.classList.remove('hidden');
    }

    function renderElement(element) {
        switch (element.type) {
            case 'text':
                return renderText(element);
            case 'image':
                return renderImage(element);
            case 'barcode':
                return renderBarcode(element);
            case 'cut':
                return renderCut(element);
            case 'feed':
                return renderFeed(element);
            default:
                console.log('[Render] Unknown element type:', element.type);
                return null;
        }
    }

    function renderText(el) {
        const span = document.createElement('span');
        span.className = 'ticket-line';
        span.textContent = el.content;

        if (el.align === 'center') span.classList.add('ticket-line--center');
        if (el.align === 'right') span.classList.add('ticket-line--right');
        if (el.bold) span.classList.add('ticket-line--bold');
        if (el.underline) span.classList.add('ticket-line--underline');
        if (el.fontB) span.classList.add('ticket-line--fontb');

        if (el.fontSize >= 3) {
            span.classList.add('ticket-line--xlarge');
        } else if (el.fontSize === 2) {
            span.classList.add('ticket-line--large');
        }

        return span;
    }

    function renderImage(el) {
        if (!el.dataBase64 || !el.width || !el.height) return null;

        // Convert 1-bit raster data to canvas, then to an image
        const canvas = document.createElement('canvas');
        canvas.width = el.width;
        canvas.height = el.height;
        const ctx = canvas.getContext('2d');

        try {
            const raw = atob(el.dataBase64);
            const imageData = ctx.createImageData(el.width, el.height);
            const bytesPerLine = Math.ceil(el.width / 8);

            for (let y = 0; y < el.height; y++) {
                for (let x = 0; x < el.width; x++) {
                    const byteIndex = y * bytesPerLine + Math.floor(x / 8);
                    const bitIndex = 7 - (x % 8);

                    if (byteIndex < raw.length) {
                        const bit = (raw.charCodeAt(byteIndex) >> bitIndex) & 1;
                        const pixelIndex = (y * el.width + x) * 4;

                        // ESC/POS: bit 1 = black, bit 0 = white
                        const color = bit ? 0 : 255;
                        imageData.data[pixelIndex] = color;     // R
                        imageData.data[pixelIndex + 1] = color; // G
                        imageData.data[pixelIndex + 2] = color; // B
                        imageData.data[pixelIndex + 3] = 255;   // A
                    }
                }
            }

            ctx.putImageData(imageData, 0, 0);
        } catch (err) {
            console.error('[Render] Failed to render image:', err);
            return null;
        }

        const img = document.createElement('img');
        img.src = canvas.toDataURL();
        img.className = 'ticket-image';
        img.alt = 'Ticket image';
        img.style.width = `${Math.min(el.width, 100)}%`;

        return img;
    }

    function renderBarcode(el) {
        const div = document.createElement('div');
        div.className = 'ticket-barcode';
        div.innerHTML = `<div>|||||||||||||||||||||||</div><div>${el.barcodeType}: ${el.data}</div>`;
        return div;
    }

    function renderCut(el) {
        const div = document.createElement('div');
        div.className = 'ticket-cut';
        return div;
    }

    function renderFeed(el) {
        const fragment = document.createDocumentFragment();
        const lines = el.lines || 1;
        for (let i = 0; i < lines; i++) {
            const br = document.createElement('span');
            br.className = 'ticket-feed';
            fragment.appendChild(br);
        }
        return fragment;
    }

    // ── Ticket List ──
    function updateTicketList() {
        if (state.tickets.length === 0) {
            dom.ticketList.innerHTML = `
                <div class="empty-state">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" width="40" height="40">
                        <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/>
                        <polyline points="14 2 14 8 20 8"/>
                        <line x1="16" y1="13" x2="8" y2="13"/>
                        <line x1="16" y1="17" x2="8" y2="17"/>
                    </svg>
                    <p>No tickets yet</p>
                    <p class="empty-hint">Print something to see it here</p>
                </div>`;
            return;
        }

        dom.ticketList.innerHTML = state.tickets
            .map(ticket => {
                const time = new Date(ticket.receivedAt).toLocaleTimeString();
                const preview = getTicketPreview(ticket);
                const isActive = ticket.id === state.activeTicketId;

                return `
                    <div class="ticket-item ${isActive ? 'active' : ''}"
                         data-ticket-id="${ticket.id}"
                         onclick="window.__thermalview.selectTicket('${ticket.id}')">
                        <div class="ticket-item-header">
                            <span class="ticket-item-id">#${ticket.id}</span>
                            <span class="ticket-item-time">${time}</span>
                        </div>
                        <div class="ticket-item-preview">${escapeHtml(preview)}</div>
                    </div>`;
            })
            .join('');
    }

    function getTicketPreview(ticket) {
        const textElements = ticket.elements.filter(e => e.type === 'text');
        if (textElements.length === 0) return 'Empty ticket';
        return textElements.slice(0, 2).map(e => e.content).join(' · ');
    }

    function highlightActiveTicket() {
        document.querySelectorAll('.ticket-item').forEach(el => {
            el.classList.toggle('active', el.dataset.ticketId === state.activeTicketId);
        });
    }

    function selectTicket(ticketId) {
        const ticket = state.tickets.find(t => t.id === ticketId);
        if (!ticket) return;

        state.activeTicketId = ticketId;
        renderTicket(ticket);
        updateMeta(ticket);
        highlightActiveTicket();
    }

    // ── Meta ──
    function updateMeta(ticket) {
        dom.metaId.textContent = `#${ticket.id}`;
        dom.metaTime.textContent = new Date(ticket.receivedAt).toLocaleTimeString();
        dom.metaSize.textContent = formatBytes(ticket.rawSizeBytes);
        dom.metaElements.textContent = `${ticket.elements.length} elements`;
    }

    function formatBytes(bytes) {
        if (bytes < 1024) return `${bytes} B`;
        if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;
        return `${(bytes / 1048576).toFixed(1)} MB`;
    }

    // ── Paper Width ──
    function setPaperWidth(width) {
        state.paperWidth = width;
        dom.ticketPaper.dataset.width = width;

        // Update button states
        dom.widthSelector.querySelectorAll('.width-btn').forEach(btn => {
            btn.classList.toggle('active', parseInt(btn.dataset.width) === width);
        });
    }

    // ── Test Print ──
    async function sendTestPrint() {
        const testEscPos = buildTestTicket();

        try {
            const response = await fetch('/api/print', {
                method: 'POST',
                headers: { 'Content-Type': 'application/octet-stream' },
                body: testEscPos,
            });

            if (response.ok) {
                showToast('Test print sent!', 'success');
            } else {
                showToast('Failed to send test print', 'error');
            }
        } catch (err) {
            showToast('Server unreachable', 'error');
            console.error('[Test] Failed:', err);
        }
    }

    function buildTestTicket() {
        const ESC = 0x1B;
        const GS = 0x1D;
        const LF = 0x0A;

        const parts = [];

        // Initialize
        parts.push(ESC, 0x40); // ESC @

        // Center align
        parts.push(ESC, 0x61, 1); // ESC a 1

        // Bold + Double size
        parts.push(ESC, 0x45, 1); // ESC E 1
        parts.push(GS, 0x21, 0x11); // GS ! 0x11 (double width+height)
        parts.push(...textToBytes('THERMALVIEW'));
        parts.push(LF);

        // Normal size, still bold
        parts.push(GS, 0x21, 0x00); // Reset size
        parts.push(...textToBytes('Test Receipt'));
        parts.push(LF);

        // Bold off
        parts.push(ESC, 0x45, 0);

        // Separator
        parts.push(...textToBytes('================================'));
        parts.push(LF);

        // Left align
        parts.push(ESC, 0x61, 0);

        // Items
        const items = [
            ['Espresso          ', '  $3.50'],
            ['Cappuccino        ', '  $4.75'],
            ['Croissant         ', '  $2.90'],
            ['Blueberry Muffin  ', '  $3.25'],
        ];

        for (const [name, price] of items) {
            parts.push(...textToBytes(name + price));
            parts.push(LF);
        }

        // Separator
        parts.push(...textToBytes('--------------------------------'));
        parts.push(LF);

        // Bold for total
        parts.push(ESC, 0x45, 1);
        parts.push(...textToBytes('TOTAL              '));
        // Right portion
        parts.push(...textToBytes(' $14.40'));
        parts.push(LF);
        parts.push(ESC, 0x45, 0);

        // Separator
        parts.push(...textToBytes('================================'));
        parts.push(LF);

        // Center
        parts.push(ESC, 0x61, 1);

        // Date/Time
        const now = new Date();
        parts.push(...textToBytes(now.toLocaleDateString() + ' ' + now.toLocaleTimeString()));
        parts.push(LF);

        // Thank you
        parts.push(LF);
        parts.push(...textToBytes('Thank you for your purchase!'));
        parts.push(LF);

        // Underline
        parts.push(ESC, 0x2D, 1); // ESC - 1
        parts.push(...textToBytes('www.thermalview.dev'));
        parts.push(LF);
        parts.push(ESC, 0x2D, 0);

        // Feed and cut
        parts.push(ESC, 0x64, 3); // Feed 3 lines
        parts.push(GS, 0x56, 0);  // Full cut

        return new Uint8Array(parts);
    }

    function textToBytes(text) {
        return Array.from(text).map(c => c.charCodeAt(0));
    }

    // ── Toast Notifications ──
    function showToast(message, type = 'info') {
        const existing = document.querySelector('.toast');
        if (existing) existing.remove();

        const toast = document.createElement('div');
        toast.className = `toast toast--${type}`;
        toast.textContent = message;
        document.body.appendChild(toast);

        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateY(10px)';
            toast.style.transition = 'all 0.3s ease';
            setTimeout(() => toast.remove(), 300);
        }, 3000);
    }

    // ── Utilities ──
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // ── Load History ──
    async function loadHistory() {
        try {
            const response = await fetch('/api/tickets?count=50');
            if (response.ok) {
                const data = await response.json();
                if (data.tickets && data.tickets.length > 0) {
                    state.tickets = data.tickets;
                    updateTicketList();

                    // Show the most recent ticket
                    const latest = state.tickets[0];
                    state.activeTicketId = latest.id;
                    renderTicket(latest);
                    updateMeta(latest);
                    highlightActiveTicket();
                }
            }
        } catch (err) {
            console.log('[History] Could not load ticket history');
        }
    }

    // ── Event Listeners ──
    function initEventListeners() {
        // Width selector
        dom.widthSelector.addEventListener('click', (e) => {
            const btn = e.target.closest('.width-btn');
            if (btn) {
                setPaperWidth(parseInt(btn.dataset.width));
            }
        });

        // Test print button
        dom.btnTestPrint.addEventListener('click', sendTestPrint);

        // Clear history
        dom.btnClearHistory.addEventListener('click', () => {
            state.tickets = [];
            state.activeTicketId = null;
            updateTicketList();

            // Reset ticket content
            dom.ticketContent.innerHTML = '';
            dom.ticketContent.appendChild(dom.welcomeState);
            dom.welcomeState.classList.remove('hidden');
            dom.ticketMeta.classList.add('hidden');
        });

        // Keyboard shortcuts
        document.addEventListener('keydown', (e) => {
            // Ctrl+T → Test print
            if (e.ctrlKey && e.key === 't') {
                e.preventDefault();
                sendTestPrint();
            }
        });
    }

    // ── Expose for onclick handlers ──
    window.__thermalview = { selectTicket };

    // ── Initialize ──
    function init() {
        initEventListeners();
        connectWebSocket();
        loadHistory();
        console.log('[Thermalview] Frontend initialized');
    }

    // Wait for DOM
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
