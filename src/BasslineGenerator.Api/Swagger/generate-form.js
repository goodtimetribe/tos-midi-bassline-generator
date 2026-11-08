(() => {
    function installMidiDownload() {
        const operation = document.getElementById("operations-Generation-post_api_generate");
        const row = operation?.querySelector(".live-responses-table tbody tr.response");
        const code = row?.querySelector(".highlight-code code.language-json");
        if (!code || row.querySelector(".bassline-midi-download") ||
            row.querySelector(".response-col_status")?.textContent.trim() !== "200") return;

        const response = window.ui?.specSelectors.responseFor("/api/generate", "post")?.toJS();
        if (response?.status !== 200 || !response.body?.request || !response.body.midiSha256 ||
            !Number.isInteger(response.body.request.seed) ||
            !code.textContent.includes(response.body.midiSha256)) return;

        const container = document.createElement("div");
        container.className = "bassline-midi-download";
        const button = document.createElement("button");
        button.type = "button";
        button.className = "btn";
        button.textContent = "Download MIDI";
        const message = document.createElement("span");
        message.setAttribute("role", "status");
        message.style.marginLeft = "8px";
        container.append(button, message);
        code.closest(".highlight-code").before(container);

        button.addEventListener("click", async () => {
            button.disabled = true;
            message.textContent = "Preparing MIDI...";
            try {
                const current = window.ui.specSelectors.responseFor("/api/generate", "post")?.toJS();
                if (current?.status !== 200 || !Number.isInteger(current.body?.request?.seed) ||
                    !current.body?.midiSha256 || !code.textContent.includes(current.body.midiSha256))
                    throw new Error("The displayed response has changed. Execute the request again.");
                const result = await fetch(current.url, {
                    method: "POST",
                    headers: { "Content-Type": "application/json", "Accept": "audio/midi" },
                    body: JSON.stringify(current.body.request)
                });
                if (!result.ok || result.headers.get("content-type")?.split(";")[0].trim() !== "audio/midi")
                    throw new Error(`MIDI download failed (HTTP ${result.status}).`);
                const url = URL.createObjectURL(await result.blob());
                const link = document.createElement("a");
                link.href = url;
                link.download = "bassline.mid";
                link.click();
                setTimeout(() => URL.revokeObjectURL(url), 1000);
                message.textContent = "";
            } catch (error) {
                message.textContent = error instanceof Error ? error.message : "MIDI download failed.";
            } finally {
                button.disabled = false;
            }
        });
    }

    function installForm() {
        const operation = document.getElementById("operations-Generation-post_api_generate");
        const textarea = operation?.querySelector(".body-param__text");
        if (!textarea || textarea.disabled || operation.querySelector(".bassline-form")) return;

        const schema = window.ui?.specSelectors.specJson().toJS().components.schemas.GenerateRequest;
        if (!schema) return;

        const form = document.createElement("div");
        form.className = "bassline-form";
        const heading = document.createElement("h4");
        heading.textContent = "Bassline settings";
        form.appendChild(heading);
        const fields = {};
        const message = document.createElement("div");
        message.setAttribute("role", "status");
        const progressionDetails = schema.properties.progression["x-progression-details"] || [];
        const progressionInfo = document.createElement("div");
        progressionInfo.setAttribute("aria-live", "polite");
        let invalidSections = false;

        for (const [name, property] of Object.entries(schema.properties)) {
            const choices = property.enum || property["x-suggested-values"];
            const label = document.createElement("label");
            label.style.cssText = "display:inline-flex;flex-direction:column;gap:4px;margin:0 12px 12px 0";
            label.textContent = name + (property.minimum != null && property.maximum != null
                ? ` (${property.minimum}-${property.maximum})` : "");
            label.title = property.description || "";
            const field = document.createElement(name === "sections" ? "textarea" : choices || property.type === "boolean" ? "select" : "input");
            field.setAttribute("aria-label", name);
            field.dataset.property = name;
            if (choices) {
                if (!schema.required?.includes(name)) {
                    const option = document.createElement("option");
                    option.value = "";
                    option.textContent = name === "mode" ? "Default (single)" : "Default (random)";
                    field.appendChild(option);
                }
                for (const value of choices) {
                    const option = document.createElement("option");
                    option.value = value;
                    option.textContent = value;
                    field.appendChild(option);
                }
            } else if (property.type === "boolean") {
                for (const [value, text] of [["", "Default (false)"], ["true", "true"], ["false", "false"]]) {
                    const option = document.createElement("option");
                    option.value = value;
                    option.textContent = text;
                    field.appendChild(option);
                }
            } else if (name === "sections") {
                field.placeholder = '[{"name":"Intro","style":"Dreamy","bars":4}]';
                field.rows = 3;
            } else {
                field.type = "number";
                if (property.minimum != null) field.min = property.minimum;
                if (property.maximum != null) field.max = property.maximum;
                field.step = property.type === "number" ? "any" : "1";
            }
            fields[name] = field;
            label.appendChild(field);
            if (name === "progression") label.appendChild(progressionInfo);
            form.appendChild(label);
        }
        form.appendChild(message);

        function updateProgressionInfo(value) {
            const progression = progressionDetails.find(item =>
                item.name === value || item.id === value || item.legacy === value || item.sequence === value);
            progressionInfo.textContent = progression
                ? `${progression.sequence} — ${progression.description} Use: ${progression.suggestedUse} Mood: ${progression.mood}.`
                : value ? "Unknown progression; use an exact name, ID, or supported legacy sequence."
                : "Random progression selected for the scale.";
        }

        function parseBody() {
            const body = JSON.parse(textarea.value);
            if (!body || typeof body !== "object" || Array.isArray(body))
                throw new Error("The request body must be an object.");
            return body;
        }

        function readBody() {
            try {
                const body = parseBody();
                for (const [name, field] of Object.entries(fields)) {
                    if (name === "sections" && invalidSections) continue;
                    field.value = name === "sections" && body[name] != null
                        ? JSON.stringify(body[name], null, 2) : body[name] ?? "";
                    if (field.tagName === "SELECT" && field.selectedIndex === -1 && typeof body[name] === "string") {
                        if (name === "progression") {
                            const match = progressionDetails.find(item =>
                                item.id === body[name] || item.legacy === body[name] || item.sequence === body[name]);
                            if (match) field.value = match.name;
                        }
                        if (name !== "progression") {
                            const match = Array.from(field.options).find(option =>
                                option.value.toLowerCase() === body[name].trim().toLowerCase());
                            if (match) field.value = match.value;
                        }
                    }
                }
                updateProgressionInfo(body.progression);
                if (!invalidSections) message.textContent = "";
            } catch {
                message.textContent = "Enter a JSON object in the request body before using the form.";
            }
        }

        function writeBody(event) {
            let body;
            try { body = parseBody(); }
            catch {
                message.textContent = "Enter a JSON object in the request body before using the form.";
                return;
            }
            const field = event.target;
            if (field.dataset.property === "progression") updateProgressionInfo(field.value);
            if (field.dataset.property === "sections" && event.type === "input") {
                try {
                    if (field.value.trim() && !Array.isArray(JSON.parse(field.value))) throw new Error();
                    invalidSections = false;
                    if (!invalidSections) message.textContent = "";
                } catch {
                    invalidSections = true;
                    message.textContent = "Enter a JSON array of sections before applying the field.";
                }
                return;
            }
            if (field.value === "" && !schema.required?.includes(field.dataset.property))
                delete body[field.dataset.property];
            else if (field.dataset.property === "sections") {
                try {
                    const sections = JSON.parse(field.value);
                    if (!Array.isArray(sections)) throw new Error();
                    body.sections = sections;
                    invalidSections = false;
                } catch {
                    invalidSections = true;
                    message.textContent = "Enter a JSON array of sections before applying the field.";
                    return;
                }
            }
            else
                body[field.dataset.property] = field.type === "number"
                    ? (field.value === "" ? null : Number(field.value))
                    : schema.properties[field.dataset.property].type === "boolean"
                    ? field.value === "true"
                    : field.value;
            message.textContent = "";
            const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, "value").set;
            setter.call(textarea, JSON.stringify(body, null, 2));
            textarea.dispatchEvent(new Event("input", { bubbles: true }));
        }

        form.addEventListener("input", writeBody);
        form.addEventListener("change", writeBody);
        form.addEventListener("sync-example", () => { invalidSections = false; readBody(); });
        textarea.addEventListener("input", readBody);
        operation.addEventListener("click", event => {
            if (form.isConnected && !textarea.disabled && event.target.closest(".execute") && invalidSections) {
                event.preventDefault();
                event.stopImmediatePropagation();
                message.textContent = "Enter a JSON array of sections before executing the request.";
                fields.sections.focus();
            }
        }, true);
        readBody();
        textarea.parentElement.insertBefore(form, textarea);
    }

    window.addEventListener("load", () => {
        document.addEventListener("change", event => {
            if (event.target.matches("#operations-Generation-post_api_generate .examples-select-element")) {
                setTimeout(() => document.querySelector(".bassline-form")?.dispatchEvent(new Event("sync-example")), 0);
            }
        });
        const observer = new MutationObserver(() => {
            installForm();
            installMidiDownload();
        });
        observer.observe(document.getElementById("swagger-ui"), { childList: true, subtree: true });
        installForm();
        installMidiDownload();
    });
})();
