(() => {
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

        for (const [name, property] of Object.entries(schema.properties)) {
            const label = document.createElement("label");
            label.style.cssText = "display:inline-flex;flex-direction:column;gap:4px;margin:0 12px 12px 0";
            label.textContent = name + (property.minimum != null && property.maximum != null
                ? ` (${property.minimum}-${property.maximum})` : "");
            label.title = property.description || "";
            const field = document.createElement(property.enum ? "select" : "input");
            field.setAttribute("aria-label", name);
            field.dataset.property = name;
            if (property.enum) {
                for (const value of property.enum) {
                    const option = document.createElement("option");
                    option.value = value;
                    option.textContent = value;
                    field.appendChild(option);
                }
            } else {
                field.type = "number";
                if (property.minimum != null) field.min = property.minimum;
                if (property.maximum != null) field.max = property.maximum;
                field.step = "1";
            }
            fields[name] = field;
            label.appendChild(field);
            form.appendChild(label);
        }
        form.appendChild(message);

        function parseBody() {
            const body = JSON.parse(textarea.value);
            if (!body || typeof body !== "object" || Array.isArray(body))
                throw new Error("The request body must be an object.");
            return body;
        }

        function readBody() {
            try {
                const body = parseBody();
                for (const [name, field] of Object.entries(fields))
                    field.value = body[name] ?? "";
                message.textContent = "";
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
            if (field.type === "number" && field.value === "" && !schema.required?.includes(field.dataset.property))
                delete body[field.dataset.property];
            else
                body[field.dataset.property] = field.type === "number"
                    ? (field.value === "" ? null : Number(field.value))
                    : field.value;
            const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, "value").set;
            setter.call(textarea, JSON.stringify(body, null, 2));
            textarea.dispatchEvent(new Event("input", { bubbles: true }));
        }

        form.addEventListener("input", writeBody);
        form.addEventListener("change", writeBody);
        form.addEventListener("sync-example", readBody);
        textarea.addEventListener("input", readBody);
        readBody();
        textarea.parentElement.insertBefore(form, textarea);
    }

    window.addEventListener("load", () => {
        document.addEventListener("change", event => {
            if (event.target.matches("#operations-Generation-post_api_generate .examples-select-element")) {
                setTimeout(() => document.querySelector(".bassline-form")?.dispatchEvent(new Event("sync-example")), 0);
            }
        });
        const observer = new MutationObserver(installForm);
        observer.observe(document.getElementById("swagger-ui"), { childList: true, subtree: true });
        installForm();
    });
})();
