(function () {
    const sanitize = (s) => (s ?? "").toString();
    const toInt = (v) => {
        const n = Number.parseInt(v, 10);
        return Number.isFinite(n) ? n : null;
    };
    const toIntList = (v) => {
        if (v == null) return null;
        return String(v)
            .split(/[,\s;]+/)
            .map((x) => toInt(x))
            .filter((x) => x != null);
    };

    // ---------- helpers de render -----------
    function tableWrap(headCells, rows) {
        const thead = `<tr>${headCells.map((h) => `<th>${h}</th>`).join("")}</tr>`;
        const tbody = rows
            .map((r) => `<tr>${r.map((c) => `<td>${c}</td>`).join("")}</tr>`)
            .join("");
        return `
      <div class="table-wrap">
        <table class="table">
          <thead>${thead}</thead>
          <tbody>${tbody}</tbody>
        </table>
      </div>`;
    }

    async function httpFetch(url) {
        const res = await fetch(url, { mode: "cors", cache: "no-cache" });
        if (!res.ok) throw new Error(`HTTP ${res.status} ${res.statusText}`);
        const ct = res.headers.get("content-type") || "";
        if (ct.includes("application/json")) return res.json();
        return res.text();
    }

    // ========== ÚNICO ENDPOINT: Top productos ==========
    const TOP = {
        label: "Top productos",
        // Orden del wizard:
        reqOrder: ["year", "limit", "almacenIds"],          // requeridos
        optOrder: ["categoria", "metodoPago", "month"],     // opcionales

        buildUrl: ({ base, year, limit, almacenIds, categoria, metodoPago, month }) => {
            const p = new URLSearchParams({ year, limit });
            toIntList(almacenIds).forEach((id) => p.append("almacenIds", id));
            if (categoria) p.set("categoria", categoria);
            if (metodoPago) p.set("metodoPago", metodoPago);
            if (month) p.set("month", month);
            const url = `${base}/api/charts/top-productos?${p.toString()}`;
            console.log("[TopProductos] URL:", url); // debug
            return url;
        },

        render: (data) => {
            // Caso 1: tu API devuelve array de objetos {label, neto, unidades}
            if (Array.isArray(data)) {
                if (data.length === 0) return `<div class="msg-empty">Sin datos para los filtros especificados.</div>`;
                const rows = data.map((it) => [
                    sanitize(it.label ?? ""),
                    it.neto != null ? it.neto : "",
                    it.unidades != null ? it.unidades : "",
                ]);
                return tableWrap(["Producto", "Neto", "Unidades"], rows);
            }

            // Caso 2 (compat): formato categories + series
            const cats = data?.categories || [];
            const serie = data?.series?.[0]?.data || [];
            if (!cats.length) return `<div class="msg-empty">Sin datos para los filtros especificados.</div>`;
            const rows = cats.map((n, i) => [n, serie[i] ?? 0]);
            return tableWrap(["Producto", "Valor"], rows);
        },
    };

    // ---------- Chat principal (solo productos) ----------
    function ChatBot({ target, apiBase = "https://localhost:7288" }) {
        this.target = target;
        this.apiBase = apiBase.replace(/\/+$/, "");
        // estado del wizard
        this.state = {
            mode: null,               // 'top'
            reqIdx: 0,
            optIdx: 0,
            params: {},               // year, limit, almacenIds, categoria?, metodoPago?, month?
            askingOptional: false,
        };

        this.formHtml = `
      <div class="chat-container">
        <div class="flex-box no-wrap j-c-center a-i-center">
          <div>
            <h2 class="chat-title">Chat Bot Grupo 9</h2>
            <p class="chat-meta">Consulta: Top productos</p>
          </div>
          <img class="logo" alt="chat bot" src="data:image/png;base64,CODIGOLOGO NO TOMAR EN CUENTA" width="64" height="64"/>
        </div>

        <div class="chip-bar"></div>

        <div class="chat-log" aria-live="polite"></div>

        <div class="form-control">
          <input type="text" id="user-input" placeholder="Escribe: top productos"/>
        </div>
        <div class="form-control" style="gap:8px;">
          <button type="button" class="btn-enviar">Enviar</button>
          <button type="button" class="btn-omit" hidden>Omitir</button>
        </div>
      </div>`;
    }

    ChatBot.prototype.init = function () {
        this.target.innerHTML = this.formHtml;

        this.chatLog = this.target.querySelector(".chat-log");
        this.userInput = this.target.querySelector("#user-input");
        this.btnEnviar = this.target.querySelector(".btn-enviar");
        this.btnOmit = this.target.querySelector(".btn-omit");
        this.chipsBar = this.target.querySelector(".chip-bar");

        // botón único
        const b = document.createElement("button");
        b.type = "button";
        b.className = "chip";
        b.textContent = TOP.label;
        b.addEventListener("click", () => this.startTop());
        this.chipsBar.innerHTML = "";
        this.chipsBar.appendChild(b);

        const send = () => this.handleSend();
        this.btnEnviar.addEventListener("click", (e) => { e.preventDefault(); send(); });
        this.userInput.addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); send(); } });

        // Botón Omitir: solo debe actuar en opcionales
        this.btnOmit.addEventListener("click", () => {
            if (this.state.mode === "top" && this.state.askingOptional && this.state.optIdx < TOP.optOrder.length) {
                // avanzar sin guardar ese campo
                this.state.optIdx += 1;
                this.askNext();
            }
        });

        this.appendMessage("Bot", "Pulsa «Top productos» o escribe: «top productos» para comenzar.");
    };

    ChatBot.prototype.appendMessage = function (sender, text, html = false) {
        const msg = document.createElement("div");
        msg.className = sender === "Usuario" ? "msg-user" : "msg-bot";
        if (html) msg.innerHTML = text; else msg.textContent = text;
        this.chatLog.appendChild(msg);
        this.chatLog.scrollTop = this.chatLog.scrollHeight;
    };

    // ---- flujo Top productos
    ChatBot.prototype.startTop = function () {
        // reset duro
        this.state = { mode: "top", reqIdx: 0, optIdx: 0, params: {}, askingOptional: false };
        this.askNext();
    };

    ChatBot.prototype.askNext = function () {
        if (this.state.mode !== "top") return;

        // Requeridos en orden
        if (this.state.reqIdx < TOP.reqOrder.length) {
            const field = TOP.reqOrder[this.state.reqIdx];
            this.state.askingOptional = false;
            this.btnOmit.hidden = true;              // no se puede omitir requeridos
            this.appendMessage("Bot", promptFor(field));
            return;
        }

        // Opcionales: uno por uno
        if (this.state.optIdx < TOP.optOrder.length) {
            const field = TOP.optOrder[this.state.optIdx];
            this.state.askingOptional = true;
            this.btnOmit.hidden = false;             // mostrar botón Omitir
            this.appendMessage("Bot", promptFor(field) + " (opcional)");
            return;
        }

        // Ya tenemos todo → fetch
        this.btnOmit.hidden = true;                // ocultar antes de consultar
        this.fetchTop();
    };

    ChatBot.prototype.handleSend = function () {
        const raw = (this.userInput.value ?? "").trim();
        if (!raw) return;
        this.appendMessage("Usuario", raw);
        this.userInput.value = "";

        // iniciar por texto libre
        if (!this.state.mode && raw.toLowerCase().includes("top productos")) {
            return this.startTop();
        }

        if (this.state.mode !== "top") {
            return this.appendMessage("Bot", "Escribe «top productos» para comenzar.");
        }

        // Guardar respuesta en el campo actual
        if (this.state.reqIdx < TOP.reqOrder.length) {
            const field = TOP.reqOrder[this.state.reqIdx];
            const val = this.coerce(field, raw);
            if (val === null) return this.appendMessage("Bot", `Valor no válido para ${field}. Intenta de nuevo.`);
            this.state.params[field] = val;
            this.state.reqIdx += 1;
            return this.askNext();
        }

        if (this.state.optIdx < TOP.optOrder.length) {
            const field = TOP.optOrder[this.state.optIdx];
            const val = this.coerce(field, raw);
            if (val === null) return this.appendMessage("Bot", `Valor no válido para ${field}. Intenta nuevamente o usa «Omitir».`);
            this.state.params[field] = val;
            this.state.optIdx += 1;
            return this.askNext();
        }
    };

    ChatBot.prototype.coerce = function (field, raw) {
        if (["year", "limit", "month"].includes(field)) {
            const n = toInt(raw);
            return n == null ? null : n;
        }
        if (field === "almacenIds") {
            const list = toIntList(raw);
            return !list?.length ? null : list.join(","); // CSV
        }
        // categoria / metodoPago → texto libre
        return raw.trim();
    };

    ChatBot.prototype.fetchTop = async function () {
        const p = this.state.params;

        // Validación final
        if (p.year == null || p.limit == null || !p.almacenIds) {
            this.appendMessage("Bot", "Faltan datos requeridos (año, límite o IDs de almacén). Reiniciando…");
            return this.startTop();
        }

        try {
            this.appendMessage("Bot", "Consultando datos…");
            const url = TOP.buildUrl({ base: this.apiBase, ...p });
            const data = await httpFetch(url);
            let html;
            try { html = TOP.render(data); }
            catch { html = `<pre>${sanitize(JSON.stringify(data, null, 2))}</pre>`; }
            this.appendMessage("Bot", html, true);
        } catch (e) {
            this.appendMessage("Bot", `Error al consultar: ${e.message}`);
        } finally {
            // listo; permitir nueva consulta
            this.state = { mode: null, reqIdx: 0, optIdx: 0, params: {}, askingOptional: false };
            this.btnOmit.hidden = true;
        }
    };

    function promptFor(field) {
        const prompts = {
            year: "¿De qué año? (ej: 2025)",
            limit: "¿Cuántos items? (ej: 10)",
            almacenIds: "¿IDs de almacenes? (ej: 19 ó 1,2,3)",
            categoria: "¿Categoría? (ej: Calzado / P0)",
            metodoPago: "¿Método de pago? (ej: Efectivo / 201)",
            month: "¿Mes? (1-12)",
        };
        return prompts[field] || `Ingresa ${field}:`;
    }

    // Exponer constructor global
    window.ChatBot = ChatBot;
})();
